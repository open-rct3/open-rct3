// Terrain
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using NLog;
using OpenCobra.GDK.Assets;
using OpenRCT3.Platforms;
using OpenRCT3.Serialization;
using System.Linq;
using OpenCobra.Data;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using DatParks = OpenCobra.Data.Parks;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the terrain of the park, a grid of tiles, each 4x4 meters.
/// </summary>
/// <remarks>
/// <para>
/// The coordinate system is Y-up:
/// <list type="bullet">
/// <item><description>X: West to East (East is +X)</description></item>
/// <item><description>Y: Down to Up (Up is +Y)</description></item>
/// <item><description>Z: South to North (North is +Z)</description></item>
/// </list>
/// </para>
/// <para>
/// Synthetic terrain places the world origin at the middle of its South edge. Loaded terrain keeps
/// the XY origin stored in the RCT3 map.
/// </para>
/// <para>
/// Each tile owns four <see cref="TerrainCorner"/> records (one per corner) stored in a flat array
/// sized <c>Width * Height * 4</c>. Corners are <i>not</i> shared between tiles: a tile and the tile
/// across an edge each have their own <see cref="TerrainCorner"/> for the same world-space point.
/// An edge presents a smooth slope where the matching corner heights are equal, and a sheer cliff
/// where they differ.
/// </para>
/// </remarks>
public class Terrain {
  private const string MapPathEnvironmentVariable = "OPENRCT3_MAP_PATH";
  private const string SmokeRunIdEnvironmentVariable = "OPENRCT3_SMOKE_RUN_ID";
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private static readonly string DefaultMapPath = Path.Combine(
    "Campaigns",
    "Base",
    "BlankLandscape.dat"
  );

  /// <summary>
  /// The height of one corner step, in meters. One corner-height unit = 1 cm; the freeform sculpting
  /// tools are continuous drag-based, so the corner grid has to be finer than the 1m "ramp rise"
  /// snap granularity to let gentle hills and valleys resolve smoothly.
  /// </summary>
  public const float HeightStep = 0.01f;

  /// <summary>
  /// The number of corners owned by a single tile.
  /// </summary>
  public const int CornersPerTile = 4;

  /// <summary>The width of the terrain grid in tiles, including the OOB border.</summary>
  public int Width { get; }
  /// <summary>The height of the terrain grid in tiles, including the OOB border.</summary>
  public int Height { get; }

  /// <summary>The world-space XY position of the terrain grid's south-west corner.</summary>
  public Vector2 Origin { get; }

  /// <summary>The world-space XY size of one terrain tile.</summary>
  public Vector2 TileSize { get; }

  /// <summary>The world-space XY bounds of the full OOB-inclusive terrain grid.</summary>
  public (Vector2 Min, Vector2 Max) Bounds => (
    Origin,
    Origin + new Vector2(Width * TileSize.X, Height * TileSize.Y)
  );

  /// <summary>The complete decoded terrain/cliff texture catalog for this map.</summary>
  public TerrainTextureCatalog? TextureCatalog { get; private set; }

  /// <summary>The default grass texture retained for callers that only need Terrain_00.</summary>
  public OpenCobra.GDK.Materials.Texture? GrassTexture => TextureCatalog?.GetSurface(0);

  private readonly TerrainCorner[] _corners;

  /// <summary>
  /// Initializes a new instance of the <see cref="Terrain"/> class with all corners at the given
  /// initial height.
  /// </summary>
  /// <param name="width">The buildable width in tiles.</param>
  /// <param name="height">The buildable height in tiles.</param>
  /// <param name="initialHeight">
  /// The starting corner height in <see cref="HeightStep"/> units applied to every corner.
  /// </param>
  public Terrain(int width = Park.DefaultMapSize, int height = Park.DefaultMapSize, int initialHeight = 0)
    : this(width, height, initialHeight, dimensionsIncludeBorder: false) { }

  private Terrain(
    int width,
    int height,
    int initialHeight,
    bool dimensionsIncludeBorder,
    Vector2? origin = null,
    Vector2? tileSize = null) {
    if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

    Width = dimensionsIncludeBorder ? width : checked(width + (Park.OutOfBoundsBorder * 2));
    Height = dimensionsIncludeBorder ? height : checked(height + (Park.OutOfBoundsBorder * 2));
    TileSize = tileSize ?? new Vector2(Park.TileSize, Park.TileSize);
    Origin = origin ?? new Vector2(-(Width / 2f) * TileSize.X, 0f);
    if (!float.IsFinite(TileSize.X) || TileSize.X <= 0f)
      throw new ArgumentOutOfRangeException(nameof(tileSize));
    if (!float.IsFinite(TileSize.Y) || TileSize.Y <= 0f)
      throw new ArgumentOutOfRangeException(nameof(tileSize));
    if (!float.IsFinite(Origin.X) || !float.IsFinite(Origin.Y))
      throw new ArgumentOutOfRangeException(nameof(origin));
    var cornerCount = checked(Width * Height * CornersPerTile);
    _corners = new TerrainCorner[cornerCount];
    for (var i = 0; i < _corners.Length; i++)
      _corners[i] = new TerrainCorner { Height = initialHeight };
  }

  /// <summary>
  /// Loads the terrain data and textures.
  /// </summary>
  /// <param name="mapPath">
  /// Optional absolute path, or path relative to the configured RCT3 installation. The
  /// <c>OPENRCT3_MAP_PATH</c> environment variable, app config, and blank landscape are used in that
  /// order when this is omitted.
  /// </param>
  /// <returns>A loaded <see cref="Terrain"/> instance.</returns>
  public static Terrain Load(string? mapPath = null) => Load(out _, mapPath);

  /// <summary>Loads terrain plus its decoded WaterManager data for world construction.</summary>
  internal static Terrain Load(
    out DatWaterManagerData? waterManager,
    string? mapPath = null
  ) {
    var config = AppConfig.Instance;
    Debug.Assert(config.InstallPath != null);
    var installPath = config.InstallPath
      ?? throw new InvalidOperationException("RCT3 installation path is not configured.");

    var configuredMapPath = mapPath;
    if (string.IsNullOrWhiteSpace(configuredMapPath))
      configuredMapPath = Environment.GetEnvironmentVariable(MapPathEnvironmentVariable);
    if (string.IsNullOrWhiteSpace(configuredMapPath)) configuredMapPath = config.MapPath;
    if (string.IsNullOrWhiteSpace(configuredMapPath)) configuredMapPath = DefaultMapPath;
    var selectedMapPath = configuredMapPath ?? DefaultMapPath;
    var resolvedMapPath = Path.IsPathRooted(selectedMapPath)
      ? selectedMapPath
      : Path.Combine(installPath, selectedMapPath);

    var smokeRunId = Environment.GetEnvironmentVariable(SmokeRunIdEnvironmentVariable);
    var loadedMapPath = Path.GetFullPath(resolvedMapPath);
    var loadedMapBytes = string.IsNullOrWhiteSpace(smokeRunId)
      ? null
      : File.ReadAllBytes(loadedMapPath);
    using var loadedMap = loadedMapBytes == null
      ? null
      : new MemoryStream(loadedMapBytes, writable: false);
    var data = loadedMap == null
      ? DatTerrainReader.Read(loadedMapPath)
      : DatTerrainReader.Read(loadedMap);
    if (loadedMapBytes != null) {
      var loadedMapSha256 = Convert.ToHexString(SHA256.HashData(loadedMapBytes));
      var loadedMapIdentity = JsonSerializer.Serialize(new {
        path = loadedMapPath,
        sha256 = loadedMapSha256
      });
      logger.Info($"Native smoke loaded map {loadedMapIdentity}");
    }
    var terrain = FromData(data);
    waterManager = data.WaterManager;

    // Load textures from terrain/RCT3/Terrain_RCT3.common.ovl
    var terrainOvl = Path.Combine(installPath, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    terrain.TextureCatalog = TextureLoader.LoadTerrainCatalog(terrainOvl);

    return terrain;
  }

  /// <summary>Builds editable simulation terrain from decoded RCT3 terrain data.</summary>
  internal static Terrain FromData(DatTerrainData data) {
    var expectedCellCount = checked(data.Width * data.Height);
    if (data.Cells.Count != expectedCellCount)
      throw new InvalidDataException("Decoded terrain cell count does not match its dimensions.");

    var terrain = new Terrain(
      data.Width,
      data.Height,
      0,
      dimensionsIncludeBorder: true,
      origin: new Vector2(data.OriginX, data.OriginY),
      tileSize: new Vector2(data.TileSizeX, data.TileSizeY)
    );
    for (var cellIndex = 0; cellIndex < data.Cells.Count; cellIndex++) {
      var tileX = cellIndex % data.Width;
      var tileY = cellIndex / data.Width;
      var cell = data.Cells[cellIndex];
      terrain.SetCorner(
        tileX,
        tileY,
        TerrainCornerSlot.SouthWest,
        new TerrainCorner(DecodeCornerHeight(cell.SouthWestHeight), cell.SurfaceIndex, cell.CliffIndex)
      );
      terrain.SetCorner(
        tileX,
        tileY,
        TerrainCornerSlot.SouthEast,
        new TerrainCorner(DecodeCornerHeight(cell.SouthEastHeight), cell.SurfaceIndex, cell.CliffIndex)
      );
      terrain.SetCorner(
        tileX,
        tileY,
        TerrainCornerSlot.NorthWest,
        new TerrainCorner(DecodeCornerHeight(cell.NorthWestHeight), cell.SurfaceIndex, cell.CliffIndex)
      );
      terrain.SetCorner(
        tileX,
        tileY,
        TerrainCornerSlot.NorthEast,
        new TerrainCorner(DecodeCornerHeight(cell.NorthEastHeight), cell.SurfaceIndex, cell.CliffIndex)
      );
    }

    return terrain;
  }

  /// <summary>Builds a <see cref="Terrain"/> sized and shaped from a saved park's decoded corner-height grid.</summary>
  internal static Terrain LoadFromSave(string path) {
    // Share the decoded corner-grid factory so saved dimensions, origins, material indices, and
    // signed heights match the full park loader without adding a second out-of-bounds border.
    return FromData(DatTerrainReader.Read(path));
  }

  private static uint ColorDistance(uint c1, uint c2) {
    var r1 = (byte)((c1 >> 16) & 0xFF);
    var g1 = (byte)((c1 >> 8) & 0xFF);
    var b1 = (byte)(c1 & 0xFF);

    var r2 = (byte)((c2 >> 16) & 0xFF);
    var g2 = (byte)((c2 >> 8) & 0xFF);
    var b2 = (byte)(c2 & 0xFF);

    var dr = (int)r1 - r2;
    var dg = (int)g1 - g2;
    var db = (int)b1 - b2;

    return (uint)(dr * dr + dg * dg + db * db);
  }

  /// <summary>
  /// Computes the flat-array index for a tile's corner.
  /// </summary>
  /// <param name="tileX">The tile's X index in the OOB-inclusive grid.</param>
  /// <param name="tileY">The tile's Y index in the OOB-inclusive grid.</param>
  /// <param name="slot">Which corner of the tile to address.</param>
  /// <returns>The flat-array offset of the requested corner.</returns>
  public int GetCornerIndex(int tileX, int tileY, TerrainCornerSlot slot)
    => ((tileY * Width) + tileX) * CornersPerTile + (int)slot;

  /// <summary>
  /// Returns the <see cref="TerrainCorner"/> at the given tile corner.
  /// </summary>
  /// <param name="tileX">The tile's X index in the OOB-inclusive grid.</param>
  /// <param name="tileY">The tile's Y index in the OOB-inclusive grid.</param>
  /// <param name="slot">Which corner of the tile to read.</param>
  public TerrainCorner GetCorner(int tileX, int tileY, TerrainCornerSlot slot)
    => _corners[GetCornerIndex(tileX, tileY, slot)];

  /// <summary>
  /// Stores the <see cref="TerrainCorner"/> at the given tile corner.
  /// </summary>
  /// <param name="tileX">The tile's X index in the OOB-inclusive grid.</param>
  /// <param name="tileY">The tile's Y index in the OOB-inclusive grid.</param>
  /// <param name="slot">Which corner of the tile to write.</param>
  /// <param name="corner">The corner data to store.</param>
  public void SetCorner(int tileX, int tileY, TerrainCornerSlot slot, TerrainCorner corner)
    => _corners[GetCornerIndex(tileX, tileY, slot)] = corner;

  /// <summary>
  /// Returns a span over the four corners of the tile at <paramref name="tileX"/>, <paramref name="tileY"/>,
  /// in <see cref="TerrainCornerSlot"/> order.
  /// </summary>
  public Span<TerrainCorner> GetCorners(int tileX, int tileY)
    => _corners.AsSpan(GetCornerIndex(tileX, tileY, TerrainCornerSlot.SouthWest), CornersPerTile);

  /// <summary>
  /// Whether a tile's edge presents a cliff (vertical face) — i.e. the matching corner heights on
  /// both sides of the edge differ.
  /// </summary>
  /// <param name="tileX">The tile's X index in the OOB-inclusive grid.</param>
  /// <param name="tileY">The tile's Y index in the OOB-inclusive grid.</param>
  /// <param name="edge">The edge to inspect.</param>
  /// <returns>
  /// <c>true</c> if the edge has at least one corner pair with differing heights, <c>false</c> if
  /// both corner pairs are equal (smooth slope) or the tile is missing a neighbor across
  /// <paramref name="edge"/>.
  /// </returns>
  public bool IsEdgeDetached(int tileX, int tileY, Edge edge) {
    var (c1, c2, dx, dy) = GetEdgeCornerPair(edge);
    var nx = tileX + dx;
    var ny = tileY + dy;
    if (!HasTile(nx, ny)) return false;
    if (!HasTile(tileX, tileY)) return false;

    var thisC1 = GetCorner(tileX, tileY, c1);
    var thisC2 = GetCorner(tileX, tileY, c2);
    var thatC1 = GetCorner(nx, ny, MirrorAcrossEdge(c1, edge));
    var thatC2 = GetCorner(nx, ny, MirrorAcrossEdge(c2, edge));
    return thisC1.Height != thatC1.Height || thisC2.Height != thatC2.Height;
  }

  /// <summary>
  /// Raises a corner by <paramref name="delta"/> corner-steps, snapping to the matching corner on
  /// every neighbor that shares it (so the shared edge stays smooth-joined).
  /// </summary>
  /// <remarks>
  /// <para>
  /// The <paramref name="maxHeightQuery"/> lets a caller cap a corner's height — for example, a
  /// placed ride's foundation or track segment footprint caps how far corners under/adjacent to it
  /// can be raised. The cap is read for the editing tile's corner first, then per shared-corner
  /// copy; the resulting corner height is the minimum across all caps.
  /// </para>
  /// <para>
  /// To produce a cliff, call <see cref="SetCornerHeight"/> instead: that stores a height for one
  /// copy of the corner without propagating, which detaches the edge.
  /// </para>
  /// </remarks>
  /// <param name="tileX">The tile's X index in the OOB-inclusive grid.</param>
  /// <param name="tileY">The tile's Y index in the OOB-inclusive grid.</param>
  /// <param name="slot">Which corner of the tile to raise.</param>
  /// <param name="delta">The amount in corner-steps to raise (negative to lower).</param>
  /// <param name="maxHeightQuery">
  /// Optional per-corner ceiling. Receives the (tileX, tileY, slot) for each shared-corner copy and
  /// returns the maximum allowed height; if the cap is below the requested raise, the raise is
  /// clamped. <c>null</c> (default) means unconstrained (<see cref="int.MaxValue"/>).
  /// </param>
  public void RaiseCorner(
    int tileX,
    int tileY,
    TerrainCornerSlot slot,
    int delta,
    Func<int, int, TerrainCornerSlot, int>? maxHeightQuery = null) {
    if (delta == 0) return;
    if (!HasTile(tileX, tileY)) return;

    var current = GetCorner(tileX, tileY, slot);
    var ceiling = maxHeightQuery?.Invoke(tileX, tileY, slot) ?? int.MaxValue;
    var newHeight = ClampHeight(Convert.ToInt64(current.Height) + delta, upper: ceiling);

    foreach (var (nx, ny, neighborSlot) in EnumerateSharedCorners(tileX, tileY, slot)) {
      var neighborCeiling = maxHeightQuery?.Invoke(nx, ny, neighborSlot) ?? int.MaxValue;
      var clamped = ClampHeight(newHeight, upper: neighborCeiling);
      var neighborCorner = GetCorner(nx, ny, neighborSlot);
      neighborCorner.Height = clamped;
      SetCorner(nx, ny, neighborSlot, neighborCorner);
    }

    current.Height = newHeight;
    SetCorner(tileX, tileY, slot, current);
  }

  /// <summary>Alias for <see cref="RaiseCorner"/> with a negative delta.</summary>
  public void LowerCorner(
    int tileX,
    int tileY,
    TerrainCornerSlot slot,
    int delta,
    Func<int, int, TerrainCornerSlot, int>? minHeightQuery = null) {
    if (delta == 0) return;
    if (!HasTile(tileX, tileY)) return;

    var current = GetCorner(tileX, tileY, slot);
    var floor = minHeightQuery?.Invoke(tileX, tileY, slot) ?? int.MinValue;
    var newHeight = ClampHeight(Convert.ToInt64(current.Height) - delta, lower: floor);

    foreach (var (nx, ny, neighborSlot) in EnumerateSharedCorners(tileX, tileY, slot)) {
      var neighborFloor = minHeightQuery?.Invoke(nx, ny, neighborSlot) ?? int.MinValue;
      var clamped = ClampHeight(newHeight, lower: neighborFloor);
      var neighborCorner = GetCorner(nx, ny, neighborSlot);
      neighborCorner.Height = clamped;
      SetCorner(nx, ny, neighborSlot, neighborCorner);
    }

    current.Height = newHeight;
    SetCorner(tileX, tileY, slot, current);
  }

  /// <summary>
  /// Stores a height for one copy of a corner without propagating to neighbors — i.e. explicitly
  /// detaches every shared edge at that corner. The edge re-joins automatically if a later
  /// raise/lower brings the matching neighbor corner back to the same height.
  /// </summary>
  public void SetCornerHeight(int tileX, int tileY, TerrainCornerSlot slot, int height) {
    if (!HasTile(tileX, tileY)) return;
    var corner = GetCorner(tileX, tileY, slot);
    corner.Height = height;
    SetCorner(tileX, tileY, slot, corner);
  }

  /// <summary>True if <c>(tileX, tileY)</c> lies within the OOB-inclusive grid.</summary>
  public bool HasTile(int tileX, int tileY)
    => tileX >= 0 && tileX < Width && tileY >= 0 && tileY < Height;

  /// <summary>
  /// Enumerates every tile that owns a copy of the corner at <c>(tileX, tileY).slot</c>, including
  /// <paramref name="tileX"/>, <paramref name="tileY"/> itself.
  /// </summary>
  /// <remarks>
  /// Exposed so callers that need to know which tiles were actually affected by a
  /// <see cref="RaiseCorner"/>/<see cref="LowerCorner"/> edit (e.g. to invalidate a <see cref="WaterPool"/>
  /// covering any of them) don't have to re-derive the shared-corner relationship themselves.
  /// </remarks>
  public IEnumerable<(int X, int Y)> GetTilesSharingCorner(int tileX, int tileY, TerrainCornerSlot slot) {
    yield return (tileX, tileY);
    foreach (var (x, y, _) in EnumerateSharedCorners(tileX, tileY, slot))
      yield return (x, y);
  }

  /// <summary>
  /// Returns the heights of the two corners of the tile at <paramref name="tileX"/>,
  /// <paramref name="tileY"/> that bound <paramref name="edge"/>.
  /// </summary>
  /// <remarks>
  /// The pair is ordered so that comparing it against the neighboring tile's pair for the
  /// <see cref="EdgeExtensions.Opposite"/> edge lines up matching world-space corners
  /// index-for-index (e.g. this tile's South-edge pair is (SouthWest, SouthEast); the neighbor's
  /// North-edge pair is (NorthWest, NorthEast), and SouthWest/NorthWest share a world position, as do
  /// SouthEast/NorthEast).
  /// </remarks>
  public (int c1, int c2) GetEdgeCornerHeights(int tileX, int tileY, Edge edge) {
    var (c1, c2, _, _) = GetEdgeCornerPair(edge);
    return (GetCorner(tileX, tileY, c1).Height, GetCorner(tileX, tileY, c2).Height);
  }

  /// <summary>Converts a corner-height count to world-space Z, in meters.</summary>
  public static float CornerHeightToWorldZ(int cornerHeight) => cornerHeight * HeightStep;
  /// <summary>Converts a corner-height count to world-space Y, in meters.</summary>
  public static float CornerHeightToWorldY(int cornerHeight) => cornerHeight * HeightStep;

  /// <summary>Converts a world-space Z value in meters to signed corner-height units.</summary>
  public static int WorldZToCornerHeight(float worldZ) {
    if (!float.IsFinite(worldZ)) throw new ArgumentOutOfRangeException(nameof(worldZ));
    var scaled = Convert.ToDouble(worldZ) / HeightStep;
    if (scaled < int.MinValue || scaled > int.MaxValue)
      throw new ArgumentOutOfRangeException(nameof(worldZ));
    return Convert.ToInt32(Math.Round(scaled, MidpointRounding.AwayFromZero));
  }

  private static int DecodeCornerHeight(float worldZ) {
    try {
      return WorldZToCornerHeight(worldZ);
    }
    catch (ArgumentOutOfRangeException exception) {
      throw new InvalidDataException("Decoded terrain height exceeds the simulation range.", exception);
    }
  }

  private static int ClampHeight(long value, int lower = int.MinValue, int upper = int.MaxValue)
    => Convert.ToInt32(Math.Max(lower, Math.Min(upper, value)));

  private static (TerrainCornerSlot c1, TerrainCornerSlot c2, int dx, int dy) GetEdgeCornerPair(Edge edge) => edge switch {
    Edge.South => (TerrainCornerSlot.SouthWest, TerrainCornerSlot.SouthEast, 0, -1),
    Edge.West  => (TerrainCornerSlot.SouthWest, TerrainCornerSlot.NorthWest, -1, 0),
    Edge.East  => (TerrainCornerSlot.SouthEast, TerrainCornerSlot.NorthEast, 1, 0),
    Edge.North => (TerrainCornerSlot.NorthWest, TerrainCornerSlot.NorthEast, 0, 1),
    _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null),
  };

  /// <summary>
  /// Returns the corner slot on the neighboring tile across <paramref name="edge"/> that occupies the
  /// same world-space point as <paramref name="slot"/> on this tile.
  /// </summary>
  /// <remarks>
  /// East/West edges are crossed by flipping the corner's West/East (X) side; North/South edges are
  /// crossed by flipping the corner's South/North (Y) side. This is <i>not</i> the diagonally-opposite
  /// corner — e.g. across the North edge, NorthWest mirrors to SouthWest (same X side), not
  /// SouthEast.
  /// </remarks>
  private static TerrainCornerSlot MirrorAcrossEdge(TerrainCornerSlot slot, Edge edge) => (edge, slot) switch {
    (Edge.East or Edge.West, TerrainCornerSlot.SouthWest) => TerrainCornerSlot.SouthEast,
    (Edge.East or Edge.West, TerrainCornerSlot.SouthEast) => TerrainCornerSlot.SouthWest,
    (Edge.East or Edge.West, TerrainCornerSlot.NorthWest) => TerrainCornerSlot.NorthEast,
    (Edge.East or Edge.West, TerrainCornerSlot.NorthEast) => TerrainCornerSlot.NorthWest,
    (Edge.North or Edge.South, TerrainCornerSlot.SouthWest) => TerrainCornerSlot.NorthWest,
    (Edge.North or Edge.South, TerrainCornerSlot.NorthWest) => TerrainCornerSlot.SouthWest,
    (Edge.North or Edge.South, TerrainCornerSlot.SouthEast) => TerrainCornerSlot.NorthEast,
    (Edge.North or Edge.South, TerrainCornerSlot.NorthEast) => TerrainCornerSlot.SouthEast,
    _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
  };

  /// <summary>
  /// Enumerates every (tileX, tileY, slot) that owns a copy of the world-space corner that
  /// <c>(tileX, tileY).slot</c> addresses, excluding the source entry itself.
  /// </summary>
  private IEnumerable<(int x, int y, TerrainCornerSlot slot)> EnumerateSharedCorners(int tileX, int tileY, TerrainCornerSlot slot) {
    // For tile (x, y), the slot's world position is:
    //   SouthWest -> (x,   y)
    //   SouthEast -> (x+1, y)
    //   NorthWest -> (x,   y+1)
    //   NorthEast -> (x+1, y+1)
    // A neighbor shares this corner when its own tile contains that world position.
    var (wx, wy) = slot switch {
      TerrainCornerSlot.SouthWest => (tileX, tileY),
      TerrainCornerSlot.SouthEast => (tileX + 1, tileY),
      TerrainCornerSlot.NorthWest => (tileX, tileY + 1),
      TerrainCornerSlot.NorthEast => (tileX + 1, tileY + 1),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    // The four tiles that can own this corner are those with the corner's world position as one of
    // their four (SW, SE, NW, NE) world positions. Concretely, those are the (x', y') tiles where
    // x' ∈ {wx, wx - 1} and y' ∈ {wy, wy - 1} — but only those tiles that also have the corner as
    // one of their four slots. Each (x', y') can own the corner as exactly one of:
    //   (x', y')       owns the world position (x', y')     as SW
    //   (x'-1, y')     owns the world position (x', y')     as SE
    //   (x', y'-1)     owns the world position (x', y')     as NW
    //   (x'-1, y'-1)   owns the world position (x', y')     as NE
    // The source tile (tileX, tileY) is excluded.
    var candidates = new (int x, int y, TerrainCornerSlot slot)[] {
      (wx,     wy,     TerrainCornerSlot.SouthWest),
      (wx - 1, wy,     TerrainCornerSlot.SouthEast),
      (wx,     wy - 1, TerrainCornerSlot.NorthWest),
      (wx - 1, wy - 1, TerrainCornerSlot.NorthEast),
    };
    foreach (var (x, y, s) in candidates) {
      if (x == tileX && y == tileY && s == slot) continue;
      if (!HasTile(x, y)) continue;
      yield return (x, y, s);
    }
  }
}

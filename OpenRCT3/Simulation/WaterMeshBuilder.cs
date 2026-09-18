// Water Mesh Builder
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Meshes;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenRCT3.Simulation;

/// <summary>Builds the bounded, tile-clipped surface mesh for a <see cref="WaterPool"/>.</summary>
/// <remarks>
/// <para>
/// Water remains a separate overlay: this mesh does not alter terrain heights. Each
/// <see cref="WaterSurfaceTriangle"/> is clipped against the pool's flat surface using its explicit
/// submerged-vertex mask. One submerged vertex emits a triangle, two emit a quad, and three emit the
/// complete terrain triangle.
/// </para>
/// <para>
/// For an ocean, this is only the bounded in-map portion identified by <see cref="WaterPool.Tiles"/>.
/// Extending an ocean to the horizon requires camera/skybox policy that is intentionally separate
/// from this deterministic tile mesh.
/// </para>
/// </remarks>
public static class WaterMeshBuilder {
  /// <summary>
  /// Rendering-only separation above the decoded water height, preventing coplanar terrain at the
  /// shoreline from winning the depth test. The simulation pool height and clipping plane remain
  /// unchanged.
  /// </summary>
  internal const float SurfaceRenderOffset = Terrain.HeightStep;

  public static Mesh Build(
    Terrain terrain,
    WaterPool pool,
    Vector4 color,
    string? name = "Water"
  ) {
    ArgumentNullException.ThrowIfNull(terrain);
    ArgumentNullException.ThrowIfNull(pool);
    if (pool.Triangles.Count == 0)
      throw new ArgumentException("A water pool must contain at least one triangle.", nameof(pool));

    var vertices = new List<Vertex>(checked(pool.Triangles.Count * 4));
    var indices = new List<uint>(checked(pool.Triangles.Count * 6));
    var surfaceY = Terrain.CornerHeightToWorldY(pool.Height) + SurfaceRenderOffset;
    var triangleKeys = new HashSet<(int X, int Y, WaterTerrainTriangle Triangle)>();

    foreach (var triangle in pool.Triangles
      .OrderBy(triangle => triangle.Y)
      .ThenBy(triangle => triangle.X)
      .ThenBy(triangle => triangle.Triangle)) {
      ValidateTriangle(triangle);
      var key = (triangle.X, triangle.Y, triangle.Triangle);
      if (!triangleKeys.Add(key))
        throw new ArgumentException(
          $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) is duplicated.",
          nameof(pool));
      if (!terrain.HasTile(triangle.X, triangle.Y))
        throw new ArgumentOutOfRangeException(
          nameof(pool),
          $"Water tile ({triangle.X}, {triangle.Y}) is outside the terrain grid.");

      var polygon = ClipTriangle(terrain, pool.Height, triangle);
      AddPolygon(terrain, triangle, polygon, surfaceY, color, vertices, indices);
    }

    return new Mesh(vertices, indices) { Name = name };
  }

  private static List<Vector2> ClipTriangle(
    Terrain terrain,
    int waterHeight,
    WaterSurfaceTriangle triangle
  ) {
    var slots = GetTriangleSlots(triangle.Triangle);
    var clipVertices = new ClipVertex[slots.Length];
    for (var index = 0; index < slots.Length; index++) {
      var slot = slots[index];
      var height = terrain.GetCorner(triangle.X, triangle.Y, slot).Height;
      var submerged = (triangle.VertexMask & (1 << index)) != 0;
      if (submerged && height > waterHeight)
        throw new InvalidDataException(
          $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) marks a corner " +
          "above its surface as submerged.");
      if (!submerged && height < waterHeight)
        throw new InvalidDataException(
          $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) marks a corner " +
          "below its surface as dry.");

      clipVertices[index] = new ClipVertex(
        CornerPosition(terrain, triangle.X, triangle.Y, slot),
        height,
        submerged);
    }

    var polygon = new List<Vector2>(4);
    for (var index = 0; index < clipVertices.Length; index++) {
      var current = clipVertices[index];
      var next = clipVertices[(index + 1) % clipVertices.Length];
      if (current.Submerged) {
        polygon.Add(current.Position);
        if (!next.Submerged) polygon.Add(IntersectAtHeight(current, next, waterHeight));
      } else if (next.Submerged) {
        polygon.Add(IntersectAtHeight(current, next, waterHeight));
      }
    }

    // A DAT mask may classify a corner exactly on the water plane as submerged. Both crossings then
    // land on that same corner, producing repeated points. A point or line has no renderable water
    // area, so normalize those boundary-only records away instead of rejecting a valid saved map.
    var normalized = RemoveRepeatedVertices(polygon);
    if (normalized.Count < 3) return [];
    if (normalized.Count > 4)
      throw new InvalidDataException(
        $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) produced invalid " +
        "clipped geometry.");
    ValidateCounterClockwise(normalized, triangle);
    return normalized;
  }

  private static void AddPolygon(
    Terrain terrain,
    WaterSurfaceTriangle triangle,
    IReadOnlyList<Vector2> polygon,
    float surfaceY,
    Vector4 color,
    List<Vertex> vertices,
    List<uint> indices
  ) {
    var west = terrain.Origin.X + (triangle.X * terrain.TileSize.X);
    var south = terrain.Origin.Y + (triangle.Y * terrain.TileSize.Y);
    var normal = Vector3.UnitY;
    var baseIndex = Convert.ToUInt32(vertices.Count);
    if (polygon.Count == 0) return;

    foreach (var point in polygon) {
      vertices.Add(new Vertex {
        Position = new Vector3(point.X, surfaceY, point.Y),
        Normal = normal,
        TexCoord = new Vector2(
          (point.X - west) / terrain.TileSize.X,
          (point.Y - south) / terrain.TileSize.Y),
        Color = color
      });
    }

    for (var index = 1; index < polygon.Count - 1; index++) {
      indices.Add(baseIndex);
      indices.Add(baseIndex + Convert.ToUInt32(index + 1));
      indices.Add(baseIndex + Convert.ToUInt32(index));
    }
  }

  private static TerrainCornerSlot[] GetTriangleSlots(WaterTerrainTriangle triangle) => triangle switch {
    WaterTerrainTriangle.SouthWest => [
      TerrainCornerSlot.SouthWest,
      TerrainCornerSlot.SouthEast,
      TerrainCornerSlot.NorthWest,
    ],
    WaterTerrainTriangle.NorthEast => [
      TerrainCornerSlot.NorthEast,
      TerrainCornerSlot.NorthWest,
      TerrainCornerSlot.SouthEast,
    ],
    _ => throw new ArgumentOutOfRangeException(nameof(triangle), triangle, null),
  };

  private static Vector2 CornerPosition(
    Terrain terrain,
    int tileX,
    int tileY,
    TerrainCornerSlot slot
  ) {
    var (dx, dy) = slot switch {
      TerrainCornerSlot.SouthWest => (0, 0),
      TerrainCornerSlot.SouthEast => (1, 0),
      TerrainCornerSlot.NorthWest => (0, 1),
      TerrainCornerSlot.NorthEast => (1, 1),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
    return new Vector2(
      terrain.Origin.X + ((tileX + dx) * terrain.TileSize.X),
      terrain.Origin.Y + ((tileY + dy) * terrain.TileSize.Y));
  }

  private static Vector2 IntersectAtHeight(ClipVertex from, ClipVertex to, int waterHeight) {
    var heightDelta = Convert.ToDouble(to.Height) - from.Height;
    if (heightDelta == 0d) {
      if (from.Height != waterHeight)
        throw new InvalidDataException("A water boundary edge does not cross the water height.");
      return from.Submerged ? from.Position : to.Position;
    }

    var amount = (Convert.ToDouble(waterHeight) - from.Height) / heightDelta;
    if (!double.IsFinite(amount) || amount < 0d || amount > 1d)
      throw new InvalidDataException("A water boundary edge does not cross the water height.");
    return Vector2.Lerp(from.Position, to.Position, Convert.ToSingle(amount));
  }

  private static List<Vector2> RemoveRepeatedVertices(IReadOnlyList<Vector2> polygon) {
    var normalized = new List<Vector2>(polygon.Count);
    foreach (var point in polygon) {
      if (normalized.Count == 0 || normalized[^1] != point) normalized.Add(point);
    }
    if (normalized.Count > 1 && normalized[0] == normalized[^1])
      normalized.RemoveAt(normalized.Count - 1);
    return normalized;
  }

  private static void ValidateCounterClockwise(
    IReadOnlyList<Vector2> polygon,
    WaterSurfaceTriangle triangle
  ) {
    var twiceArea = 0d;
    for (var index = 0; index < polygon.Count; index++) {
      var current = polygon[index];
      var next = polygon[(index + 1) % polygon.Count];
      twiceArea += (Convert.ToDouble(current.X) * next.Y) -
        (Convert.ToDouble(next.X) * current.Y);
    }
    if (twiceArea <= 0d)
      throw new InvalidDataException(
        $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) is not " +
        "counter-clockwise after clipping.");
  }

  private static void ValidateTriangle(WaterSurfaceTriangle triangle) {
    if (!Enum.IsDefined(typeof(WaterTerrainTriangle), triangle.Triangle))
      throw new ArgumentException("A water triangle has an invalid triangle index.", nameof(triangle));
    if (triangle.VertexMask is < 1 or > 7)
      throw new ArgumentException("A water triangle has an invalid vertex mask.", nameof(triangle));
  }

  private readonly record struct ClipVertex(Vector2 Position, int Height, bool Submerged);
}

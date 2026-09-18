// Represents the game world: the current park, terrain, objects, and people.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Meshes;
using OpenCobra.GDK.Streaming;
using OpenRCT3.OpenGL;
using OpenRCT3.Scenario;
using Silk.NET.Input;
using System.Drawing;
using System.Numerics;
using GDK = OpenCobra.GDK;

#if WINDOWS
using System.Windows.Forms;
#elif MACOS
using OpenRCT3.Platforms.macOS;
#endif

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World : GDK.Game.World, IParkLoader {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }
  /// <summary>The first current terrain batch, exposed for live debug statistics.</summary>
  internal Mesh? TerrainMesh { get; private set; }
  /// <summary>
  /// World-space center of the rotation-marker cube (see <see cref="Load"/>) - exposed so per-frame code
  /// (e.g. <c>Game.Run</c>'s <c>ImDraw.Axis</c> proof of concept) can reference the marker's position
  /// without recomputing it.
  /// </summary>
  public Vector3 MarkerCenter { get; private set; }

  /// <summary>
  /// The scenario editor and park chooser windows, created once and wired to the persistent loader
  /// system for opening a different park later.
  /// </summary>
  private Editor? editor;
  private ParkChooser? parkChooser;
  private ParkLoadSystem? parkLoadSystem;

  // FIXME: Load() blocks until every task completes since callers (e.g. Game's constructor) dereference
  // Terrain/Park synchronously right after calling it. Progress.MeasureTasks runs tasks on a background
  // Task.Run and returns immediately without waiting; without this .Wait(), Terrain/Park may still be
  // null when the caller reads them. Revisit once a progress bar actually consumes Progress
  // asynchronously (see the TODO in Game.cs) instead of blocking here.
  /// <summary>Loads the default flat park and builds the scene.</summary>
  public override void Load() => Load(parkPath: null);

  /// <summary>Loads a park from the given path (or the default park if path is null) and builds the scene.</summary>
  /// <remarks>
  /// Creates and registers one <see cref="ParkLoadSystem"/> for subsequent park load requests through
  /// the Early-phase systems pipeline.
  /// </remarks>
  /// <param name="parkPath">Path to the park save file, or null to load the default park.</param>
  public void Load(string? parkPath) {
    var previousTerrain = Terrain;
    var measurement = Progress.MeasureTasks([
      new(() => {
        var terrain = Terrain.Load(out var waterManager, parkPath);
        try {
          var park = new Park(terrain);
          if (waterManager != null) WaterManagerLoader.Load(park, terrain, waterManager);
          Terrain = terrain;
          Park = park;
        } catch {
          terrain.TextureCatalog?.Dispose();
          throw;
        }
      }, "Loading park terrain"),
    ]);
    Progress = measurement.Progress;
    measurement.Task.Wait();
    var loader = parkLoadSystem ??= new ParkLoadSystem();
    AddSystem(loader);
    BuildScene(loader);
    if (!ReferenceEquals(previousTerrain?.TextureCatalog, Terrain?.TextureCatalog))
      previousTerrain?.TextureCatalog?.Dispose();
  }

  /// <summary>Builds the terrain mesh, rotation-marker cube, camera framing, and windows for <see cref="Game.Scene"/>.</summary>
  /// <remarks>
  /// <para>
  /// The first call wires <see cref="ParkChooser.ParkSelected"/> to
  /// <see cref="ParkLoadSystem.RequestLoad"/> so later park selection runs during the Early phase.
  /// </para>
  /// <para>
  /// Opening a different park afterward is handled by <paramref name="parkLoadSystem"/>, which
  /// requests a full park load through the systems pipeline instead of blocking the render pass.
  /// </para>
  /// </remarks>
  private void BuildScene(ParkLoadSystem parkLoadSystem) {
    var game = Game.Instance!;
    var scene = game.Scene;

    TerrainMesh = null;
    foreach (var model in scene.Models) model.Dispose();
    scene.Models.Clear();
    // Build texture-batched meshes from the loaded terrain's corner-height grid. Each DAT cell's
    // decoded surface/cliff indices select the matching texture from the terrain catalog.
    Debug.Assert(Terrain != null);
    Debug.Assert(Terrain.TextureCatalog != null);
    foreach (var batch in TerrainMeshBuilder.BuildBatches(Terrain, Vector4.One)) {
      var texture = batch.Kind switch {
        TerrainMaterialKind.Surface => Terrain.TextureCatalog.GetSurface(batch.Index),
        TerrainMaterialKind.Cliff => Terrain.TextureCatalog.GetCliff(batch.Index),
        _ => throw new ArgumentOutOfRangeException(nameof(batch.Kind), batch.Kind, null),
      };
      var terrainModel = new Model(batch.Mesh) {
        Material = new Textured { AlbedoTexture = texture }
      };
      scene.Models.Add(terrainModel);
      TerrainMesh ??= terrainModel.Mesh;
    }
    logger.Debug("Added terrain meshes");

    // Water is a separate overlay over the terrain. Each decoded DAT WaterManager pool keeps its
    // exact triangle masks and surface height while rendering independently from the terrain mesh.
    Debug.Assert(Park != null);
    foreach (var pool in Park.WaterPools) {
      var waterModel = new Model(WaterMeshBuilder.Build(
        Terrain,
        pool,
        new Vector4(0.12f, 0.42f, 0.72f, 1f))) {
        Material = new Flat()
      };
      scene.Models.Add(waterModel);
    }
    logger.Debug("Added {Count} water meshes", Park.WaterPools.Count);

    // Frame the camera on the loaded terrain's full 3D bounds. Camera's default framing (a small
    // fixed offset from the origin) only suits a toy scene; it doesn't scale to an actual map, so
    // most or all of the terrain otherwise ends up outside the view frustum.
    //
    // TerrainCameraFraming includes the OOB border and scans the real corner-height range. Centering
    // on XYZ keeps elevated maps aimed correctly, while the full 3D diagonal bounds the 45°-azimuth
    // "diamond" without the old buildable-area-only 1.8x heuristic (see CameraFramingTests).
    var framing = TerrainCameraFraming.Calculate(Terrain);
    scene.Camera.MaxDistance = framing.Distance;
    scene.Camera.Frame(framing.Target, framing.Distance);
    logger.Trace("Framed camera on terrain");

    // Proof-of-concept marker: a unit cube placed off-center in one quadrant of the buildable area, so
    // Q/E map rotation (above) is visually obvious - a centered object wouldn't appear to move at all.
    Debug.Assert(Park != null);
    var (boundsMin, boundsMax) = Park.BuildableBounds;
    var markerPosition = new Vector3(
      boundsMin.X + (boundsMax.X - boundsMin.X) * 0.75f,
      1f,
      boundsMin.Y + (boundsMax.Y - boundsMin.Y) * 0.75f);
    MarkerCenter = markerPosition + new Vector3(0, 0.5f, 0);
    var marker = new Model(Primitives.Cube(name: "RotationMarker", color: Color.FromArgb(200, 30, 30).ToGl())) {
      Material = new Flat(),
      Transform = new Transform { Matrix = Matrix4x4.CreateTranslation(markerPosition) }
    };
    scene.Models.Add(marker);
    logger.Trace("Added rotation marker cube");

    if (editor != null) return;
    // Add the scenario editor and park chooser windows.
    editor = new Editor();
    editor.Exit += () => {
      game.Quit();

      // Exit the game
      // TODO: This doesn't belong here; move it to a new platform-agnostic abstraction
#if WINDOWS
      Application.Exit();
#elif MACOS
      if (NSApplication.SharedApplication.Delegate is AppDelegate app)
        app.Exit();
#endif
    };
    if (GamePresentationOptions.ShowUserInterface) scene.Windows.Add(editor);

    parkChooser = new ParkChooser();
    editor.OpenPark += parkChooser.Show;
    parkChooser.ParkSelected += parkLoadSystem.RequestLoad;
    if (GamePresentationOptions.ShowUserInterface) scene.Windows.Add(parkChooser);

    // Made.Of statically checks Debug's constructor at compile time (rather than reflection-based
    // Parameters.Of). Debug reads the current terrain batch from Game.World each frame, so reloads
    // cannot leave it holding a mesh that scene replacement already disposed.
    Game.IoC.Register(Made.Of(() => new UI.Debug(
      Arg.Of<Game>(),
      Arg.Of<GDK.Platform.IWindow>(),
      Arg.Of<IInputContext>())));
    if (GamePresentationOptions.ShowUserInterface) scene.Windows.Add(Game.IoC.Resolve<UI.Debug>());
  }

  protected override void Dispose(bool disposing) {
    if (disposing) {
      Terrain?.TextureCatalog?.Dispose();
    }

    Terrain = null;
    Park = null;
    TerrainMesh = null;
    base.Dispose(disposing);
  }
}

/// <summary>
/// System that manages asynchronous park loading requests, ensuring they execute before rendering each tick.
/// </summary>
/// <remarks>
/// <para>
/// Runs in Early phase (before Render), dequeuing pending park load requests and calling
/// <see cref="IParkLoader.Load(string?)"/> atomically via <see cref="SafeWeakReference{T}.TryGetTarget"/>.
/// This fixes the UI-thread reentrancy bug where <see cref="ParkChooser.ParkSelected"/> calling
/// <see cref="World.Load(string?)"/> directly would block the render loop.
/// </para>
/// <para>
/// Load requests are stored in <see cref="pendingParkPath"/> using <see cref="Interlocked.Exchange"/>
/// for thread-safe last-write-wins semantics: if two threads call <see cref="RequestLoad"/> before the
/// next update, only the final requested path loads.
/// </para>
/// </remarks>
internal class ParkLoadSystem : GDK.Game.System {
  private GDK.Threading.SafeWeakReference<IParkLoader>? world;
  /// <remarks>
  /// Stores the next park path to load. Uses <see cref="Interlocked.Exchange"/> for thread-safe
  /// updates and a sentinel value ("_NO_LOAD_") to distinguish "no pending load" from "load default park" (null).
  /// </remarks>
  private string? pendingParkPath = "_NO_LOAD_";

  internal ParkLoadSystem() : base(GDK.Game.PipelinePhase.Early) { }

  /// <remarks>
  /// Wraps the world in a <see cref="SafeWeakReference{T}"/> to enforce atomic <see cref="SafeWeakReference{T}.TryGetTarget"/>
  /// dereference in <see cref="Update"/>, preventing the race condition where <see cref="System.IsRunning"/> is checked
  /// separately and then the world is dereferenced later (following production game engine patterns).
  /// </remarks>
  public override void Attach(WeakReference<GDK.Game.IWorld> worldRef) {
    if (worldRef.TryGetTarget(out var w) && w is IParkLoader parkLoader) {
      world = new GDK.Threading.SafeWeakReference<IParkLoader>(parkLoader);
    }
  }

  /// <summary>
  /// Request a park load for the next update.
  /// </summary>
  /// <remarks>
  /// If called multiple times before the next update, only the last requested path will be loaded (last-write-wins).
  /// </remarks>
  /// <param name="parkPath">Path to the park save file, or null to load the default park.</param>
  public void RequestLoad(string? parkPath) {
    Interlocked.Exchange(ref pendingParkPath, parkPath);
  }

  /// <remarks>
  /// Atomically dequeues the pending park path via <see cref="Interlocked.Exchange"/>, then calls
  /// <see cref="IParkLoader.Load(string?)"/> if a load was pending and the world is still alive.
  /// The sentinel value "_NO_LOAD_" distinguishes "no pending load" from "load default park" (null path).
  /// Multiple <see cref="RequestLoad"/> calls before this update run only the final requested path (last-write-wins).
  /// </remarks>
  public override void Update(TimeSpan delta) {
    base.Update(delta);

    var path = Interlocked.Exchange(ref pendingParkPath, "_NO_LOAD_");
    if (path != "_NO_LOAD_" && world?.TryGetTarget(out var parkLoader) == true)
      parkLoader?.Load(path);
  }
}

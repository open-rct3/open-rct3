// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
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
#elif OSX
using AppKit;
#endif

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World : GDK.Game.World {
  /// <summary>
  /// <see cref="IGame.IoC"/> service key the terrain <see cref="Mesh"/> is registered under - keyed
  /// rather than by bare <see cref="Mesh"/> type so a later feature registering some other
  /// <see cref="Mesh"/> instance can't collide with (or be shadowed by) this one.
  /// </summary>
  private const string TerrainMeshServiceKey = "Terrain";
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }
  /// <summary>
  /// World-space center of the rotation-marker cube (see <see cref="Load"/>) - exposed so per-frame code
  /// (e.g. <c>Game.Run</c>'s <c>ImDraw.Axis</c> proof of concept) can reference the marker's position
  /// without recomputing it.
  /// </summary>
  public Vector3 MarkerCenter { get; private set; }

  // FIXME: Load() blocks until every task completes since callers (e.g. Game's constructor) dereference
  // Terrain/Park synchronously right after calling it. Progress.MeasureTasks runs tasks on a background
  // Task.Run and returns immediately without waiting; without this .Wait(), Terrain/Park may still be
  // null when the caller reads them. Revisit once a progress bar actually consumes Progress
  // asynchronously (see the TODO in Game.cs) instead of blocking here.
  public override void Load() {
    var measurement = Progress.MeasureTasks([
      new(() => Park = new Park(), "Loading park"),
      new(() => Terrain = Terrain.Load(), "Loading terrain"),
      new(BuildScene, "Creating park"),
    ]);
    Progress = measurement.Progress;
    measurement.Task.Wait();
  }

  /// <summary>
  /// Builds the initial <see cref="OpenRCT3.Game.Scene"/> content from the just-loaded
  /// <see cref="Terrain"/>/<see cref="Park"/>: the terrain mesh, a placeholder rotation-marker cube,
  /// camera framing, and the scenario editor/debug windows. Extracted out of <see cref="Game"/>'s
  /// constructor (which was otherwise doing scene-building work well beyond "load the world") so
  /// <see cref="Load"/> is the single place that turns loaded simulation data into what the renderer
  /// actually draws.
  /// </summary>
  private void BuildScene() {
    var game = Game.Instance!;
    var scene = game.Scene;

    // Build a mesh from the loaded terrain's corner-height grid (solid-colored prototype;
    // surface painting isn't wired up yet)
    var grass = Color.FromArgb(79, 129, 14).ToGl();
    Debug.Assert(Terrain != null);
    var hasGrassTexture = Terrain.GrassTexture != null;
    var terrainMesh = TerrainMeshBuilder.Build(Terrain, hasGrassTexture ? Color.White.ToGl() : grass);
    Game.IoC.RegisterInstance(terrainMesh, serviceKey: TerrainMeshServiceKey);
    var ground = new Model(terrainMesh) {
      Material = hasGrassTexture
        ? new Textured { AlbedoTexture = Terrain.GrassTexture }
        : new Flat()
    };
    scene.Models.Add(ground);
    logger.Trace("Added terrain mesh");

    // Proof-of-concept marker: a unit cube placed off-center in one quadrant of the buildable area, so
    // Q/E map rotation (above) is visually obvious - a centered object wouldn't appear to move at all.
    Debug.Assert(Park != null);
    var (boundsMin, boundsMax) = Park.BuildableBounds;
    var markerPosition = new Vector3(
      boundsMin.X + (boundsMax.X - boundsMin.X) * 0.75f,
      boundsMin.Y + (boundsMax.Y - boundsMin.Y) * 0.75f,
      1f);
    MarkerCenter = markerPosition + new Vector3(0, 0, 0.5f);
    var marker = new Model(Primitives.Cube(name: "RotationMarker", color: Color.FromArgb(200, 30, 30).ToGl())) {
      Material = new Flat(),
      Transform = new Transform { Matrix = Matrix4x4.CreateTranslation(markerPosition) }
    };
    scene.Models.Add(marker);
    logger.Trace("Added rotation marker cube");

    // "Fully zoomed out" distance framing the whole park - bounds Zoom and sizes the far clip plane
    // (Camera.FarPlaneReferenceDistance) even though default framing below targets the marker cube.
    // Margin compensates for Camera's fixed 45° azimuth foreshortening the near corner; picked empirically.
    const float FramingDistanceMargin = 1.25f;
    var bounds = Park.BuildableBounds;
    var parkDiagonal = Vector2.Distance(bounds.Min, bounds.Max);
    var maxFramingDistance = parkDiagonal * FramingDistanceMargin;
    scene.Camera.MaxDistance = maxFramingDistance;

    // Default framing targets the marker cube (currently the only placed object worth focusing on)
    // rather than the whole park. Primitives.Cube spans -1..1 on each local axis (corner-to-corner
    // diagonal 2*sqrt(3)); the same margin as the whole-park framing keeps every corner on-screen.
    var markerDiagonal = 2f * MathF.Sqrt(3);
    var markerFramingDistance = markerDiagonal * FramingDistanceMargin;
    scene.Camera.Frame(MarkerCenter, markerFramingDistance);
    logger.Trace("Framed camera on marker cube");

    // Add the scenario editor and debug windows
    var editor = new Editor();
    editor.Exit += () => {
      game.Quit();
      // TODO: Make this cross-platform
      Application.Exit();
    };
    scene.Windows.Add(editor);

    // Made.Of statically checks Debug's constructor at compile time (rather than reflection-based
    // Parameters.Of), matching the IInputContext/GUI.Controller registrations above - Game and the
    // terrain Mesh are resolved from the instances just registered, PlatformWindow/IInputContext from
    // the registrations GameWindow.cs/GLSurface.cs already made.
    Game.IoC.Register(Made.Of(() => new UI.Debug(
      Arg.Of<Game>(),
      Arg.Of<Mesh>(TerrainMeshServiceKey),
      Arg.Of<GDK.Platform.IWindow>(),
      Arg.Of<IInputContext>())));
    scene.Windows.Add(Game.IoC.Resolve<UI.Debug>());
  }

  protected virtual void Dispose(bool disposing) {
    if (disposing) {
      Terrain?.GrassTexture?.Dispose();
    }

    Terrain = null;
    Park = null;
    base.Dispose(disposing);
  }
}

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
using System.Numerics;
using Drawing = System.Drawing;
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
  /// <summary>
  /// <see cref="IGame.IoC"/> service key the terrain <see cref="Mesh"/> is registered under - keyed
  /// rather than by bare <see cref="Mesh"/> type so a later feature registering some other
  /// <see cref="Mesh"/> instance can't collide with (or be shadowed by) this one.
  /// </summary>
  private const string ServiceKey = "Terrain";

  public Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }
  /// <summary>
  /// World-space center of the rotation-marker cube (see <see cref="Load"/>) - exposed so per-frame code
  /// (e.g. <c>Game.Run</c>'s <c>ImDraw.Axis</c> proof of concept) can reference the marker's position
  /// without recomputing it.
  /// </summary>
  public Vector3 MarkerCenter { get; private set; }

  /// <summary>
  /// The scenario editor and park chooser windows, created once on <see cref="BuildScene"/> (called
  /// once, from <see cref="Load"/>) and wired to <see cref="ReplaceTerrain"/> for opening a different
  /// park later.
  /// </summary>
  public UI.Debug? Debug { get; private set; }
  public ParkChooser? ParkChooser => parkChooser;

  private Editor? editor;
  private ParkChooser? parkChooser;
  private Model? rotationMarker;
  private Model? currentGroundModel;

  /// <summary>Loads the default flat park and builds the scene.</summary>
  public override void Load() => Load(parkPath: null);

  /// <summary>Loads a park from the given path (or the default park if path is null).</summary>
  /// <param name="parkPath">Path to the park save file, or null to load the default park.</param>
  public void Load(string? parkPath) {
    var measurement = Progress.MeasureTasks([
      new(() => Park = Park.Load(parkPath), "Loading park"),
      new(() => Terrain = Terrain.Load(parkPath), "Loading terrain"),
      new(InitializeScene, "Creating park"),
    ]);
    Progress = measurement.Progress;
    measurement.Task.Wait();
  }

  /// <summary>Initializes or updates the scene models, camera framing, and windows.</summary>
  private void InitializeScene() {
    var game = Game.Instance;
    if (game == null) return;
    var scene = game.Scene;

    System.Diagnostics.Debug.Assert(Terrain != null);
    System.Diagnostics.Debug.Assert(Park != null);

    Game.IoC.RegisterInstance(Terrain.GroundModel.Mesh, serviceKey: ServiceKey, ifAlreadyRegistered: IfAlreadyRegistered.Replace);

    if (currentGroundModel != null)
      scene.Models.Remove(currentGroundModel);
    currentGroundModel = Terrain.GroundModel;
    scene.Models.Add(currentGroundModel);

    var (boundsMin, boundsMax) = Park.BuildableBounds;
    var markerPosition = new Vector3(
      boundsMin.X + (boundsMax.X - boundsMin.X) * 0.75f,
      1f,
      boundsMin.Y + (boundsMax.Y - boundsMin.Y) * 0.75f);
    MarkerCenter = markerPosition + new Vector3(0, 0.5f, 0);

    if (rotationMarker == null) {
      rotationMarker = new Model(Primitives.Cube(name: "RotationMarker", color: Drawing.Color.FromArgb(200, 30, 30).ToGl())) {
        Material = new Flat(),
        Transform = new Transform { Matrix = Matrix4x4.CreateTranslation(markerPosition) }
      };
      scene.Models.Add(rotationMarker);
    } else {
      rotationMarker.Transform.Matrix = Matrix4x4.CreateTranslation(markerPosition);
    }

    const float FramingDistanceMargin = 1.25f;
    var (Min, Max) = Park.BuildableBounds;
    var parkDiagonal = Vector2.Distance(Min, Max);
    var maxFramingDistance = parkDiagonal * FramingDistanceMargin;
    scene.Camera.MaxDistance = maxFramingDistance;

    var markerDiagonal = 2f * MathF.Sqrt(3);
    var markerFramingDistance = markerDiagonal * FramingDistanceMargin;
    scene.Camera.Frame(MarkerCenter, markerFramingDistance);

    if (editor == null) {
      editor = new Editor();
      editor.Exit += () => {
        game.Quit();
#if WINDOWS
        Application.Exit();
#elif MACOS
        if (NSApplication.SharedApplication.Delegate is AppDelegate app)
          app.Exit();
#endif
      };
      scene.Windows.Add(editor);
    }

    if (parkChooser == null) {
      parkChooser = new ParkChooser();
      editor.OpenPark += parkChooser.Show;
      parkChooser.ParkSelected += path => Load(path);
      scene.Windows.Add(parkChooser);
    }

    if (Debug != null) return;
    Debug = new UI.Debug(game);
    Game.IoC.RegisterInstance(Debug, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
    scene.Windows.Add(Debug);
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

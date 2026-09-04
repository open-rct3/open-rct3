// Debugging Telemetry Window
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using DryIoc;
using Hexa.NET.ImGui;
using OpenCobra.GDK;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Meshes;
using OpenRCT3.Debug;
using OpenRCT3.Simulation;
using Silk.NET.Input;
using PlatformWindow = OpenCobra.GDK.Platform.IWindow;

namespace OpenRCT3.UI;

/// <summary>
/// Developer-only diagnostics window for runtime rendering stats and toggles, to isolate which
/// setting is responsible for a given visual bug.
/// </summary>
/// <remarks>
/// <paramref name="window"/> and <paramref name="inputContext"/> default to resolving from
/// <see cref="Game.IoC"/>'s existing registrations (see <c>GameWindow.cs</c> and <c>GLSurface.cs</c>),
/// so this window never has to reach back into the container itself at render time.
/// </remarks>
public class Debug(Game game, PlatformWindow window, IInputContext inputContext) : IWindow {
  public Debug(Game game) : this(
    game,
    Game.IoC.Resolve<PlatformWindow>(),
    Game.IoC.Resolve<IInputContext>()
  ) {}

  private static readonly uint PlotColor = Color.FromRgb(0x4CAF50).ToUint();
  private static readonly uint PlotBgColor = Color.FromRgba(76, 175, 80, Convert.ToByte(255 * 0.35f)).ToUint();

  public bool Open { get; private set; } = true;

  private readonly FrameRateAccumulator frameRate = new();
  private readonly RollingPlot framePlot = new(
    capacity: 120,
    lineColor: PlotColor,
    fillColor: PlotBgColor,
    targetScale: 33.33f,
    thickness: 1.5f
  );
  private Mesh? TerrainMesh => game.World.Terrain?.GroundModel.Mesh;

  /// <summary>Gets the cursor terrain position or UI state description.</summary>
  private string CursorPosition {
    get {
      // Skip picking while the mouse is over an ImGui window (including this one) - IMouse.Position still
      // reports a screen coordinate in that case, and TryPickTile would happily report a bogus pick for
      // whatever's behind the panel.
      if (ImGui.GetIO().WantCaptureMouse) return "(UI)";

      var mouse = inputContext.Mice[0];
      var camera = game.Scene.Camera;
      var terrain = game.World.Terrain;

      if (terrain == null) return "none";

      var ray = camera.ToRay(mouse.Position, window.FramebufferSize);
      var pick = TerrainPicker.TryPickTile(ray, terrain, StepBudget(camera));

      return pick is { } hit
        ? $"Terrain at ({hit.Point.X:0.00}, {hit.Point.Y:0.00}, {hit.Point.Z:0.00})"
        : "none";
    }
  }

  /// <summary>
  /// The step budget for the cursor-position ray march - derived per-frame from <see cref="Camera.MaxDistance"/>,
  /// falling back to the live eye-to-target distance (mirroring the fallback <see cref="Camera"/> itself
  /// uses for its far clip plane) when unset, e.g. before <c>Game.cs</c> has framed a park.
  /// </summary>
  private static int StepBudget(Camera camera)
    => (int)MathF.Ceiling((camera.MaxDistance ?? Vector3.Distance(camera.Eye, camera.Target)) / Park.TileSize);


  public void Render() {
    if (!Open) return;

    // TODO: Extract this workspace and no-resize/no-move/no-close stuff to a helper method in a OpenRCT3.UI.ImGui class
    // Pin to the top-right corner of the work area (excludes menu-bars/task-bars, if any). Anchored
    // via the (1, 0) pivot - SetNextWindowPos's pos becomes that corner of the window, not its
    // top-left - so this stays pinned regardless of the window's auto-resized content each frame.
    var viewport = ImGui.GetMainViewport();
    var windowPos = new Vector2(
      viewport.WorkPos.X + viewport.WorkSize.X - Gui.Padding,
      viewport.WorkPos.Y + Gui.Padding
    );
    ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new Vector2(1f, 0f));
    ImGui.Begin("Debug", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

    var delta = game.FrameTime;
    frameRate.RecordFrame(delta);
    framePlot.Push((float)delta.TotalMilliseconds);

    // Frame timing statistics
    ImGui.Text($"Frame: {frameRate.CurrentFps:0} fps ({frameRate.CurrentFrameTimeMs:0.00}ms)");
    var availWidth = Math.Max(Convert.ToInt32(ImGui.GetContentRegionAvail().X), 150);
    framePlot.Render(BoxConstraints.TightFor(availWidth));
    if (framePlot.Summary is { } stats)
      ImGui.TextDisabled($"{stats.Min:0.0}/{stats.Max:0.0}ms  avg: {stats.Average:0.0}ms  dev: {stats.StandardDeviation:0.0}ms");

    // Terrain statistics
    var mesh = TerrainMesh;
    var faces = mesh != null ? mesh.Indices.Count / 3 : 0;
    var vertices = mesh != null ? mesh.Vertices.Count : 0;
    ImGui.Text($"Terrain: {faces} faces, {vertices} vertices");
    ImGui.Text($"Cursor: {CursorPosition}");

    ImGui.End();
  }
}

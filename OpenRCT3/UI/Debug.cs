// Debug Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using Hexa.NET.ImGui;
using OpenCobra.GDK;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Meshes;
using OpenRCT3.Simulation;
using Silk.NET.Input;
using static OpenRCT3.UI.Gui;
using PlatformWindow = OpenCobra.GDK.Platform.IWindow;

namespace OpenRCT3.UI;

/// <summary>
/// Developer-only diagnostics window for runtime rendering stats and toggles, to isolate which
/// setting is responsible for a given visual bug.
/// </summary>
/// <remarks>
/// Constructed via <see cref="Game.IoC"/> (see <c>Game.cs</c>'s <c>Made.Of</c> registration) rather than
/// <c>new</c> - <paramref name="window"/> and <paramref name="inputContext"/> are resolved from the
/// container's existing registrations (see <c>GameWindow.cs</c>/<c>GLSurface.cs</c>), so this window
/// never has to reach back into the container itself at render time.
/// </remarks>
public class Debug(Game game, Mesh terrainMesh, PlatformWindow window, IInputContext inputContext) : IWindow {
  /// <summary>
  /// The step budget for the cursor-position ray march - derived per-frame from <see cref="Camera.MaxDistance"/>,
  /// falling back to the live eye-to-target distance (mirroring the fallback <see cref="Camera"/> itself
  /// uses for its far clip plane) when unset, e.g. before <c>Game.cs</c> has framed a park.
  /// </summary>
  private static int StepBudget(Camera camera)
    => (int)MathF.Ceiling((camera.MaxDistance ?? Vector3.Distance(camera.Eye, camera.Target)) / Park.TileSize);

  public bool Open { get; private set; } = true;

  public void Render() {
    if (!Open) return;

    // TODO: Extract this workspace and no-resize/no-move/no-close stuff to a helper method in a OpenRCT3.UI.ImGui class
    // Pin to the top-right corner of the work area (excludes menu-bars/task-bars, if any). Anchored
    // via the (1, 0) pivot - SetNextWindowPos's pos becomes that corner of the window, not its
    // top-left - so this stays pinned regardless of the window's auto-resized content each frame.
    var viewport = ImGui.GetMainViewport();
    var windowPos = new Vector2(viewport.WorkPos.X + viewport.WorkSize.X - Padding, viewport.WorkPos.Y + Padding);
    ImGui.SetNextWindowPos(windowPos, ImGuiCond.Always, new Vector2(1f, 0f));
    ImGui.Begin("Debug", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);

    var frameSeconds = game.FrameTime.TotalSeconds;
    var fps = frameSeconds > 0 ? 1.0 / frameSeconds : 0;
    ImGui.Text($"Frame: {fps:0} fps ({game.FrameTime.TotalMilliseconds:0.00}ms)");
    ImGui.Text($"Terrain: {terrainMesh.Indices.Count / 3} faces, {terrainMesh.Vertices.Count} vertices");

    RenderCursorPosition();

    // TODO: Render a graph of frame times

    ImGui.End();
  }

  /// <summary>
  /// Renders a live "Cursor: {kind} at (x, y, z)" line, re-running <see cref="Camera.Unproject"/> and
  /// <see cref="TerrainPicker.TryPickTile"/> once per frame against the current mouse position - purely
  /// for display. This is a separate, independent pick from whatever tool input wiring later dispatches
  /// clicks (see the "raise-lower-smoothing-tools.md" plan); a debug overlay shouldn't add a hidden
  /// dependency between the two.
  /// </summary>
  private void RenderCursorPosition() {
    // Skip picking while the mouse is over an ImGui window (including this one) - IMouse.Position still
    // reports a screen coordinate in that case, and TryPickTile would happily report a bogus pick for
    // whatever's behind the panel.
    if (ImGui.GetIO().WantCaptureMouse) {
      ImGui.Text("Cursor: (UI)");
      return;
    }

    var mouse = inputContext.Mice[0];
    var camera = game.Scene.Camera;
    var terrain = game.World.Terrain;

    if (terrain == null) {
      ImGui.Text("Cursor: none");
      return;
    }

    var ray = camera.ToRay(mouse.Position, window.FramebufferSize);
    var pick = TerrainPicker.TryPickTile(ray, terrain, StepBudget(camera));

    ImGui.Text(pick is { } hit
      ? $"Cursor: Terrain at ({hit.Point.X:0.00}, {hit.Point.Y:0.00}, {hit.Point.Z:0.00})"
      : "Cursor: none");
  }
}

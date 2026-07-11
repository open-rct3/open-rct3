// Debug Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Meshes;
using static OpenRCT3.UI.Gui;

namespace OpenRCT3.UI;

/// <summary>
/// Developer-only diagnostics window for runtime rendering stats and toggles, to isolate which
/// setting is responsible for a given visual bug.
/// </summary>
public class Debug(Game game, Mesh terrainMesh) : IWindow {
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

    // TODO: Render a graph of frame times

    ImGui.End();
  }
}

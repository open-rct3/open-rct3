// Debug Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Meshes;

namespace OpenRCT3.UI;

/// <summary>
/// Developer-only diagnostics window for runtime rendering stats and toggles, to isolate which
/// setting is responsible for a given visual bug.
/// </summary>
public class Debug(Game game, Mesh terrainMesh) : IWindow {
  public bool Open { get; private set; } = true;

  public void Render() {
    if (!Open) return;

    var open = Open;
    ImGui.Begin("Debug", ref open);

    var frameSeconds = game.FrameTime.TotalSeconds;
    var fps = frameSeconds > 0 ? 1.0 / frameSeconds : 0;
    ImGui.Text($"Frame: {fps:0} fps ({game.FrameTime.TotalMilliseconds:0.00}ms)");
    ImGui.Text($"Terrain: {terrainMesh.Indices.Count / 3} faces, {terrainMesh.Vertices.Count} vertices");

    ImGui.End();
    if (open != Open) Open = open;
  }
}

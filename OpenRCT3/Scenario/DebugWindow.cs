// Debug Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;

namespace OpenRCT3.Scenario;

/// <summary>
/// Developer-only diagnostics window for toggling rendering settings at runtime, to isolate
/// which setting is responsible for a given visual bug. Currently empty - add checkboxes/sliders
/// here as needed.
/// </summary>
public class DebugWindow : IWindow {
  public bool Open { get; private set; } = true;

  public void Render() {
    if (!Open) return;

    var open = Open;
    ImGui.Begin("Debug", ref open);
    ImGui.End();
    if (open != Open) Open = open;
  }
}

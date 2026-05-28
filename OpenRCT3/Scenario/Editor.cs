// Scenario Editor
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using ImGuiNET;
using OpenCobra.GDK.GUI;

namespace OpenRCT3.Scenario;

public class Editor : IWindow {
  public bool Open { get; private set; } = true;

  public void Render() {
    if (!Open) return;

    var open = Open;
    ImGui.Begin("Scenario Editor", ref open);

    // Row of icon buttons
    if (ImGui.Button("Save")) {
      // TODO: Save scenario
    }
    ImGui.SameLine();
    if (ImGui.Button("Quit")) {
      open = false;
    }

    ImGui.Separator();

    // Column of labeled buttons
    if (ImGui.Button("Setup Park", new System.Numerics.Vector2(200, 0))) {
      // TODO: Open park dialog
    }
    if (ImGui.Button("Choose Finances", new System.Numerics.Vector2(200, 0))) {
      // TODO: Open finances dialog
    }
    if (ImGui.Button("Choose Objectives & Challenges", new System.Numerics.Vector2(200, 0))) {
      // TODO: Open objectives dialog
    }

    ImGui.End();
    if (open != Open) Open = open;
  }
}

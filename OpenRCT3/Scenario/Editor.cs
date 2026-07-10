// Scenario Editor
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;
using System.Numerics;

namespace OpenRCT3.Scenario;

public class Editor : IWindow {
  // Wide enough to fit the "Choose Objectives & Challenges" button's label.
  private const float ButtonWidth = 235;

  public bool Open { get; private set; } = true;

  public void Render() {
    if (!Open) return;

    var open = Open;
    ImGui.SetNextWindowSize(new Vector2(ButtonWidth + ImGui.GetStyle().WindowPadding.X * 2, 0), ImGuiCond.Once);
    ImGui.Begin("Scenario Editor", ref open, ImGuiWindowFlags.NoResize);

    // Row of icon buttons
    if (ImGui.Button("Save")) {
      // TODO: Save scenario
    }
    ImGui.SameLine();
    if (ImGui.Button("Quit")) {
      open = false;
      // TODO: Quit the game
    }

    ImGui.Separator();

    // Column of labeled buttons
    if (ImGui.Button("Setup Park", new Vector2(ButtonWidth, 0))) {
      // TODO: Open park dialog
    }
    if (ImGui.Button("Choose Finances", new Vector2(ButtonWidth, 0))) {
      // TODO: Open finances dialog
    }
    if (ImGui.Button("Choose Objectives & Challenges", new Vector2(ButtonWidth, 0))) {
      // TODO: Open objectives dialog
    }

    ImGui.End();
    if (open != Open) Open = open;
  }
}

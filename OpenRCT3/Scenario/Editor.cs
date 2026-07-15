// Scenario Editor
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;
using static OpenRCT3.UI.Gui;

namespace OpenRCT3.Scenario;

public class Editor : IWindow {
  // Wide enough to fit the "Choose Objectives & Challenges" button's label.
  private const float ButtonWidth = 235;

  public bool Open { get; private set; } = true;
  public event Action? OpenPark;
  public event Action? SavePark;
  public event Action? Exit;

  public void Render() {
    if (!Open) return;

    var open = Open;

    var viewport = ImGui.GetMainViewport();
    var windowPos = new Vector2(viewport.WorkPos.X + Padding, viewport.WorkPos.Y + Padding);
    ImGui.SetNextWindowPos(windowPos, ImGuiCond.Appearing, new Vector2(0f, 0f));
    ImGui.SetNextWindowSize(new Vector2(ButtonWidth + ImGui.GetStyle().WindowPadding.X * 2, 0), ImGuiCond.Once);
    ImGui.Begin("Scenario Editor", ref open, ImGuiWindowFlags.NoResize);

    // Row of icon buttons
    if (ImGui.Button("Open")) {
      OpenPark?.Invoke();
    }
    ImGui.SameLine();
    if (ImGui.Button("Save")) {
      SavePark?.Invoke();
    }
    ImGui.SameLine();
    if (ImGui.Button("Quit")) {
      open = false;
      Exit?.Invoke();
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

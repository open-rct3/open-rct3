// Park Chooser
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Hexa.NET.ImGui;
using OpenCobra.GDK.GUI;
using OpenRCT3.Platforms;

namespace OpenRCT3.Scenario;

/// <summary>
/// Lists saved-park <c>.dat</c> files from <see cref="Paths.NewScenariosDirectory"/>,
/// <see cref="Paths.ParksDirectory"/>, and <see cref="Paths.ScenariosDirectory"/> for the user to
/// pick one to open. Aggregates all three folders together for now; distinguishing scenarios from
/// saved parks in the UI is a follow-on concern.
/// </summary>
public class ParkChooser : IWindow {
  private const float WindowWidth = 400;
  private const int MaxVisibleRows = 12;

  public bool Open { get; private set; }
  public event Action<string>? ParkSelected;

  private List<string> parkFiles = [];
  private string? selectedPath;

  /// <summary>Rescans the saved-park folders and opens the chooser.</summary>
  public void Show() {
    parkFiles = DiscoverParkFiles();
    selectedPath = null;
    Open = true;
  }

  public void Render() {
    if (!Open) return;

    var open = Open;

    var viewport = ImGui.GetMainViewport();
    var windowPos = viewport.WorkPos + viewport.WorkSize / 2;
    ImGui.SetNextWindowPos(windowPos, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
    ImGui.SetNextWindowSize(new Vector2(WindowWidth, 0), ImGuiCond.Appearing);
    ImGui.Begin("Open Park", ref open, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize);

    if (parkFiles.Count == 0) {
      ImGui.TextDisabled("No saved parks found.");
    } else {
      var rows = Math.Min(parkFiles.Count, MaxVisibleRows);
      var listSize = new Vector2(
        WindowWidth - ImGui.GetStyle().WindowPadding.X * 2,
        rows * ImGui.GetTextLineHeightWithSpacing()
      );
      ImGui.BeginListBox("##ParkFiles", listSize);
      foreach (var path in parkFiles) {
        var name = Path.GetFileNameWithoutExtension(path);
        if (!ImGui.Selectable(name, path == selectedPath, ImGuiSelectableFlags.AllowDoubleClick)) continue;

        selectedPath = path;
        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
          ParkSelected?.Invoke(path);
          open = false;
        }
      }
      ImGui.EndListBox();
    }

    ImGui.BeginDisabled(selectedPath == null);
    if (ImGui.Button("Open")) {
      ParkSelected?.Invoke(selectedPath!);
      open = false;
    }
    ImGui.EndDisabled();
    ImGui.SameLine();
    if (ImGui.Button("Cancel")) open = false;

    ImGui.End();
    if (open != Open) Open = open;
  }

  private static List<string> DiscoverParkFiles() {
    string[] directories = [Paths.NewScenariosDirectory, Paths.ParksDirectory, Paths.ScenariosDirectory];
    return [.. directories
      .Where(Directory.Exists)
      .SelectMany(dir => Directory.EnumerateFiles(dir, "*.dat"))
      .OrderBy(Path.GetFileName)];
  }
}

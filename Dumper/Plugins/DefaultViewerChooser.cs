// DefaultViewerChooser
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Dumper.Plugins;
using OpenCobra.OVL.Files;

namespace Dumper;

/// <summary>Dialog that lets the user pick a default viewer plugin for a file type.</summary>
sealed partial class DefaultViewerChooser : Form {

  /// <summary>The file type tag selected by the user.</summary>
  public string SelectedFileTypeTag { get; private set; } = "";

  /// <summary>The plugin the user wants as the default, or null if unchanged.</summary>
  public IViewerPlugin? SelectedDefaultPlugin { get; private set; }

  public DefaultViewerChooser(Dictionary<string, List<IViewerPlugin>> viewersByTag) {
    InitializeComponent();

    // Populate combo with file types that have viewers
    foreach (var (tag, viewers) in viewersByTag.OrderBy(kv => kv.Key)) {
      var fileType = tag.ToFileType();
      var displayName = fileType == FileType.Unknown ? tag : fileType.ToDisplayName();
      fileTypeCombo.Items.Add(new FileTypeComboItem(tag, displayName, viewers));
    }

    if (fileTypeCombo.Items.Count > 0)
      fileTypeCombo.SelectedIndex = 0;

    fileTypeCombo.SelectedIndexChanged += FileTypeCombo_Changed;
  }

  protected override void OnFormClosing(FormClosingEventArgs e) {
    base.OnFormClosing(e);
    if (DialogResult != DialogResult.OK) return;

    if (fileTypeCombo.SelectedItem is not FileTypeComboItem item) {
      e.Cancel = true;
      return;
    }

    SelectedFileTypeTag = item.Tag;
    SelectedDefaultPlugin = item.Viewers.Count > 0 ? item.Viewers[0] : null;
  }

  private void FileTypeCombo_Changed(object? sender, EventArgs e) {
    if (fileTypeCombo.SelectedItem is not FileTypeComboItem item) {
      currentDefaultLabel.Text = "";
      return;
    }

    var defaultViewer = item.Viewers.Count > 0 ? item.Viewers[0] : null;
    currentDefaultLabel.Text = defaultViewer != null
      ? $"Current default: {defaultViewer.Name} v{defaultViewer.Version}"
      : "No viewer available";
  }

  private sealed class FileTypeComboItem {
    public string Tag { get; }
    public string DisplayName { get; }
    public List<IViewerPlugin> Viewers { get; }

    public FileTypeComboItem(string tag, string displayName, List<IViewerPlugin> viewers) {
      Tag = tag;
      DisplayName = displayName;
      Viewers = viewers;
    }

    public override string ToString() => $"{DisplayName} (.{Tag})";
  }
}

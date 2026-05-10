// ContentPanelHeader
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Dumper.Plugins;

namespace Dumper;

/// <summary>Header bar docked to the top of the content panel.</summary>
/// <remarks>
/// Shows the current plugin name centered, and a dropdown on the right
/// to switch between available viewers for the current file type.
/// </remarks>
sealed partial class ContentPanelHeader : TableLayoutPanel {

  /// <summary>Fired when the user selects a different viewer from the dropdown.</summary>
  public event EventHandler<IViewerPlugin?>? ViewerChanged;

  public ContentPanelHeader() {
    InitializeComponent();
    viewerCombo.SelectedIndexChanged += ViewerCombo_SelectedIndexChanged;
  }

  /// <summary>Set the header to show the given viewer's name and populate the dropdown.</summary>
  public void SetViewers(IViewerPlugin active, IEnumerable<IViewerPlugin> allViewers) {
    viewerCombo.SelectedIndexChanged -= ViewerCombo_SelectedIndexChanged;

    nameLabel.Text = $"{active.Name} v{active.Version}";
    viewerCombo.Items.Clear();

    foreach (var viewer in allViewers) {
      var idx = viewerCombo.Items.Add(new ViewerComboItem(viewer));
      if (viewer == active)
        viewerCombo.SelectedIndex = idx;
    }

    viewerCombo.Enabled = allViewers.Count() > 1;
    viewerCombo.SelectedIndexChanged += ViewerCombo_SelectedIndexChanged;
  }

  /// <summary>Show the header with a message (no viewer available).</summary>
  public void SetMessage(string message) {
    nameLabel.Text = message;
    viewerCombo.Items.Clear();
    viewerCombo.Enabled = false;
  }

  private void ViewerCombo_SelectedIndexChanged(object? sender, EventArgs e) {
    if (viewerCombo.SelectedItem is ViewerComboItem item)
      ViewerChanged?.Invoke(this, item.Plugin);
  }

  private sealed class ViewerComboItem {
    public IViewerPlugin Plugin { get; }
    public ViewerComboItem(IViewerPlugin plugin) => Plugin = plugin;
    public override string ToString() => $"{Plugin.Name} v{Plugin.Version}";
  }
}

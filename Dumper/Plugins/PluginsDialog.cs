// PluginsDialog
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper.Plugins;

/// <summary>Dialog that lists all loaded viewer plugins and shows their metadata.</summary>
sealed partial class PluginsDialog : Form {
  private readonly IReadOnlyList<IViewerPlugin> _plugins;

  public PluginsDialog(IReadOnlyList<IViewerPlugin> plugins) {
    _plugins = plugins;
    InitializeComponent();

    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    var color = new DuoToneColor(Color.FromArgb(64, 64, 64), Color.FromArgb(185, 185, 185));
    var bmp = MainForm.RenderIcon(icons, "Puzzle", color);
    if (bmp != null) Icon = Icon.FromHandle(bmp.GetHicon());

    if (_plugins.Count == 0) {
      emptyLabel.Visible = true;
      return;
    }

    foreach (var plugin in _plugins)
      this.plugins.Items.Add(plugin.Info.Name);

    this.plugins.SelectedIndex = 0;
  }

  private void PluginListBox_SelectedIndexChanged(object? sender, System.EventArgs e) {
    var idx = plugins.SelectedIndex;
    if (idx < 0 || idx >= _plugins.Count) {
      metadata.Visible = false;
      return;
    }

    var info = _plugins[idx].Info;
    nameValue.Text = info.Name;
    versionValue.Text = info.Version;
    fileTypesValue.Text = info.FileTypes.Count > 0
      ? string.Join(", ", info.FileTypes.Select(t => $".{t}"))
      : "(none)";
    locationValue.Text = info.SourcePath;
    metadata.Visible = true;
  }
}

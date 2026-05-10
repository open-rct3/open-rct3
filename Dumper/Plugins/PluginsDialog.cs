// PluginsDialog
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper.Plugins;

/// <summary>Dialog that lists all loaded viewer plugins and shows their metadata.</summary>
sealed partial class PluginsDialog : Form {
  private readonly IReadOnlyList<IViewerPlugin> plugins;
  private readonly Image enableIcon;
  private readonly Image disableIcon;

  public PluginsDialog(IReadOnlyList<IViewerPlugin> plugins) {
    this.plugins = plugins;
    InitializeComponent();

    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    var color = new DuoToneColor(Color.FromArgb(64, 64, 64), Color.FromArgb(185, 185, 185));
    var disabledColor = new DuoToneColor(Color.FromArgb(180, 180, 180), Color.FromArgb(210, 210, 210));
    var removeColor = new DuoToneColor(Color.FromArgb(211, 47, 47), Color.Transparent);

    var puzzleBmp = MainForm.RenderIcon(icons, "PuzzleOutline", color);
    if (puzzleBmp != null) Icon = Icon.FromHandle(puzzleBmp.GetHicon());

    enableIcon = MainForm.RenderIcon(icons, "Puzzle", color)!;
    disableIcon = MainForm.RenderIcon(icons, "PuzzleMinusOutline", disabledColor)!;

    install.Image = MainForm.RenderIcon(icons, "PuzzleOutline", color);
    uninstall.Image = MainForm.RenderIcon(icons, "TrashCan", removeColor);
  }

  private void UpdatePluginList() {
    if (pluginList.Items.Count > 0) pluginList.Items.Clear();
    foreach (var plugin in plugins)
      pluginList.Items.Add(plugin);

    pluginList.SelectedIndex = pluginList.SelectedIndex > -1 ? pluginList.SelectedIndex : 0;
  }

  private void PluginsDialog_Load(object sender, EventArgs e) => UpdatePluginList();

  private void PluginListBox_SelectedIndexChanged(object? sender, EventArgs e) {
    var selectedPlugin = pluginList.SelectedItem as IViewerPlugin;
    var isEnabled = selectedPlugin?.Enabled ?? false;
    var isEmpty = plugins.Count == 0;

    emptyLabel.Visible = isEmpty;
    metadata.Visible = !isEmpty;

    // Update toolbar state
    toggleActive.Image = isEnabled ? disableIcon : enableIcon;
    toggleActive.ToolTipText = isEnabled ? "Disable this plugin" : "Enable this plugin";

    // Update metadata
    if (selectedPlugin == null) return;
    var info = selectedPlugin.Info;
    nameValue.Text = info.Name;
    versionValue.Text = info.Version;
    fileTypesValue.Text = info.FileTypes.Count > 0
      ? string.Join(", ", info.FileTypes.Select(type => $".{type}"))
      : "None";
    locationValue.Text = info.SourcePath;
  }

  private void InstallFromCatalog_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Install from catalog is not yet implemented.");

  private void InstallFromDisk_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Install from disk is not yet implemented.");

  private void ToggleActive_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Enable/disable is not yet implemented.");

  private void Uninstall_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Uninstall is not yet implemented.");
}

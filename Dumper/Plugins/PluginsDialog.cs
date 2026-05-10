// PluginsDialog
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper.Plugins;

/// <summary>Dialog that lists all loaded viewer plugins and shows their metadata.</summary>
sealed partial class PluginsDialog : Form {
  private static readonly string NoPluginsText = "No plugins installed.";
  private readonly IReadOnlyList<IViewerPlugin> plugins;

  public PluginsDialog(IReadOnlyList<IViewerPlugin> plugins) {
    this.plugins = plugins;

    InitializeComponent();
    InitializeComponentIcons();

    emptyLabel.Text = NoPluginsText;
    DialogResult = DialogResult.None;
  }

  private void UpdatePluginList() {
    var selectedIndex = pluginList.SelectedIndex;

    // FIXME: emptyLabel.Image = Icons.Spinner (a GIF, maybe there's something that came with Windows Vista?);
    emptyLabel.Visible = true;
    metadata.Visible = false;

    if (pluginList.Items.Count > 0) pluginList.Items.Clear();
    foreach (var plugin in plugins)
      pluginList.Items.Add(plugin);

    if (plugins.Count > 0)
      pluginList.SelectedIndex = selectedIndex < plugins.Count ? selectedIndex : 0;
  }

  private void UpdateEmptyState() {
    var isEmpty = plugins.Count == 0;
    emptyLabel.Visible = isEmpty;
    metadata.Visible = !isEmpty;
  }

  private void InitializeComponentIcons() {
    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();

    Icon = Icons.Render(icons, "PuzzleOutline")?.ToIcon() ?? Icons.DefaultWindowIcon;

    install.Image = Icons.Render(icons, "PuzzlePlusOutline");
    installFromCatalog.Image = Icons.Render(icons, "Web", Icons.Blue);
    installFromDisk.Image = Icons.Render(icons, "Harddisk");
    uninstall.Image = Icons.Render(icons, "TrashCan", Icons.Danger);
  }

  private void PluginsDialog_Load(object sender, EventArgs e) {
    UpdatePluginList();
    UpdateEmptyState();
  }

  private void PluginListBox_SelectedIndexChanged(object? sender, EventArgs e) {
    var selectedPlugin = pluginList.SelectedItem as IViewerPlugin;
    var isEnabled = selectedPlugin?.Enabled ?? false;

    // Update metadata
    if (selectedPlugin == null) return;
    nameValue.Text = selectedPlugin.Name;
    versionValue.Text = selectedPlugin.Version;
    fileTypesValue.Text = selectedPlugin.FileTypes.Count > 0
      ? string.Join(", ", selectedPlugin.FileTypes.Select(type => $".{type}"))
      : "None";
    toolTip.SetToolTip(enabled, isEnabled ? "Disable this plugin" : "Enable this plugin");
  }

  // TODO: Use UpdateEmptyState to show installation progress
  private void InstallFromCatalog_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Install from catalog is not yet implemented.");

  // TODO: Use UpdateEmptyState to show installation progress
  private void InstallFromDisk_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Install from disk is not yet implemented.");

  private void ToggleActive_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Enable/disable is not yet implemented.");

  private void Uninstall_Click(object? sender, EventArgs e) =>
    throw new NotImplementedException("Uninstall is not yet implemented.");

  private void InstallSplitBtn_DropDownOpening(object sender, EventArgs e) => toolStrip.ShowItemToolTips = false;
  private void InstallSplitBtn_DropDownClosed(object sender, EventArgs e) => toolStrip.ShowItemToolTips = true;

  private void OpenFolder_Click(object sender, EventArgs e) =>
    throw new NotImplementedException("Reveal in file explorer is not yet implemented");

  private void Close_Click(object sender, EventArgs e) => Close();
}

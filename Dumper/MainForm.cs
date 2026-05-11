// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.
using System.Security;
using Dumper.Plugins;
using Dumper.Properties;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;
using File = System.IO.File;

namespace Dumper;

public partial class MainForm : Form {
  static readonly string ready = "Ready";
  static readonly string openingArchive = "Opening archive…";
  static readonly string resourcesFmt = "{0} Resources";
  static readonly string ovlFmt = "{0} OVLs";

  private readonly PluginManager pluginManager = new();
  private Ovl? currentOvl;
  private readonly Dictionary<TreeNode, OvlLoaderEntry> _nodeEntries = [];
  private bool suppressSplitterMoved = false;

  public MainForm() {
    InitializeComponent();
    InitializeComponentIcons();

    Settings.Default.Reload();
    // TODO: Add user's RCT3 dir
    // openDialog.CustomPlaces.Add(InstallFinder.Find());
    TryLoadPlugins();
  }

  protected override void OnShown(EventArgs e) {
    base.OnShown(e);

    // Initialize web view after the form is shown (requires message loop)
    _ = contentPanel.InitializeAsync();
  }

  protected override void OnFormClosed(FormClosedEventArgs e) {
    pluginManager.Dispose();

    base.OnFormClosed(e);
  }

  private async Task OpenOvl() {
    openDialog.InitialDirectory = Settings.Default.LastOvlOpened != null
      ? Directory.GetParent(Settings.Default.LastOvlOpened)?.FullName ?? ""
      : "";
    if (openDialog.ShowDialog() != DialogResult.OK) return;

    this.Cursor = Cursors.WaitCursor;
    LoadOvl(await Task.Run(() => Ovl.Open(openDialog.FileName)));
    this.Cursor = Cursors.Default;

    Settings.Default.LastOvlOpened = openDialog.FileName;
    Settings.Default.Save();
  }

  private void LoadOvl(Ovl ovl) {
    currentOvl = ovl;
    _nodeEntries.Clear();
    contentPanel.ShowEmpty(true);

    // Update window title with document name
    var docName = Path.GetFileName(ovl.FileName);
    var lower = docName.ToLower();
    if (lower.EndsWith(".common.ovl"))
      docName = docName[..^".common.ovl".Length];
    else if (lower.EndsWith(".unique.ovl"))
      docName = docName[..^".unique.ovl".Length];
    else if (lower.EndsWith(".ovl"))
      docName = docName[..^".ovl".Length];
    else
      docName = Ovl.UnnamedOvl;
    Text = $"OVL Dumper \u2014 {docName}";

    treeView.BeginUpdate();
    treeView.Nodes.Clear();

    if (ovl.LoaderEntries.Count > 0) {
      EnsureTreeImageList();

      // Group entries by source file, preserving common/unique order
      var entriesByFile = ovl.LoaderEntries
        .GroupBy(e => e.SourceFile)
        .ToDictionary(g => g.Key, g => g.ToList());

      // Determine file order: common first, then unique
      var fileNames = entriesByFile.Keys
        .OrderBy(f => f.Contains(".unique.") ? 1 : 0)
        .ToList();

      // For unpaired archives, fall back to the Ovl.FileName
      if (fileNames.Count == 0)
        fileNames.Add(Path.GetFileName(ovl.FileName));

      foreach (var fileName in fileNames) {
        var fileNode = treeView.Nodes.Add(fileName, fileName);
        fileNode.ImageKey = "FolderOpen";
        fileNode.SelectedImageKey = "FolderOpen";

        if (!entriesByFile.TryGetValue(fileName, out var entries)) continue;

        // Resolve display names and types for all entries
        var resolved = entries.Select(entry => {
          var loaderFileType = entry.Tag.ToFileType();
          string displayName;
          FileType symbolFileType;
          var colonIdx = entry.SymbolName.LastIndexOf(':');
          if (colonIdx >= 0) {
            displayName = entry.SymbolName[..colonIdx];
            symbolFileType = entry.SymbolName[(colonIdx + 1)..].ToFileType();
          } else {
            displayName = entry.Name;
            symbolFileType = loaderFileType;
          }
          return (entry, displayName, loaderFileType, symbolFileType);
        }).ToList();

        // Group numbered animation frames by their base name (strip trailing digits).
        // Groups with multiple entries are animated textures: parent = base name, children = suffixes.
        // Only apply this nesting for textures.
        var frameGroups = resolved
          .Where(r => (r.loaderFileType == FileType.Texture || r.loaderFileType == FileType.Flic) && EndsWithDigit(r.displayName))
          .GroupBy(r => StripTrailingDigits(r.displayName))
          .Where(g => g.Count() > 1)
          .ToDictionary(g => g.Key, g => g.OrderBy(r => r.displayName).ToList());
        var groupedNames = new HashSet<string>(
          frameGroups.Values.SelectMany(g => g).Select(r => r.displayName));

        // Collect non-animated-texture entries
        var remainingEntries = resolved
          .Where(r => !groupedNames.Contains(r.displayName))
          .ToList();

        // Group remaining entries by display name to find duplicates
        var duplicateGroups = remainingEntries
          .GroupBy(r => r.displayName)
          .Where(g => g.Count() > 1)
          .ToDictionary(g => g.Key, g => g.ToList());
        var duplicateNames = new HashSet<string>(duplicateGroups.Keys);

        // Add animated texture groups
        foreach (var (entry, displayName, loaderFileType, symbolFileType) in resolved) {
          if (groupedNames.Contains(displayName)) {
            var baseName = StripTrailingDigits(displayName);
            if (!frameGroups.ContainsKey(baseName)) continue;

            var group = frameGroups[baseName];
            var parentNode = fileNode.Nodes.Add(baseName);
            parentNode.ImageKey = FileType.Flic.ToIconName();
            parentNode.SelectedImageKey = FileType.Flic.ToIconName();
            parentNode.Tag = FileType.Flic;
            parentNode.ToolTipText = $"Animated texture ({group.Count} frames) \u00b7 Loader: {loaderFileType.ToDisplayName()}";

            foreach (var frame in group) {
              var suffix = frame.displayName[baseName.Length..];
              var childNode = parentNode.Nodes.Add(suffix);
              childNode.ImageKey = FileType.Texture.ToIconName();
              childNode.SelectedImageKey = FileType.Texture.ToIconName();
              childNode.ToolTipText = BuildEntryTooltip(frame.symbolFileType, frame.loaderFileType);
              _nodeEntries[childNode] = frame.entry;
            }

            frameGroups.Remove(baseName);
            continue;
          }
        }

        // Add duplicate name groups
        foreach (var (name, group) in duplicateGroups) {
          var commonType = group.Select(r => r.loaderFileType).Distinct().Count() == 1
            ? group.First().loaderFileType
            : FileType.Unknown;
          var iconKey = commonType == FileType.Unknown
            ? "FileMultipleOutline"
            : commonType.ToGroupIconName();
          var pluralName = Pluralize(commonType.ToDisplayName());
          var parentNode = fileNode.Nodes.Add($"{group.Count} {pluralName}");
          parentNode.ImageKey = iconKey;
          parentNode.SelectedImageKey = iconKey;
          parentNode.Tag = commonType;
          parentNode.ToolTipText = $"{group.Count} entries named \"{name}\"";

          foreach (var (entry, _, loaderFileType, symbolFileType) in group) {
            var childIconKey = loaderFileType == FileType.Flic
              ? FileType.Texture.ToIconName()
              : loaderFileType.ToIconName();
            var childNode = parentNode.Nodes.Add(name);
            childNode.ImageKey = childIconKey;
            childNode.SelectedImageKey = childIconKey;
            childNode.Tag = loaderFileType;
            childNode.ToolTipText = BuildEntryTooltip(symbolFileType, loaderFileType);
            _nodeEntries[childNode] = entry;
          }
        }

        // Add non-duplicate, non-animated entries
        foreach (var (entry, displayName, loaderFileType, symbolFileType) in remainingEntries) {
          if (duplicateNames.Contains(displayName)) continue;

          var iconKey = loaderFileType == FileType.Flic
            ? FileType.Texture.ToIconName()
            : loaderFileType.ToIconName();
          var tooltip = BuildEntryTooltip(symbolFileType, loaderFileType);

          var node = fileNode.Nodes.Add(displayName);
          node.ImageKey = iconKey;
          node.SelectedImageKey = iconKey;
          node.Tag = loaderFileType;
          node.ToolTipText = tooltip;
          _nodeEntries[node] = entry;
        }
      }
    } else {
      // Fallback: single root node with loader type descriptors
      EnsureTreeImageList();
      var root = treeView.Nodes.Add(Path.GetFileName(ovl.FileName), Path.GetFileName(ovl.FileName));
      root.ImageKey = "FolderOpen";
      root.SelectedImageKey = "FolderOpen";
      foreach (var header in ovl.LoaderHeaders) {
        var fileType = header.tag.ToFileType();
        var node = root.Nodes.Add(header.name);
        node.ImageKey = fileType.ToIconName();
        node.SelectedImageKey = fileType.ToIconName();
        node.Tag = fileType;
        node.ToolTipText = fileType.ToDisplayName();
      }
    }


    foreach (TreeNode fileNode in treeView.Nodes) {
      fileNode.Expand();
      foreach (TreeNode child in fileNode.Nodes) {
        if (child.Tag is FileType ft && ft != FileType.Flic && !IsDuplicateGroup(child.Text)) {
          child.Expand();
        }
      }
    }
    treeView.EndUpdate();

    UpdateStatusBar();

    FitSidebarToContent(ClientSize.Width / 2);
  }

  private void UpdateStatusBar() {
    var ovlCount = treeView.Nodes.Count;
    var resourceCount = CountLeafNodes(treeView.Nodes);
    ovlCountLabel.Text = string.Format(ovlFmt, ovlCount);
    resourceCountLabel.Text = string.Format(resourcesFmt, resourceCount);
  }

  private static int CountLeafNodes(TreeNodeCollection nodes) {
    var count = 0;
    foreach (TreeNode node in nodes) {
      if (node.Nodes.Count == 0)
        count++;
      else
        count += CountLeafNodes(node.Nodes);
    }
    return count;
  }

  private void ClearStatusBarCounts() {
    ovlCountLabel.Text = "";
    resourceCountLabel.Text = "";
  }

  private static string BuildEntryTooltip(FileType symbolType, FileType loaderType) {
    if (symbolType == loaderType)
      return symbolType.ToDisplayName();
    if (symbolType == FileType.Unknown)
      return loaderType.ToDisplayName();
    return $"{symbolType.ToDisplayName()} \u00b7 Loader: {loaderType.ToDisplayName()}";
  }

  private static bool EndsWithDigit(string name) => name.Length > 0 && char.IsDigit(name[^1]);

  private static string StripTrailingDigits(string name) {
    var i = name.Length;
    while (i > 0 && char.IsDigit(name[i - 1])) i--;
    return name[..i];
  }

  private static string Pluralize(string word) {
    if (word.EndsWith('y'))
      return $"{word[..^1]}ies";
    if (word.EndsWith('s') || word.EndsWith('x') || word.EndsWith('z') || word.EndsWith("ch") || word.EndsWith("sh"))
      return $"{word}es";
    return $"{word}s";
  }

  private static bool IsDuplicateGroup(string text) {
    var spaceIdx = text.IndexOf(' ');
    return spaceIdx > 0 && int.TryParse(text[..spaceIdx], out _);
  }

  private void EnsureTreeImageList() {
    if (treeView.ImageList != null) return;

    var icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    var imageList = new ImageList { ImageSize = IconSize };

    // Add folder icon for file group nodes
    imageList.Images.Add("FolderOpen", Icons.Render(icons, "FolderOpen", Icons.Folder)!);

    // Add icons for each file type, skipping unknown icon names
    foreach (var fileType in Enum.GetValues<FileType>()) {
      var iconName = fileType.ToIconName();
      if (!imageList.Images.ContainsKey(iconName)) {
        var bmp = Icons.Render(icons, iconName);
        if (bmp != null)
          imageList.Images.Add(iconName, bmp);
      }
    }

    treeView.ImageList = imageList;
  }

  private void TryLoadPlugins() {
    // Load plugins
    try {
      pluginManager.Load();
    } catch (Exception ex) {
      Debug.WriteLine($"Plugin loading failed: {ex.Message}");

      if (ex is UnauthorizedAccessException || ex is SecurityException) {
        // TODO: Detect if this is a user-land problem and message the user
      }

      // Show a retry offer to the user
      var result = MessageBox.Show(
        "Could not load plugins.\n\nTry again or continue anyway?",
        "Error",
        MessageBoxButtons.CancelTryContinue,
        MessageBoxIcon.Error
      );

      if (result == DialogResult.TryAgain) TryLoadPlugins();
      // User cancelled the operation, confirm app exit
      else if (result == DialogResult.Cancel && TryExit() == DialogResult.Yes)
        Application.Exit();
    }
  }

  private static DialogResult TryExit() => MessageBox.Show(
    "Are you sure you want to exit?",
    "Exit Dumper?",
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question,
    MessageBoxDefaultButton.Button2
  );

  private async void OpenMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    ClearStatusBarCounts();
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private async void OpenArchive_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    ClearStatusBarCounts();
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private void ExitMenuItem_Click(object sender, EventArgs e) {
    if (TryExit() == DialogResult.Yes) Application.Exit();
  }

  private void PluginsMenuItem_Click(object sender, EventArgs e) =>
    new PluginsDialog(pluginManager.AllPlugins).ShowDialog(this);

  private void OvlTree_AfterSelect(object? sender, TreeViewEventArgs e) {
    if (e.Node == null || currentOvl == null) {
      contentPanel.ShowEmpty(currentOvl != null);
      return;
    }

    var fileType = e.Node.Tag as FileType?;

    // If this node has a loader entry, show it via the plugin viewer
    if (_nodeEntries.TryGetValue(e.Node, out var entry) && fileType != null && fileType != FileType.Unknown) {
      var tag = fileType.Value.ToTagString();
      var viewers = pluginManager.GetViewers(tag);
      if (viewers.Count == 0) {
        contentPanel.ShowNoViewer(fileType.Value);
        return;
      }

      var data = currentOvl.GetResourceBytes(entry);
      if (data == null) {
        contentPanel.ShowEmpty(currentOvl != null);
        MessageBox.Show("Failed to load selected resource.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
      }

      contentPanel.ShowContent(viewers, data);
    } else {
      // Group node or no entry, show empty
      contentPanel.ShowEmpty(currentOvl != null);
    }
  }

  private void OvlTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e) {
    if (e.Button != MouseButtons.Right || e.Node == null) return;

    // Only show context menu on leaf nodes with a known file type and loader entry
    var fileType = e.Node.Tag as FileType?;
    if (fileType == null || fileType == FileType.Unknown || !_nodeEntries.ContainsKey(e.Node)) return;

    var tag = fileType.Value.ToTagString();
    var viewers = pluginManager.GetViewers(tag);

    var menu = new ContextMenuStrip();

    // --- Open With submenu ---
    var openWith = new ToolStripMenuItem("Open With");
    if (viewers.Count > 0) {
      var defaultViewer = pluginManager.GetDefaultViewer(tag);
      foreach (var viewer in viewers) {
        var viewerItem = new ToolStripMenuItem($"{viewer.Name}  v {viewer.Version}") {
          Tag = viewer,
          Font = viewer == defaultViewer
            ? new Font(menu.Font, FontStyle.Bold)
            : menu.Font,
        };
        viewerItem.Click += (_, _) => {
          if (currentOvl == null || !_nodeEntries.TryGetValue(e.Node, out var entry)) return;
          var data = currentOvl.GetResourceBytes(entry);
          if (data == null) return;
          contentPanel.ShowContent(viewers, data);
        };
        openWith.DropDownItems.Add(viewerItem);
      }
      openWith.DropDownItems.Add(new ToolStripSeparator());
    }
    var chooseDefault = new ToolStripMenuItem("Choose a default viewer\u2026");
    chooseDefault.Click += (_, _) => {
      using var chooser = new DefaultViewerChooser(pluginManager.GetRegistrySnapshot());
      if (chooser.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(chooser.SelectedFileTypeTag)) {
        pluginManager.SetDefaultViewer(chooser.SelectedFileTypeTag, chooser.SelectedDefaultPlugin!);
        // Re-render if this node's type was changed
        if (tag == chooser.SelectedFileTypeTag)
          OvlTree_AfterSelect(sender, new TreeViewEventArgs(e.Node));
      }
    };
    openWith.DropDownItems.Add(chooseDefault);
    menu.Items.Add(openWith);

    // --- Export ---
    var export = new ToolStripMenuItem("Export");
    export.Click += Export_Click;
    menu.Items.Add(export);

    menu.Items.Add(new ToolStripSeparator());

    // --- Properties ---
    var properties = new ToolStripMenuItem("Properties");
    properties.Click += (_, _) => {
      if (currentOvl == null || !_nodeEntries.TryGetValue(e.Node, out var entry)) return;
      var hasViewer = viewers.Count > 0;
      using var dialog = new ResourceProperties(entry, fileType.Value, hasViewer);
      dialog.ShowDialog(this);
    };
    menu.Items.Add(properties);

    menu.Show(treeView, e.Location);
  }

  private async void Export_Click(object? sender, EventArgs e) {
    var node = treeView.SelectedNode;
    var fileType = node.Tag as FileType?;
    if (node == null || fileType == null) return;
    if (currentOvl == null || !_nodeEntries.TryGetValue(node, out var entry)) return;

    var data = currentOvl.GetResourceBytes(entry);
    if (data == null) {
      MessageBox.Show("Failed to read resource data.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      return;
    }

    var fileTypeName = fileType.Value.ToDisplayName();
    var ext = fileType.Value.ToTagString();
    using var saveDialog = new SaveFileDialog {
      Title = $"Export {fileTypeName}",
      FileName = $"{node.Text}.{ext}",
      Filter = $"{fileTypeName} (*.{ext})|*.{ext}|All Files (*.*)|*.*",
      DefaultExt = ext,
    };
    saveDialog.InitialDirectory = Settings.Default.LastDocumentExported != null
      ? Directory.GetParent(Settings.Default.LastDocumentExported)?.FullName ?? ""
      : "";
    if (saveDialog.ShowDialog(this) != DialogResult.OK) return;

    try {
      await File.WriteAllBytesAsync(saveDialog.FileName, data);

      Settings.Default.LastDocumentExported = saveDialog.FileName;
      Settings.Default.Save();
    } catch (Exception ex) {
      MessageBox.Show($"Failed to export file:\n{ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private void Splitter_MouseDoubleClick(object? sender, MouseEventArgs e) {
    if (treeView.Nodes.Count == 0) return;
    FitSidebarToContent(ClientSize.Width / 2);
  }

  private void SplitView_SplitterMoved(object? sender, SplitterEventArgs e) => ClampSplitterDistance();
  private void SplitView_SizeChanged(object? sender, EventArgs e) => ClampSplitterDistance();

  private void ClampSplitterDistance() {
    if (suppressSplitterMoved) return;
    // Prevent infinite loops when user's adjust the splitter distance
    suppressSplitterMoved = true;
    try {
      var maxAllowed = splitView.Width / 2;
      if (splitView.SplitterDistance > maxAllowed) {
        splitView.SplitterDistance = maxAllowed;
      }
    } finally {
      suppressSplitterMoved = false;
    }
  }

  private void FitSidebarToContent(int maxWidth) {
    int contentWidth = 0;
    foreach (TreeNode node in treeView.Nodes)
      contentWidth = Math.Max(contentWidth, MeasureNodeWidthFast(node));

    var padding = SystemInformation.VerticalScrollBarWidth + 8;
    // Clamp maximum width to no more than 25% wider than content width
    var maxAllowedWidth = (int)(contentWidth * 1.25);
    maxWidth = Math.Min(maxWidth, maxAllowedWidth);

    var target = Math.Min(contentWidth + padding, maxWidth);
    target = Math.Min(target, splitView.Width - splitView.Panel2MinSize);
    target = Math.Max(target, splitView.Panel1MinSize);
    splitView.SplitterDistance = target;
  }

  private int MeasureNodeWidthFast(TreeNode node) {
    var textWidth = TextRenderer.MeasureText(node.Text, treeView.Font).Width;
    var indent = node.Level * treeView.Indent;
    var iconWidth = treeView.ImageList?.ImageSize.Width ?? 0;
    var plusMinusBtnWidth = SystemInformation.SmallIconSize.Width;
    var iconTextGap = 2;
    var total = indent + plusMinusBtnWidth + iconWidth + iconTextGap + textWidth;
    foreach (TreeNode child in node.Nodes)
      total = Math.Max(total, MeasureNodeWidthFast(child));
    return total;
  }
}

// File Tree
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Dumper.Plugins;

namespace Dumper;

public partial class FileTree : UserControl {
  public event EventHandler<OvlFile>? ResourceSelected;
  public event EventHandler<OvlFile>? ExportResource;
  public event EventHandler<OvlFile>? ShowResourceProperties;

  private Dictionary<TreeNode, OvlFile> _nodeEntries = [];
  private Ovl? _currentOvl;
  private PluginManager _pluginManager;
  private ImageList? _imageList;

  public FileTree() {
    InitializeComponent();
    _pluginManager = new PluginManager();
  }

  internal void Initialize(PluginManager pluginManager, ImageList imageList) {
    _pluginManager = pluginManager;
    _imageList = imageList;
    treeView.ImageList = _imageList;
  }

  public void LoadOvl(Ovl ovl) {
    _currentOvl = ovl;
    _nodeEntries.Clear();
    treeView.BeginUpdate();
    treeView.Nodes.Clear();

    if (ovl.Keys.Count > 0) {
      BuildFileNodes(ovl);
    } else {
      BuildFallbackNode(ovl);
    }

    ExpandTreeNodes();
    treeView.EndUpdate();
  }

  private void BuildFileNodes(Ovl ovl) {
    var entriesByFile = ovl.Keys
        .GroupBy(e => e.Path)
        .ToDictionary(g => g.Key, g => g.ToList());

    var fileNames = entriesByFile.Keys
        .OrderBy(f => f.Contains(".unique.") ? 1 : 0)
        .ToList();

    if (fileNames.Count == 0)
        fileNames.Add(Path.GetFileName(ovl.Keys.First().Path));

    foreach (var fileName in fileNames)
        BuildFileNode(fileName, entriesByFile);
  }

  private void BuildFileNode(string fileName, Dictionary<string, List<OvlFile>> entriesByFile) {
    var fileNode = treeView.Nodes.Add(fileName, Path.GetFileName(fileName));
    fileNode.ImageKey = "FolderOpen";
    fileNode.SelectedImageKey = "FolderOpen";

    if (!entriesByFile.TryGetValue(fileName, out var entries)) return;

    var resolved = ResolveEntries(entries);

    var resourceGroups = BuildResourceGroups(resolved);
    var groupedNames = new HashSet<string>(
        resourceGroups.Values.SelectMany(g => g).Select(r => r.DisplayName));

    var remainingEntries = resolved
        .Where(r => !groupedNames.Contains(r.DisplayName))
        .ToList();

    var groupsByName = remainingEntries
        .GroupBy(r => r.DisplayName)
        .Where(g => g.Count() > 1)
        .ToDictionary(g => g.Key, g => g.ToList());
    var duplicateNames = new HashSet<string>(groupsByName.Keys);

    AddAnimatedTextureNodes(fileNode, resolved, resourceGroups, groupedNames);
    AddDuplicateNameNodes(fileNode, groupsByName);
    AddLeafNodes(fileNode, remainingEntries, duplicateNames);
  }

  private static List<OvlEntryViewModel> ResolveEntries(List<OvlFile> entries) =>
    entries.Select(entry => {
        var colonIdx = entry.ToString().LastIndexOf(':');
        if (colonIdx >= 0)
            return new OvlEntryViewModel(entry, entry.ToString()[..colonIdx], entry.ToString()[(colonIdx + 1)..].ToFileType());
        return new OvlEntryViewModel(entry, entry.ToString(), entry.Type);
    }).ToList();

  private static Dictionary<string, List<OvlEntryViewModel>> BuildResourceGroups(List<OvlEntryViewModel> resolved) =>
    resolved
        .Where(r => (r.SymbolType == FileType.Texture || r.SymbolType == FileType.Flic)
                    && EndsWithDigit(r.DisplayName))
        .GroupBy(r => StripTrailingDigits(r.DisplayName))
        .Where(g => g.Count() > 1)
        .ToDictionary(g => g.Key, g => g.OrderBy(r => r.DisplayName).ToList());

  private void AddAnimatedTextureNodes(
    TreeNode fileNode,
    List<OvlEntryViewModel> resolved,
    Dictionary<string, List<OvlEntryViewModel>> resourceGroups,
    HashSet<string> groupedNames) {
    foreach (var viewModel in resolved) {
        if (!groupedNames.Contains(viewModel.DisplayName)) continue;

        var baseName = StripTrailingDigits(viewModel.DisplayName);
        if (!resourceGroups.Remove(baseName, out var group)) continue;

        var parentNode = fileNode.Nodes.Add(baseName);
        parentNode.ImageKey = FileType.Flic.ToIconName();
        parentNode.SelectedImageKey = FileType.Flic.ToIconName();
        parentNode.Tag = FileType.Flic;
        parentNode.ToolTipText =
            $"Animated texture ({group.Count} frames) \u00b7 Loader: {viewModel.SymbolType.ToDisplayName()}";

        foreach (var frame in group) {
            var childNode = parentNode.Nodes.Add(frame.DisplayName[baseName.Length..]);
            childNode.ImageKey = FileType.Texture.ToIconName();
            childNode.SelectedImageKey = FileType.Texture.ToIconName();
            childNode.ToolTipText = frame.ToolTip;
            childNode.Tag = frame.SymbolType;
            _nodeEntries[childNode] = frame.Entry;
        }
    }
  }

  private void AddDuplicateNameNodes(
    TreeNode fileNode,
    Dictionary<string, List<OvlEntryViewModel>> groupsByName) {
    foreach (var (name, group) in groupsByName) {
        var commonType = group.Select(r => r.SymbolType).Distinct().Count() == 1
            ? group.First().SymbolType
            : FileType.Unknown;

        // Rule 1: No nodes shall be nested under nodes of type "Terrain Type"
        if (commonType == FileType.TerrainType) {
            foreach (var viewModel in group) {
                var node = fileNode.Nodes.Add(name);
                node.ImageKey = viewModel.IconKey;
                node.SelectedImageKey = viewModel.IconKey;
                node.Tag = viewModel.SymbolType;
                node.ToolTipText = viewModel.ToolTip;
                _nodeEntries[node] = viewModel.Entry;
            }
            continue;
        }

        var parentNode = fileNode.Nodes.Add($"{group.Count} {Pluralize(commonType.ToDisplayName())}");
        parentNode.ImageKey = commonType == FileType.Unknown ? "FileMultipleOutline" : commonType.ToGroupIconName();
        parentNode.SelectedImageKey = parentNode.ImageKey;
        parentNode.Tag = commonType;
        parentNode.ToolTipText = $"{group.Count} entries named \"{name}\"";

        foreach (var viewModel in group) {
            var childNode = parentNode.Nodes.Add(name);
            childNode.ImageKey = viewModel.IconKey;
            childNode.SelectedImageKey = viewModel.IconKey;
            childNode.Tag = viewModel.SymbolType;
            childNode.ToolTipText = viewModel.ToolTip;
            _nodeEntries[childNode] = viewModel.Entry;
        }
    }
  }

  private void AddLeafNodes(
    TreeNode fileNode,
    List<OvlEntryViewModel> remainingEntries,
    HashSet<string> duplicateNames) {
    foreach (var viewModel in remainingEntries) {
        if (duplicateNames.Contains(viewModel.DisplayName)) continue;

        var node = fileNode.Nodes.Add(viewModel.DisplayName);
        node.ImageKey = viewModel.IconKey;
        node.SelectedImageKey = viewModel.IconKey;
        node.Tag = viewModel.SymbolType;
        node.ToolTipText = viewModel.ToolTip;
        _nodeEntries[node] = viewModel.Entry;
    }
  }

  private void BuildFallbackNode(Ovl ovl) {
    var fileName = Path.GetFileName(ovl.Keys.First().Path);
    var root = treeView.Nodes.Add(fileName, fileName);
    root.ImageKey = "FolderOpen";
    root.SelectedImageKey = "FolderOpen";
    foreach (var header in ovl.Keys) {
        var node = root.Nodes.Add(header.Name);
        node.ImageKey = header.Type.ToIconName();
        node.SelectedImageKey = node.ImageKey;
        node.Tag = header.Type;
        node.ToolTipText = header.Type.ToDisplayName();
    }
  }

  private void ExpandTreeNodes() {
    foreach (TreeNode fileNode in treeView.Nodes) {
        fileNode.Expand();
        foreach (TreeNode child in fileNode.Nodes) {
            if (child.Tag is FileType ft && ft != FileType.Flic && !IsDuplicateGroup(child.Text))
                child.Expand();
        }
    }
  }

  private void treeView_AfterSelect(object? sender, TreeViewEventArgs e) {
    if (e.Node == null || _currentOvl == null) {
      ResourceSelected?.Invoke(this, null!);
      return;
    }

    if (e.Node.Parent == null) {
      // Root node selected
      ResourceSelected?.Invoke(this, null!);
      return;
    }

    var fileType = e.Node.Tag as FileType?;
    if (fileType == null) {
      ResourceSelected?.Invoke(this, null!);
      return;
    }

    if (_nodeEntries.TryGetValue(e.Node, out var entry)) {
        ResourceSelected?.Invoke(this, entry);
    } else {
        ResourceSelected?.Invoke(this, null!);
    }
  }

  private void treeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e) {
    if (e.Button != MouseButtons.Right) return;

    // Rule 2: All nodes, except root nodes, SHALL open the context menu if right-clicked
    if (e.Node.Parent == null) return;

    var menu = new ContextMenuStrip();
    var fileType = e.Node.Tag as FileType?;

    if (fileType.HasValue) {
      var tag = fileType.Value.ToTagString();
      var viewers = _pluginManager.GetViewers(tag);

      // All nodes, except root nodes, SHALL trigger the viewer iff a viewer is found for the node
      if (viewers.Count > 0 && _nodeEntries.ContainsKey(e.Node)) {
        var openWith = new ToolStripMenuItem("Open With");
        var defaultViewer = _pluginManager.GetDefaultViewer(tag);
        foreach (var viewer in viewers) {
          var viewerItem = new ToolStripMenuItem($"{viewer.Name}  v {viewer.Version}") {
            Tag = viewer,
            Font = viewer == defaultViewer
              ? new Font(menu.Font, FontStyle.Bold)
              : menu.Font,
          };
          viewerItem.Click += (_, _) => {
            if (_nodeEntries.TryGetValue(e.Node, out var entry)) {
                ResourceSelected?.Invoke(this, entry);
            }
          };
          openWith.DropDownItems.Add(viewerItem);
        }
        openWith.DropDownItems.Add(new ToolStripSeparator());

        var chooseDefault = new ToolStripMenuItem("Choose a default viewer\u2026");
        chooseDefault.Click += (_, _) => {
          using var chooser = new DefaultViewerChooser(_pluginManager.GetRegistrySnapshot());
          if (chooser.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(chooser.SelectedFileTypeTag)) {
            _pluginManager.SetDefaultViewer(chooser.SelectedFileTypeTag, chooser.SelectedDefaultPlugin!);
            if (tag == chooser.SelectedFileTypeTag && _nodeEntries.TryGetValue(e.Node, out var entry)) {
                ResourceSelected?.Invoke(this, entry);
            }
          }
        };
        openWith.DropDownItems.Add(chooseDefault);
        menu.Items.Add(openWith);
      } else if (viewers.Count > 0) {
        // Allow choosing default viewer even if node doesn't have an entry (e.g. group node)
        var openWith = new ToolStripMenuItem("Open With");
        var chooseDefault = new ToolStripMenuItem("Choose a default viewer\u2026");
        chooseDefault.Click += (_, _) => {
          using var chooser = new DefaultViewerChooser(_pluginManager.GetRegistrySnapshot());
          if (chooser.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(chooser.SelectedFileTypeTag)) {
            _pluginManager.SetDefaultViewer(chooser.SelectedFileTypeTag, chooser.SelectedDefaultPlugin!);
          }
        };
        openWith.DropDownItems.Add(chooseDefault);
        menu.Items.Add(openWith);
      }
    }

    if (_nodeEntries.TryGetValue(e.Node, out var exportEntry)) {
      var export = new ToolStripMenuItem("Export");
      export.Click += (_, _) => ExportResource?.Invoke(this, exportEntry);
      menu.Items.Add(export);

      menu.Items.Add(new ToolStripSeparator());

      var properties = new ToolStripMenuItem("Properties");
      properties.Click += (_, _) => ShowResourceProperties?.Invoke(this, exportEntry);
      menu.Items.Add(properties);
    }

    if (menu.Items.Count > 0) {
      menu.Show(treeView, e.Location);
    }
  }

  public int CountLeafNodes() => CountLeafNodes(treeView.Nodes);

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

  // --- Helper Methods from MainForm ---

  private static bool EndsWithDigit(string name) => name.Length > 0 && char.IsDigit(name[^1]);

  private static string StripTrailingDigits(string name) {
    var i = name.Length - 1;
    while (i >= 0 && char.IsDigit(name[i])) i--;
    return name[..(i + 1)];
  }

  private static string Pluralize(string word) {
    if (word.EndsWith('y'))
      return string.Concat(word.AsSpan(0, word.Length - 1), "ies");
    if (word.EndsWith('s') || word.EndsWith("sh") || word.EndsWith("ch") || word.EndsWith('x') || word.EndsWith('z'))
      return word + "es";
    return word + "s";
  }

  private static bool IsDuplicateGroup(string text) =>
    text.Length > 0 && char.IsDigit(text[0]) && text.Contains(' ') && !text.Contains('.');
}

class OvlEntryViewModel {
    public OvlFile Entry { get; }
    public string DisplayName { get; }
    public FileType SymbolType { get; }

    public OvlEntryViewModel(OvlFile entry, string displayName, FileType symbolType) {
        Entry = entry;
        DisplayName = displayName;
        SymbolType = symbolType;
    }

    public string IconKey => SymbolType == FileType.Flic
                                 ? FileType.Texture.ToIconName()
                                 : SymbolType.ToIconName();
    public string ToolTip => SymbolType.ToDisplayName();
}

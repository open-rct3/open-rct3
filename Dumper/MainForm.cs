// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.
using Dumper.Models;
using OVL;
using OVL.Files;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dumper;

public partial class MainForm : Form {
  static readonly string ready = "Ready";
  static readonly string openingArchive = "Opening archive…";

  public MainForm() {
    InitializeComponent();
    InitializeComponentIcons();
    splitView.MouseDoubleClick += Splitter_MouseDoubleClick;
  }

  private async Task OpenOvl() {
    switch (openDialog.ShowDialog()) {
      case DialogResult.OK:
        this.Cursor = Cursors.WaitCursor;
        LoadOvl(await Task.Run(() => Ovl.Open(openDialog.FileName)));
        this.Cursor = Cursors.Default;
        break;
    }
  }

  private void LoadOvl(Ovl ovl) {
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
        var frameGroups = resolved
          .Where(r => EndsWithDigit(r.displayName))
          .GroupBy(r => StripTrailingDigits(r.displayName))
          .Where(g => g.Count() > 1)
          .ToDictionary(g => g.Key, g => g.OrderBy(r => r.displayName).ToList());
        var groupedNames = new HashSet<string>(
          frameGroups.Values.SelectMany(g => g).Select(r => r.displayName));

        foreach (var (entry, displayName, loaderFileType, symbolFileType) in resolved) {
          // Animated texture group — add parent node once, children are number suffixes
          if (groupedNames.Contains(displayName)) {
            var baseName = StripTrailingDigits(displayName);
            if (!frameGroups.ContainsKey(baseName)) continue; // already added

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
            }

            frameGroups.Remove(baseName);
            continue;
          }

          // Non-numbered flic entries use bitmap icon; others use their default icon
          var iconKey = loaderFileType == FileType.Flic && !EndsWithDigit(displayName)
            ? FileType.Texture.ToIconName()
            : loaderFileType.ToIconName();
          var tooltip = BuildEntryTooltip(symbolFileType, loaderFileType);

          var node = fileNode.Nodes.Add(displayName);
          node.ImageKey = iconKey;
          node.SelectedImageKey = iconKey;
          node.Tag = loaderFileType;
          node.ToolTipText = tooltip;
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
        if (child.Tag is FileType ft && ft != FileType.Flic) {
          child.Expand();
        }
      }
    }
    treeView.EndUpdate();
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

  private void EnsureTreeImageList() {
    if (treeView.ImageList != null) return;

    var icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    var color = new DuoToneColor(
      System.Drawing.Color.FromArgb(64, 64, 64), System.Drawing.Color.FromArgb(185, 185, 185));
    var folderColor = new DuoToneColor(
      System.Drawing.Color.FromArgb(0xC8, 0xA2, 0x17), System.Drawing.Color.FromArgb(0xC8, 0xA2, 0x17));

    var imageList = new ImageList { ImageSize = new Size(IconSize, IconSize) };

    // Add folder icon for file group nodes
    imageList.Images.Add("FolderOpen", RenderIcon(icons, "FolderOpen", folderColor)!);

    // Add icons for each file type, skipping unknown icon names
    foreach (var fileType in Enum.GetValues<FileType>()) {
      var iconName = fileType.ToIconName();
      if (!imageList.Images.ContainsKey(iconName)) {
        var bmp = RenderIcon(icons, iconName, color);
        if (bmp != null)
          imageList.Images.Add(iconName, bmp);
      }
    }

    treeView.ImageList = imageList;
  }

  private async void openToolStripMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private async void openArchiveToolStripButton_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
    Application.Exit();
  }

  private void Splitter_MouseDoubleClick(object? sender, MouseEventArgs e) {
    if (treeView.Nodes.Count == 0) return;

    // Save expansion state of all nodes
    var savedState = new Dictionary<string, bool>();
    foreach (TreeNode node in treeView.Nodes)
      SaveExpansionState(node, savedState);

    // Expand all to measure
    treeView.ExpandAll();

    // Find max width across all visible nodes
    int maxWidth = 0;
    foreach (TreeNode node in treeView.Nodes)
      maxWidth = Math.Max(maxWidth, MeasureNodeWidth(node));

    // Restore expansion state
    foreach (TreeNode node in treeView.Nodes)
      RestoreExpansionState(node, savedState);

    // Set splitter distance to fit content, clamped to valid range
    var padding = SystemInformation.VerticalScrollBarWidth + 8;
    var target = Math.Min(maxWidth + padding, splitView.Width - splitView.Panel2MinSize);
    target = Math.Max(target, splitView.Panel1MinSize);
    splitView.SplitterDistance = target;
  }

  private static void SaveExpansionState(TreeNode node, Dictionary<string, bool> state) {
    state[node.FullPath] = node.IsExpanded;
    foreach (TreeNode child in node.Nodes)
      SaveExpansionState(child, state);
  }

  private static void RestoreExpansionState(TreeNode node, Dictionary<string, bool> state) {
    if (state.TryGetValue(node.FullPath, out var expanded)) {
      if (expanded) node.Expand(); else node.Collapse();
    }
    foreach (TreeNode child in node.Nodes)
      RestoreExpansionState(child, state);
  }

  private static int MeasureNodeWidth(TreeNode node) {
    // TreeNode.Bounds includes indent and icon for visible nodes
    int maxWidth = node.Bounds.Right;
    foreach (TreeNode child in node.Nodes)
      maxWidth = Math.Max(maxWidth, MeasureNodeWidth(child));
    return maxWidth;
  }
}

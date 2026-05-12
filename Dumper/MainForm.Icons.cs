// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Drawing;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper;

public partial class MainForm {
  internal readonly Size IconSize = new(Icons.DefaultSize, Icons.DefaultSize);

  private void InitializeComponentIcons() {
    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();

    menuStrip.ImageScalingSize = IconSize;
    toolStrip.ImageScalingSize = IconSize;

    // File
    openToolStripMenuItem.Image = Icons.Render(icons, "FolderOpen", Icons.Folder);
    exportToolStripMenuItem.Image = Icons.Render(icons, "ExportVariant");
    exitToolStripMenuItem.Image = Icons.Render(icons, "ExitToApp", Icons.Danger);

    // Edit
    undoToolStripMenuItem.Image = Icons.Render(icons, "Undo");
    redoToolStripMenuItem.Image = Icons.Render(icons, "Redo");
    cutToolStripMenuItem.Image = Icons.Render(icons, "ContentCut");
    copyToolStripMenuItem.Image = Icons.Render(icons, "ContentCopy");
    pasteToolStripMenuItem.Image = Icons.Render(icons, "ContentPaste");
    selectAllToolStripMenuItem.Image = Icons.Render(icons, "SelectAll");

    // Tools
    pluginsToolStripMenuItem.Image = Icons.Render(icons, "PuzzleOutline");
    optionsToolStripMenuItem.Image = Icons.Render(icons, "Cog");

    // Toolbar
    openArchiveToolStripButton.Image = Icons.Render(icons, "FolderOpen", Icons.Folder);
    helpToolStripButton.Image = Icons.Render(icons, "HelpCircle", Icons.Blue);
  }
}

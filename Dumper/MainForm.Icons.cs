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

    var defaultColor = new DuoToneColor(Color.FromArgb(64, 64, 64), Color.FromArgb(185, 185, 185));
    var folderColor = new DuoToneColor(Color.FromArgb(0xC8, 0xA2, 0x17), Color.FromArgb(0xC8, 0xA2, 0x17));
    var exitColor = new DuoToneColor(Color.FromArgb(0xD3, 0x2F, 0x2F), Color.Transparent);
    var helpColor = new DuoToneColor(Color.FromArgb(0x19, 0x76, 0xD2), Color.White);

    // File
    openToolStripMenuItem.Image = Icons.Render(icons, "FolderOpen", folderColor);
    exportToolStripMenuItem.Image = Icons.Render(icons, "ExportVariant", defaultColor);
    exitToolStripMenuItem.Image = Icons.Render(icons, "ExitToApp", exitColor);

    // Edit
    undoToolStripMenuItem.Image = Icons.Render(icons, "Undo", defaultColor);
    redoToolStripMenuItem.Image = Icons.Render(icons, "Redo", defaultColor);
    cutToolStripMenuItem.Image = Icons.Render(icons, "ContentCut", defaultColor);
    copyToolStripMenuItem.Image = Icons.Render(icons, "ContentCopy", defaultColor);
    pasteToolStripMenuItem.Image = Icons.Render(icons, "ContentPaste", defaultColor);
    selectAllToolStripMenuItem.Image = Icons.Render(icons, "SelectAll", defaultColor);

    // Tools
    pluginsToolStripMenuItem.Image = Icons.Render(icons, "PuzzleOutline", defaultColor);
    optionsToolStripMenuItem.Image = Icons.Render(icons, "Cog", defaultColor);

    // Toolbar
    openArchiveToolStripButton.Image = Icons.Render(icons, "FolderOpen", folderColor);
    helpToolStripButton.Image = Icons.Render(icons, "HelpCircle", helpColor);
  }
}

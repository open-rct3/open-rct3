// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper;

public partial class MainForm {
  private const int IconSize = 16;

  private void InitializeComponentIcons() {
    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();

    menuStrip.ImageScalingSize = new Size(IconSize, IconSize);
    toolStrip.ImageScalingSize = new Size(IconSize, IconSize);

    var defaultColor = new DuoToneColor(Color.FromArgb(64, 64, 64), Color.FromArgb(185, 185, 185));
    var folderColor = new DuoToneColor(Color.FromArgb(0xC8, 0xA2, 0x17), Color.FromArgb(0xC8, 0xA2, 0x17));
    var exitColor = new DuoToneColor(Color.FromArgb(0xD3, 0x2F, 0x2F), Color.Transparent);
    var helpColor = new DuoToneColor(Color.FromArgb(0x19, 0x76, 0xD2), Color.White);

    // File
    openToolStripMenuItem.Image = RenderIcon(icons, "FolderOpen", folderColor);
    exportToolStripMenuItem.Image = RenderIcon(icons, "ExportVariant", defaultColor);
    exitToolStripMenuItem.Image = RenderIcon(icons, "ExitToApp", exitColor);

    // Edit
    undoToolStripMenuItem.Image = RenderIcon(icons, "Undo", defaultColor);
    redoToolStripMenuItem.Image = RenderIcon(icons, "Redo", defaultColor);
    cutToolStripMenuItem.Image = RenderIcon(icons, "ContentCut", defaultColor);
    copyToolStripMenuItem.Image = RenderIcon(icons, "ContentCopy", defaultColor);
    pasteToolStripMenuItem.Image = RenderIcon(icons, "ContentPaste", defaultColor);
    selectAllToolStripMenuItem.Image = RenderIcon(icons, "SelectAll", defaultColor);

    // Tools
    pluginsToolStripMenuItem.Image = RenderIcon(icons, "Puzzle", defaultColor);
    optionsToolStripMenuItem.Image = RenderIcon(icons, "Cog", defaultColor);

    // Toolbar
    openArchiveToolStripButton.Image = RenderIcon(icons, "FolderOpen", folderColor);
    helpToolStripButton.Image = RenderIcon(icons, "HelpCircle", helpColor);
  }

  internal static Bitmap? RenderIcon(IEmbeddedIcons icons, string name, DuoToneColor color) {
    var icon = icons.GetIcon(name);
    if (icon == null) return null;
    var bmp = new Bitmap(IconSize, IconSize);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    icon.DrawIcon(g, color, 0, 0, IconSize);
    return bmp;
  }
}

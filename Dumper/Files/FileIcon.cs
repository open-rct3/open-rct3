// FileIcon
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using OVL.Files;

namespace Dumper.Files;

public static class Icons {
  public const string Unknown = "questionmark.square.dashed";
  public const string Text = "textformat.size.smaller";
  public const string Number = "number";
  public const string Image = "photo";
  public const string Scenery = "cube";
  public const string Swatch = "swatchpalette";
  public const string Gui = "macwindow";
}

public static class FileTypeExtensions {
  public static string ToIconName(this FileType type) {
    switch (type) {
      case FileType.Text: return Icons.Text;
      case FileType.Integer: return Icons.Number;
      case FileType.Texture:
      case FileType.Flic:
        return Icons.Image;
      case FileType.FlexibleTexture: return Icons.Swatch;
      case FileType.GuiSkinItem: return Icons.Gui;
      case FileType.SceneryItem: return Icons.Scenery;
      default: return Icons.Unknown;
    }
  }
}

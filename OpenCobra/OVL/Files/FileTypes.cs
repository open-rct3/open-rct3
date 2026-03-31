// FileTypes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
namespace OVL.Files;

/// <summary>
/// Files known to Frontier Graphics Development Kit and RCT3, c. 2004.
/// </summary>
public enum FileType : ushort {
  Unknown = 0,
  /// <summary>
  /// Text (txt)
  /// </summary>
  Text,
  /// <summary>
  /// Integer Number (int)
  /// </summary>
  Integer,
  /// <summary>
  /// 2D Texture (tex)
  /// </summary>
  Texture,
  /// <summary>
  /// Compressed 2D Image (flic)
  /// </summary>
  Flic,
  /// <summary>
  /// Flexi-Texture (flt)
  /// </summary>
  FlexibleTexture,
  /// <summary>
  /// GUI Skin Item (gsi)
  /// </summary>
  GuiSkinItem,
  /// <summary>
  /// Scenery Item (sid)
  /// </summary>
  SceneryItem,
  /// <summary>
  /// Bitmap Table (btbl)
  /// </summary>
  BitmapTable
}

/// <summary>Extension methods for working with <see cref="FileType"/>.</summary>
public static class StringExtensions {
  /// <summary>Convert an OVL file type tag string (e.g. <c>"tex"</c>) to the corresponding <see cref="FileType"/>.</summary>
  public static FileType ToFileType(this string extension) {
    switch (extension) {
      case "txt": return FileType.Text;
      case "int": return FileType.Integer;
      case "tex": return FileType.Texture;
      case "flic": return FileType.Flic;
      case "flt": return FileType.FlexibleTexture;
      case "gsi": return FileType.GuiSkinItem;
      case "sid": return FileType.SceneryItem;
      case "btbl": return FileType.BitmapTable;
      default: return FileType.Unknown;
    }
  }

  /// <summary>Returns a human-readable display name for the given <see cref="FileType"/>.</summary>
  public static string ToDisplayName(this FileType type) {
    switch (type) {
      case FileType.Unknown: return "Unknown";
      case FileType.Text: return "Text";
      case FileType.Integer: return "Integer Number";
      case FileType.Texture: return "2D Texture";
      case FileType.Flic: return "Compressed 2D Image";
      case FileType.FlexibleTexture: return "Flexi-Texture";
      case FileType.GuiSkinItem: return "GUI Skin Item";
      case FileType.SceneryItem: return "Scenery Item";
      case FileType.BitmapTable: return "Bitmap Table";
      default: return "Unknown";
    }
  }
}

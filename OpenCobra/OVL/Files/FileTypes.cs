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
  SceneryItem
}

public static class StringExtensions {
  public static FileType ToFileType(this string extension) {
    switch (extension) {
      case "txt": return FileType.Text;
      case "int": return FileType.Integer;
      case "tex": return FileType.Texture;
      case "flic": return FileType.Flic;
      case "flt": return FileType.FlexibleTexture;
      case "gsi": return FileType.GuiSkinItem;
      case "sid": return FileType.SceneryItem;
      default: return FileType.Unknown;
    }
  }
}

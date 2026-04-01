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
  /// <summary>Text (txt)</summary>
  Text,
  /// <summary>Integer Number (int)</summary>
  Integer,
  /// <summary>2D Texture (tex)</summary>
  Texture,
  /// <summary>Compressed 2D Image (flic)</summary>
  Flic,
  /// <summary>Flexi-Texture (ftx)</summary>
  FlexibleTexture,
  /// <summary>GUI Skin Item (gsi)</summary>
  GuiSkinItem,
  /// <summary>Scenery Item (sid)</summary>
  SceneryItem,
  /// <summary>Bitmap Table (btbl)</summary>
  BitmapTable,
  /// <summary>Animated Ride (anr)</summary>
  AnimatedRide,
  /// <summary>Bone Animation (ban)</summary>
  BoneAnim,
  /// <summary>Bone Shape (bsh)</summary>
  BoneShape,
  /// <summary>Carried Item Extra (ced)</summary>
  CarriedItemExtra,
  /// <summary>Changing Room (chg)</summary>
  ChangingRoom,
  /// <summary>Carried Item (cid)</summary>
  CarriedItem,
  /// <summary>Manifold Mesh (mam)</summary>
  ManifoldMesh,
  /// <summary>Path Type (ptd)</summary>
  PathType,
  /// <summary>Queue Type (qtd)</summary>
  QueueType,
  /// <summary>Ride Car (ric)</summary>
  RideCar,
  /// <summary>Ride Train (rit)</summary>
  RideTrain,
  /// <summary>Special Attraction (sat)</summary>
  SpecialAttraction,
  /// <summary>Static Shape (shs)</summary>
  StaticShape,
  /// <summary>Sound (snd)</summary>
  Sound,
  /// <summary>Spline (spl)</summary>
  Spline,
  /// <summary>Stall (sta)</summary>
  Stall,
  /// <summary>Scenery Item Visual (svd)</summary>
  SceneryItemVisual,
  /// <summary>Terrain Type (ter)</summary>
  TerrainType,
  /// <summary>Track Section (tks)</summary>
  TrackSection,
  /// <summary>Tracked Ride (trr)</summary>
  TrackedRide,
  /// <summary>Wild Animal Item (wai)</summary>
  WildAnimalItem
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
      case "ftx": return FileType.FlexibleTexture;
      case "flt": return FileType.FlexibleTexture;
      case "gsi": return FileType.GuiSkinItem;
      case "sid": return FileType.SceneryItem;
      case "btbl": return FileType.BitmapTable;
      case "anr": return FileType.AnimatedRide;
      case "ban": return FileType.BoneAnim;
      case "bsh": return FileType.BoneShape;
      case "ced": return FileType.CarriedItemExtra;
      case "chg": return FileType.ChangingRoom;
      case "cid": return FileType.CarriedItem;
      case "mam": return FileType.ManifoldMesh;
      case "ptd": return FileType.PathType;
      case "qtd": return FileType.QueueType;
      case "ric": return FileType.RideCar;
      case "rit": return FileType.RideTrain;
      case "sat": return FileType.SpecialAttraction;
      case "shs": return FileType.StaticShape;
      case "snd": return FileType.Sound;
      case "spl": return FileType.Spline;
      case "sta": return FileType.Stall;
      case "svd": return FileType.SceneryItemVisual;
      case "ter": return FileType.TerrainType;
      case "tks": return FileType.TrackSection;
      case "trr": return FileType.TrackedRide;
      case "wai": return FileType.WildAnimalItem;
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
      case FileType.AnimatedRide: return "Animated Ride";
      case FileType.BoneAnim: return "Bone Animation";
      case FileType.BoneShape: return "Bone Shape";
      case FileType.CarriedItemExtra: return "Carried Item Extra";
      case FileType.ChangingRoom: return "Changing Room";
      case FileType.CarriedItem: return "Carried Item";
      case FileType.ManifoldMesh: return "Manifold Mesh";
      case FileType.PathType: return "Path Type";
      case FileType.QueueType: return "Queue Type";
      case FileType.RideCar: return "Ride Car";
      case FileType.RideTrain: return "Ride Train";
      case FileType.SpecialAttraction: return "Special Attraction";
      case FileType.StaticShape: return "Static Shape";
      case FileType.Sound: return "Sound";
      case FileType.Spline: return "Spline";
      case FileType.Stall: return "Stall";
      case FileType.SceneryItemVisual: return "Scenery Item Visual";
      case FileType.TerrainType: return "Terrain Type";
      case FileType.TrackSection: return "Track Section";
      case FileType.TrackedRide: return "Tracked Ride";
      case FileType.WildAnimalItem: return "Wild Animal Item";
      default: return "Unknown";
    }
  }

  /// <summary>Returns a Material Design icon name for the given <see cref="FileType"/>.</summary>
  public static string ToIconName(this FileType type) {
    switch (type) {
      case FileType.Unknown: return "FileQuestion";
      case FileType.Text: return "TextBoxOutline";
      case FileType.Integer: return "Numeric";
      case FileType.Texture: return "Image";
      case FileType.Flic: return "Filmstrip";
      case FileType.FlexibleTexture: return "Panorama";
      case FileType.GuiSkinItem: return "PaletteOutline";
      case FileType.SceneryItem: return "Tree";
      case FileType.BitmapTable: return "ImageMultiple";
      case FileType.AnimatedRide: return "HorseVariant";
      case FileType.BoneAnim: return "Bone";
      case FileType.BoneShape: return "Bone";
      case FileType.CarriedItemExtra: return "BagPersonalOutline";
      case FileType.ChangingRoom: return "Door";
      case FileType.CarriedItem: return "BagPersonalOutline";
      case FileType.ManifoldMesh: return "CubeOutline";
      case FileType.PathType: return "Routes";
      case FileType.QueueType: return "Routes";
      case FileType.RideCar: return "CarSide";
      case FileType.RideTrain: return "Train";
      case FileType.SpecialAttraction: return "FerrisWheel";
      case FileType.StaticShape: return "ShapeOutline";
      case FileType.Sound: return "VolumeHigh";
      case FileType.Spline: return "VectorPolyline";
      case FileType.Stall: return "StorefrontOutline";
      case FileType.SceneryItemVisual: return "Tree";
      case FileType.TerrainType: return "TextureBox";
      case FileType.TrackSection: return "Train";
      case FileType.TrackedRide: return "Train";
      case FileType.WildAnimalItem: return "Paw";
      default: return "FileQuestion";
    }
  }
}

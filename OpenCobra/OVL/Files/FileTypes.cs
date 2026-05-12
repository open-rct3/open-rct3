// FileTypes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.OVL.Files;

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
public static class FileTypeExtensions {
  /// <summary>Convert an OVL file type tag string (e.g. <c>"tex"</c>) to the corresponding <see cref="FileType"/>.</summary>
  public static FileType ToFileType(this string extension) => extension switch {
    "txt" => FileType.Text,
    "int" => FileType.Integer,
    "tex" => FileType.Texture,
    "flic" => FileType.Flic,
    "ftx" => FileType.FlexibleTexture,
    "flt" => FileType.FlexibleTexture,
    "gsi" => FileType.GuiSkinItem,
    "sid" => FileType.SceneryItem,
    "btbl" => FileType.BitmapTable,
    "anr" => FileType.AnimatedRide,
    "ban" => FileType.BoneAnim,
    "bsh" => FileType.BoneShape,
    "ced" => FileType.CarriedItemExtra,
    "chg" => FileType.ChangingRoom,
    "cid" => FileType.CarriedItem,
    "mam" => FileType.ManifoldMesh,
    "ptd" => FileType.PathType,
    "qtd" => FileType.QueueType,
    "ric" => FileType.RideCar,
    "rit" => FileType.RideTrain,
    "sat" => FileType.SpecialAttraction,
    "shs" => FileType.StaticShape,
    "snd" => FileType.Sound,
    "spl" => FileType.Spline,
    "sta" => FileType.Stall,
    "svd" => FileType.SceneryItemVisual,
    "ter" => FileType.TerrainType,
    "tks" => FileType.TrackSection,
    "trr" => FileType.TrackedRide,
    "wai" => FileType.WildAnimalItem,
    _ => FileType.Unknown,
  };

  public static string ToString(this FileType type) =>
    $"{type.ToDisplayName()} ({type.ToTagString()})";

  /// <summary>Convert a <see cref="FileType"/> to its OVL file type tag string (e.g. <see cref="FileType.Texture"/> → <c>"tex"</c>).</summary>
  public static string ToTagString(this FileType type, bool asExtension = false)
  {
    var tag = type switch {
      FileType.Text => "txt",
      FileType.Integer => "int",
      FileType.Texture => "tex",
      FileType.Flic => "flic",
      FileType.FlexibleTexture => "ftx",
      FileType.GuiSkinItem => "gsi",
      FileType.SceneryItem => "sid",
      FileType.BitmapTable => "btbl",
      FileType.AnimatedRide => "anr",
      FileType.BoneAnim => "ban",
      FileType.BoneShape => "bsh",
      FileType.CarriedItemExtra => "ced",
      FileType.ChangingRoom => "chg",
      FileType.CarriedItem => "cid",
      FileType.ManifoldMesh => "mam",
      FileType.PathType => "ptd",
      FileType.QueueType => "qtd",
      FileType.RideCar => "ric",
      FileType.RideTrain => "rit",
      FileType.SpecialAttraction => "sat",
      FileType.StaticShape => "shs",
      FileType.Sound => "snd",
      FileType.Spline => "spl",
      FileType.Stall => "sta",
      FileType.SceneryItemVisual => "svd",
      FileType.TerrainType => "ter",
      FileType.TrackSection => "tks",
      FileType.TrackedRide => "trr",
      FileType.WildAnimalItem => "wai",
      _ => "",
    };

    return asExtension
      ? tag.Prepend('.').ToArray().ToString() ?? tag
      : tag;
  }

  /// <summary>Returns a human-readable display name for the given <see cref="FileType"/>.</summary>
  public static string ToDisplayName(this FileType type) => type switch {
    FileType.Unknown => "Unknown",
    FileType.Text => "Text",
    FileType.Integer => "Integer Number",
    FileType.Texture => "2D Texture",
    FileType.Flic => "Compressed 2D Image",
    FileType.FlexibleTexture => "Flexi-Texture",
    FileType.GuiSkinItem => "GUI Skin Item",
    FileType.SceneryItem => "Scenery Item",
    FileType.BitmapTable => "Bitmap Table",
    FileType.AnimatedRide => "Animated Ride",
    FileType.BoneAnim => "Bone Animation",
    FileType.BoneShape => "Bone Shape",
    FileType.CarriedItemExtra => "Carried Item Extra",
    FileType.ChangingRoom => "Changing Room",
    FileType.CarriedItem => "Carried Item",
    FileType.ManifoldMesh => "Manifold Mesh",
    FileType.PathType => "Path Type",
    FileType.QueueType => "Queue Type",
    FileType.RideCar => "Ride Car",
    FileType.RideTrain => "Ride Train",
    FileType.SpecialAttraction => "Special Attraction",
    FileType.StaticShape => "Static Shape",
    FileType.Sound => "Sound",
    FileType.Spline => "Spline",
    FileType.Stall => "Stall",
    FileType.SceneryItemVisual => "Scenery Item Visual",
    FileType.TerrainType => "Terrain Type",
    FileType.TrackSection => "Track Section",
    FileType.TrackedRide => "Tracked Ride",
    FileType.WildAnimalItem => "Wild Animal Item",
    _ => "Unknown",
  };

  /// <summary>Returns a Material Design icon name for the given <see cref="FileType"/>.</summary>
  public static string ToIconName(this FileType type) => type switch {
    FileType.Unknown => "FileQuestion",
    FileType.Text => "TextBoxOutline",
    FileType.Integer => "Numeric",
    FileType.Texture => "Image",
    FileType.Flic => "Filmstrip",
    FileType.FlexibleTexture => "Panorama",
    FileType.GuiSkinItem => "PaletteOutline",
    FileType.SceneryItem => "Tree",
    FileType.BitmapTable => "ImageMultiple",
    FileType.AnimatedRide => "HorseVariant",
    FileType.BoneAnim => "Bone",
    FileType.BoneShape => "Bone",
    FileType.CarriedItemExtra => "BagPersonalOutline",
    FileType.ChangingRoom => "Door",
    FileType.CarriedItem => "BagPersonalOutline",
    FileType.ManifoldMesh => "CubeOutline",
    FileType.PathType => "Routes",
    FileType.QueueType => "Routes",
    FileType.RideCar => "CarSide",
    FileType.RideTrain => "Train",
    FileType.SpecialAttraction => "FerrisWheel",
    FileType.StaticShape => "ShapeOutline",
    FileType.Sound => "VolumeHigh",
    FileType.Spline => "VectorPolyline",
    FileType.Stall => "StorefrontOutline",
    FileType.SceneryItemVisual => "Tree",
    FileType.TerrainType => "TextureBox",
    FileType.TrackSection => "Train",
    FileType.TrackedRide => "Train",
    FileType.WildAnimalItem => "Paw",
    _ => "FileQuestion",
  };

  /// <summary>Returns a Material Design icon name suitable for a group of the given <see cref="FileType"/>.</summary>
  public static string ToGroupIconName(this FileType type) => type switch {
    FileType.Texture => "ImageMultiple",
    FileType.Flic => "VideoMultiple",
    FileType.Text => "TextBoxMultipleOutline",
    FileType.SceneryItem or FileType.SceneryItemVisual => "ForestOutline",
    FileType.ManifoldMesh or FileType.StaticShape => "ViewGridOutline",
    FileType.Sound => "SpeakerMultiple",
    FileType.CarriedItem or FileType.CarriedItemExtra => "BagPersonalMultipleOutline",
    FileType.BoneAnim or FileType.BoneShape => "Bone",
    FileType.RideCar or FileType.RideTrain or FileType.TrackedRide => "TrainCar",
    FileType.AnimatedRide => "HorseVariant",
    FileType.BitmapTable => "ImageMultiple",
    FileType.FlexibleTexture => "Panorama",
    FileType.GuiSkinItem => "PaletteMultiple",
    FileType.PathType or FileType.QueueType => "SignDirectionMultiple",
    FileType.Spline => "VectorPolyline",
    FileType.TerrainType => "TextureBox",
    FileType.TrackSection => "TrainCar",
    FileType.SpecialAttraction => "FerrisWheel",
    FileType.Stall => "StorefrontMultiple",
    FileType.WildAnimalItem => "Paw",
    FileType.ChangingRoom => "Door",
    FileType.Integer => "Numeric",
    _ => "FileMultipleOutline",
  };
}

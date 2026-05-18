// FileTypes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using System.Reflection;

namespace OpenCobra.OVL.Files;

/// <summary>
/// Files known to Frontier Graphics Development Kit and RCT3, c. 2004.
/// </summary>
public enum FileType : ushort {
  [Description("Unknown")]
  Unknown = 0,
  /// <summary>Text (txt)</summary>
  [Description("Text")]
  Text,
  /// <summary>Integer Number (int)</summary>
  [Description("Integer Number")]
  Integer,
  /// <summary>2D Texture (tex)</summary>
  [Description("2D Texture")]
  Texture,
  /// <summary>Compressed 2D Image (flic)</summary>
  [Description("Compressed 2D Image")]
  Flic,
  /// <summary>Flexi-Texture (ftx)</summary>
  [Description("Flexi-Texture")]
  FlexibleTexture,
  /// <summary>GUI Skin Item (gsi)</summary>
  [Description("GUI Skin Item")]
  GuiSkinItem,
  /// <summary>Scenery Item (sid)</summary>
  [Description("Scenery Item")]
  SceneryItem,
  /// <summary>Bitmap Table (btbl)</summary>
  [Description("Bitmap Table")]
  BitmapTable,
  /// <summary>Animated Ride (anr)</summary>
  [Description("Animated Ride")]
  AnimatedRide,
  /// <summary>Bone Animation (ban)</summary>
  [Description("Bone Animation")]
  BoneAnim,
  /// <summary>Bone Shape (bsh)</summary>
  [Description("Bone Shape")]
  BoneShape,
  /// <summary>Carried Item Extra (ced)</summary>
  [Description("Carried Item Extra")]
  CarriedItemExtra,
  /// <summary>Changing Room (chg)</summary>
  [Description("Changing Room")]
  ChangingRoom,
  /// <summary>Carried Item (cid)</summary>
  [Description("Carried Item")]
  CarriedItem,
  /// <summary>Manifold Mesh (mam)</summary>
  [Description("Manifold Mesh")]
  ManifoldMesh,
  /// <summary>Path Type (ptd)</summary>
  [Description("Path Type")]
  PathType,
  /// <summary>Queue Type (qtd)</summary>
  [Description("Queue Type")]
  QueueType,
  /// <summary>Ride Car (ric)</summary>
  [Description("Ride Car")]
  RideCar,
  /// <summary>Ride Train (rit)</summary>
  [Description("Ride Train")]
  RideTrain,
  /// <summary>Special Attraction (sat)</summary>
  [Description("Special Attraction")]
  SpecialAttraction,
  /// <summary>Static Shape (shs)</summary>
  [Description("Static Shape")]
  StaticShape,
  /// <summary>Sound (snd)</summary>
  [Description("Sound")]
  Sound,
  /// <summary>Spline (spl)</summary>
  [Description("Spline")]
  Spline,
  /// <summary>Stall (sta)</summary>
  [Description("Stall")]
  Stall,
  /// <summary>Scenery Item Visual (svd)</summary>
  [Description("Scenery Item Visual")]
  SceneryItemVisual,
  /// <summary>Terrain Type (ter)</summary>
  [Description("Terrain Type")]
  TerrainType,
  /// <summary>Track Section (tks)</summary>
  [Description("Track Section")]
  TrackSection,
  /// <summary>Tracked Ride (trr)</summary>
  [Description("Tracked Ride")]
  TrackedRide,
  /// <summary>Wild Animal Item (wai)</summary>
  [Description("Wild Animal Item")]
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
      ? new string([.. tag.Prepend('.')]) ?? tag
      : tag;
  }

  /// <summary>Returns a human-readable display name for the given <see cref="FileType"/>.</summary>
  public static string ToDisplayName(this FileType type) =>
    type.GetAttr<DescriptionAttribute>()?.Description ?? "Unknown";

  private static T? GetAttr<T>(this Enum value) where T : Attribute =>
    value.GetType()
         .GetField(value.ToString())
         ?.GetCustomAttribute<T>();

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
    FileType.TrackSection => "TrainCar",
    FileType.SpecialAttraction => "FerrisWheel",
    FileType.Stall => "StorefrontMultiple",
    FileType.WildAnimalItem => "Paw",
    FileType.ChangingRoom => "Door",
    FileType.Integer => "Numeric",
    _ => "FileMultipleOutline",
  };
}

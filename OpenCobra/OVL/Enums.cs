// Enums
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.OVL;

/// <summary>OVL archive format version.</summary>
public enum Version : uint {
  Unknown = 0,
  One = 1,
  Four = 4,
  Five = 5
}

/// <summary>Expansion pack addon identifier.</summary>
public enum Addon : uint {
  Vanilla = 0x0000,
  Soaked = 0x0001,
  Wild = 0x0002,
  SoakedHi = 0x0200,
  WildHi = 0x0300
}

/// <summary>Combinable recolorability flags for flexi-textures.</summary>
[Flags]
public enum Recolorable : uint {
  None = 0,
  First = 1,
  Second = 2,
  Third = 4
}

/// <summary>Mesh type for a single LOD entry within a Scenery Item Visual (SVD).</summary>
/// <remarks>
/// Corresponds to <c>SVD::LOD_Type</c> in the original C++ source (rct3constants.h).
/// Each <c>SceneryItemVisualLOD</c> struct contains a <c>meshtype</c> field using these values.
/// A single SVD may contain multiple LODs, each with a different mesh type.
/// </remarks>
public enum SvdLodType {
  StaticShape = 0,
  BoneShape = 3,
  Billboard = 4
}

public enum SvdFlags : uint {
  /// <remarks>
  /// Set on Trees, Shrubs, Ferns, and also on lots of walls.
  /// </remarks>
  Greenery = 0x00000001,
  NoShadow = 0x00000002,
  /// <remarks>
  /// Alias for <see cref="NoShadow"/>. Both are explicitly <c>0x00000002</c> in the original
  /// C++ source (rct3constants.h). On flower-type objects (<see cref="SidType.Flowers"/>),
  /// this bit identifies the object as a flower; on all other object types, it suppresses
  /// shadow casting. The functional effect (no shadow) is the same in both contexts.
  /// </remarks>
  Flower = 0x00000002,
  Rotation = 0x00000004,
  Unknown01 = 0x00000010,
  Unknown02 = 0x00000020,
  Unknown03 = 0x00000040,
  AnimatedPreview = 0x00000800,
  UnknownGiantFerris = 0x00001000,
  Soaked = 0x01000000,
  Wild = 0x02000000,
  SoakedOrWild = 0x03000000
}

/// <summary>Scenery item tile positioning.</summary>
public enum SidPosition {
  TileFull = 0,
  PathEdgeInner = 1,
  PathEdgeOuter = 2,
  Wall = 3,
  TileQuarter = 4,
  TileHalf = 5,
  PathCenter = 6,
  Corner = 7,
  PathEdgeJoin = 8
}

[Flags]
public enum SidFlags : uint {
  Unknown01 = 0x00000001,
  /// <summary>
  /// Never set?
  /// </summary>
  Unknown02 = 0x00000002,
  Unknown03 = 0x00000004,
  Unknown04 = 0x00000008,
  DeleteOnGroundChange = 0x00000010,
  /// <summary>
  /// Goes with ground if non-colliding, blocks ground change otherwise.
  /// </summary>
  GroundChange = 0x00000020,
  Unknown07 = 0x00000040,
  Unknown08 = 0x00000080,
  /// <summary>Blocks supports, only from above, may require collision detection.</summary>
  /// <remarks>
  /// Set on terrain blocks and courtyard column.
  /// </remarks>
  SupportBlock = 0x00000100,
  /// <remarks>
  /// <para>Several of Track2-Track6, Track8-Track15, Track18, Track22, Track24, Track26-Track36, Track59, Track60,
  /// TrackBased01-TrackBased04, TrackBased07, TrackBased14, TrackBased21-TrackBased24, TrackBased33, TrackBased37, TrackBased10</para>
  /// <para> Also TrackBased12: SteepSlopeDown</para>
  /// </remarks>
  Unknown10 = 0x00000200,
  /// <remarks>
  /// <para>Few of Track20.</para>
  /// <para>Most of Track3, Track31, Track33_CT, Track34 (those that do not have Flag27), Track35, Track59, TrackBased09, TrackBased22</para>
  /// <para>All sections of Track2, Track4 - Track6, Track8 - Track15, Track18, Track22, Track24, Track26-Track29, Track32, Track36, Track60,
  /// TrackBased01-TrackBased04, TrackBased07-TrackBased08, TrackBased14, TrackBased21, TrackBased23, TrackBased24, TrackBased33, TrackBased37</para>
  /// </remarks>
  Unknown11 = 0x00000400,
  /// <summary>Some sections of Track6, Track9, Track10, Track60; Most sections of Track8, Track12, Track13, TrackBased04</summary>
  Unknown12 = 0x00000800,
  /// <summary>Object skews with ground (like fences).</summary>
  /// <remarks>Might depend on other things?</remarks>
  Skew = 0x00001000,
  /// <summary>Smooth height change, not in increments.</summary>
  SmoothHeight = 0x00002000,
  /// <summary>Some Supports: ts2, ts3</summary>
  Unknown15 = 0x00004000,
  /// <summary>Some Supports: ts5</summary>
  Unknown16 = 0x00008000,
  /// <summary>Removes base of flats and makes them place-able on water only.</summary>
  /// <remarks>Also set on some stations.</remarks>
  OnWaterOnly = 0x00010000,
  /// <summary>Never set</summary>
  Unknown18 = 0x00020000,
  /// <summary>Never set</summary>
  Unknown19 = 0x00040000,
  /// <summary>Peeps leave from end of stop animation instead of idle.</summary>
  RideLeaveAfterStop = 0x00080000,
  /// <summary>Track33_CT, Vertical chain; Trackbased21, Bowl and Funnel</summary>
  Unknown21 = 0x00100000,
  /// <summary>On rides, fence only around parts touching the floor (collision detection)</summary>
  RideExactFence = 0x00200000,
  /// <summary>All ride events</summary>
  Unknown23 = 0x00400000,
  /// <summary>Set for fences and fence-like objects. Purpose unknown.</summary>
  Fence = 0x00800000,
  /// <summary>Jungle Trees</summary>
  Unknown25 = 0x01000000,
  /// <summary>Puts object in billboard menu</summary>
  BillboardMenu = 0x02000000,
  /// <summary>Right part of joint sections for the splitting cosater</summary>
  Unknown27 = 0x04000000,
  /// <summary>Never set?</summary>
  Unknown28 = 0x08000000,
  /// <summary>Never set?</summary>
  Unknown29 = 0x10000000,
  /// <summary>Never set?</summary>
  Unknown30 = 0x20000000,
  /// <summary>Never set?</summary>
  Unknown31 = 0x40000000,
  /// <summary>Never set?</summary>
  Unknown32 = 0x80000000,
}

/// <summary>Per-square flags for scenery item tile occupancy.</summary>
[Flags]
public enum SidSquareFlags {
  /// <summary>
  /// Activates collision detection.
  /// </summary>
  Collision = 0x00000001,
  /// <summary>Show supports.</summary>
  /// <remarks>Needs Collision and the respective setting.</remarks>
  Supports = 0x00000002,
  /// <summary>Never set?</summary>
  Unknown35 = 0x00000004,
  Unknown36 = 0x00000008,
  Unknown37 = 0x00000010,
  Unknown38 = 0x00000020,
  Unknown39 = 0x00000040,
  Unknown40 = 0x00000080,
  Unknown41 = 0x00000100,
  Unknown42 = 0x00000200,
  Unknown43 = 0x00000400,
  Unknown44 = 0x00000800
}

/// <summary>Scenery item type classification.</summary>
public enum SidType {
  Tree = 0,
  Plant = 1,
  Shrub = 2,
  Flowers = 3,
  Fence = 4,
  WallMisc = 5,
  PathLamp = 6,
  ScenerySmall = 7,
  SceneryMedium = 8,
  SceneryLarge = 9,
  SceneryAnamatronic = 10,
  SceneryMisc = 11,
  SupportMiddle = 12,
  SupportTop = 13,
  SupportBottom = 14,
  SupportBottomExtra = 15,
  SupportGirder = 16,
  SupportCap = 17,
  RideTrack = 18,
  Path = 19,
  ParkEntrance = 20,
  Litter = 21,
  GuestInject = 22,
  Ride = 23,
  RideEntrance = 24,
  RideExit = 25,
  KeepClearFence = 26,
  Stall = 27,
  RideEvent = 28,
  Firework = 29,
  LitterBin = 30,
  Bench = 31,
  Sign = 32,
  PhotoPoint = 33,
  WallStraight = 34,
  WallRoof = 35,
  WallCorner = 36,

  #region New for Soaked!
  WaterCannon = 37,
  PoolPiece = 38,
  PoolExtra = 39,
  ChangingRoom = 40,
  LaserDome = 41,
  WaterJet = 42,
  TerrainPiece = 43,
  ParticleEffect = 44,
  #endregion

  #region New for Wild!
  AnimalFence = 45,
  AnimalMisc = 46
  #endregion
}

/// <remarks>TODO: Tracked rides. See <see href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/rct3constants.h#L505"/>.</remarks>

public enum TextureType : uint {
  /// <summary>Regular uncompressed texture.</summary>
  Regular = 0x0D,
  /// <summary>Icon texture, e.g. a GUI icon.</summary>
  Icon = 0x10
}

public enum TextureFormat : uint {
  /// <summary>RGB, no alpha</summary>
  R8G8B8 = 0x01,
  /// <summary>Full RGBA, primary uncompressed target</summary>
  [SeenInGame]
  A8R8G8B8 = 0x02,
  /// <summary>RGB with unused alpha byte</summary>
  X8R8G8B8 = 0x03,
  /// <summary>5-6-5 RGB</summary>
  R5G6B5 = 0x04,
  /// <summary>RGB with 1 unused bit</summary>
  X1R5G5B5 = 0x05,
  /// <summary>8-bit palette index</summary>
  P8 = 0x07,
  /// <summary>1-bit alpha + 15-bit RGB</summary>
  A1R5G5B5 = 0x08,
  /// <summary>4-4-4 RGB with 4 unused bits</summary>
  X4R4G4B4 = 0x09,
  /// <summary>4-bit alpha + 12-bit RGB</summary>
  A4R4G4B4 = 0x0A,
  /// <summary>8-bit luminance (grayscale)</summary>
  L8 = 0x0B,
  /// <summary>8-bit alpha + 8-bit luminance</summary>
  A8L8 = 0x0C,
  /// <summary>Normal map format</summary>
  V8U8 = 0x0E,
  /// <summary>YUV 4:2:2 packed</summary>
  Uyvy = 0x10,
  /// <summary>YUV 4:2:2 packed</summary>
  Yuy2 = 0x11,
  /// <summary>S3TC/DXT1 compression</summary>
  [SeenInGame]
  Dxt1 = 0x12,
  /// <summary>S3TC/DXT3 compression</summary>
  [SeenInGame]
  Dxt3 = 0x13,
  /// <summary>S3TC/DXT5 compression</summary>
  Dxt5 = 0x14,
  /// <summary>3-3-2 RGB</summary>
  R3G3B2 = 0x15,
  /// <summary>Alpha-only</summary>
  A8 = 0x16,
  /// <summary>Depth buffer</summary>
  D16 = 0x100,
  /// <summary>Depth buffer</summary>
  D32 = 0x101,
  /// <summary>Depth + stencil</summary>
  D15S1 = 0x102,
  /// <summary>Depth + stencil</summary>
  D24S8 = 0x103,
}

/// <summary>
/// Whether a the thing annotated has been observed in-game.
/// </summary>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property)]
public class SeenInGameAttribute : Attribute {
  public long[] Values { get; init; } = [];
}

public static class TextureFormatExtensions {
  public static int BitsPerPixel(this TextureFormat format) {
    switch (format) {
      case TextureFormat.Dxt1:
        return 4;
      case TextureFormat.Dxt3:
      case TextureFormat.Dxt5:
      case TextureFormat.R3G3B2:
      case TextureFormat.A8:
      case TextureFormat.L8:
      case TextureFormat.P8:
        return 8;
      case TextureFormat.R5G6B5:
      case TextureFormat.X1R5G5B5:
      case TextureFormat.A1R5G5B5:
      case TextureFormat.X4R4G4B4:
      case TextureFormat.A4R4G4B4:
      case TextureFormat.A8L8:
      case TextureFormat.V8U8:
      case TextureFormat.Uyvy:
      case TextureFormat.Yuy2:
      case TextureFormat.D15S1:
      case TextureFormat.D16:
        return 16;
      case TextureFormat.R8G8B8:
        return 24;
      case TextureFormat.A8R8G8B8:
      case TextureFormat.X8R8G8B8:
      case TextureFormat.D32:
      case TextureFormat.D24S8:
        return 32;
      default:
        throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown texture format");
    }
  }

  public static bool IsCompressed(this TextureFormat format) => format switch {
    TextureFormat.Dxt1 or TextureFormat.Dxt3 or TextureFormat.Dxt5 => true,
    _ => false,
  };

  /// <returns>Size of a single pixel, in bits.</returns>
  /// <exception cref="ArgumentOutOfRangeException">
  /// Thrown when the format is not supported.
  /// </exception>
  public static int BlockSize(this TextureFormat format) => format switch {
    TextureFormat.A8R8G8B8 => 32,
    TextureFormat.Dxt1 => 8,
    TextureFormat.Dxt3 => 16,
    TextureFormat.Dxt5 => 16,
    _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported texture format"),
  };
}

// SceneryItems
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Concurrent;
using System.Numerics;
using NLog;

namespace OpenCobra.OVL.Files;

/// <summary>UI/catalog metadata for a scenery item, resolved from <c>cSidUI</c>.</summary>
public readonly record struct SceneryItemListing(
  string Name,
  string Icon,
  string Group,
  string GroupIcon,
  uint SceneryType,
  int Cost,
  int RemovalCost
);

/// <summary>Footprint/placement data, resolved from <c>cSidPosition</c>.</summary>
public readonly record struct SceneryItemPosition(
  Placement Placement,
  uint XSquares,
  uint ZSquares,
  Vector3 Position,
  Vector3 Size,
  string SupportsRef
);

/// <summary>
/// A single tile's occupancy/height data, resolved from the on-disk 20-byte <c>SceneryItemData</c>
/// array (<c>flags</c>, <c>min_height</c>, <c>max_height</c>, <c>block_bits*</c>, <c>supports</c>) -
/// the <c>block_bits</c> per-tile height bitmask pointer is not modeled here (opaque, unused by callers).
/// </summary>
public readonly record struct SceneryItemTile(
  uint Flags,
  int MinHeight,
  int MaxHeight,
  uint SupportFlags
);

/// <summary>
/// A single sound script's raw, variable-size (8- or 16-byte) commands - see <c>SoundScript</c> in
/// sceneryrevised.h ("repeated till instruction == 0"). Commands are sliced out but not decoded;
/// their individual fields (time/parameters) are not modeled here.
/// </summary>
public readonly record struct SoundScript(
  IReadOnlyList<ReadOnlyMemory<byte>> RawCommands
);

/// <summary>A raw key/value scenery parameter pair; values are not further typed or validated.</summary>
public readonly record struct SceneryParam(
  string Key,
  string Value
);

/// <summary>
/// One entry of a scenery item's per-instance sound list, resolved from the on-disk 16-byte
/// <c>ScenerySound</c> array (<c>sound_count</c>, <c>sound_refs**</c>, <c>sound_script_count</c>,
/// <c>sound_scripts**</c>).
/// </summary>
public readonly record struct SceneryItemSound(
  IReadOnlyList<string> SoundRefs,
  IReadOnlyList<SoundScript> AnimationScripts
);

/// <summary>
/// Version-gated fields from <c>cSidExtra</c>. <see cref="AddonPack"/>/<see cref="GenericAddon"/> come
/// from the 16-byte <c>SceneryItem_Sext</c> block appended when <see cref="Version"/> &gt;= 1;
/// <see cref="Unknown"/>/<see cref="BillboardAspect"/> come from the further 8-byte
/// <c>SceneryItem_Wext</c> block appended only when <see cref="Version"/> == 2.
/// </summary>
public readonly record struct SceneryItemExtra(
  ushort Version,
  Addon AddonPack,
  uint GenericAddon,
  float Unknown,
  uint BillboardAspect
);

/// <summary>
/// A decoded <c>sid</c> (SceneryItem) entry - per rct3-importer's sceneryrevised.h/ManagerSID.cpp,
/// see <see cref="SceneryItems.ReadSid"/> for the on-disk struct layout this mirrors.
/// </summary>
public readonly record struct SceneryItem(
  string Name,
  string OvlPath,
  SceneryItemListing Listing,
  SceneryItemPosition Position,
  uint PrimaryColor,
  uint SecondaryColor,
  uint TertiaryColor,
  IReadOnlyList<SceneryItemTile> Tiles,
  SceneryItemExtra Extra,
  IReadOnlyList<SceneryItemSound> Sounds,
  IReadOnlyList<string> SvdRefs,
  IReadOnlyList<SceneryParam> Parameters,
  IReadOnlyList<string> AnrRefs
);

/// <summary>Decodes "sid" (SceneryItem) entries per rct3-importer's ManagerSID.cpp.</summary>
public static class SceneryItems {
  // On-disk struct sizes (32-bit pointers, no padding) - see sceneryrevised.h:
  //   SceneryItem_V (VStructSize)   base struct, always present
  //   SceneryItem_Sext (SextSize)   sounds_extra*, unk2, addon_pack, generic_addon - appended when
  //                                 structure_version >= 1
  //   SceneryItem_Wext (WextSize)   unkf, billboard_aspect - appended after Sext only when
  //                                 structure_version == 2
  private const int VStructSize = 212;
  private const int SextSize = 16;
  private const int WextSize = 8;
  // r3::Constants::SID::Extra::BillboardAspect::None sentinel (cSidExtra's default ctor: -1 as uint).
  private const uint NoBillboardAspect = 0xFFFFFFFF;

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static IReadOnlyList<SceneryItem> Extract(Ovl ovl) {
    var sidFiles = ovl.Keys.Where(file => file.Type == FileType.SceneryItem).ToList();
    var bag = new ConcurrentBag<SceneryItem>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(sidFiles, file => {
      try {
        bag.Add(ReadSid(ovl, file));
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} scenery items from {x} OVL{suffix}.",
        failures.Count,
        sidFiles.Count,
        sidFiles.Count != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  /// <summary>
  /// Decodes a single <c>sid</c>-tagged symbol without scanning the rest of the archive - for callers
  /// (e.g. Dumper's <c>sid-viewer</c> host integration) that need one item on demand. Returns
  /// <c>null</c> instead of throwing on decode failure, mirroring
  /// <see cref="StaticShapes.TryExtractOne"/>'s per-symbol failure handling.
  /// </summary>
  public static SceneryItem? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      return ReadSid(ovl, file);
    } catch (Exception ex) {
      logger.Error(ex, "Failed to decode {FileName}", file.ToString());
      return null;
    }
  }

  /// <summary>
  /// Reads a <c>sid</c> symbol's 212-byte <c>SceneryItem_V</c> struct (see sceneryrevised.h), plus
  /// the version-gated <c>SceneryItem_Sext</c>/<c>SceneryItem_Wext</c> tail (see
  /// <see cref="SceneryItemExtra"/>).
  /// </summary>
  /// <remarks>
  /// Field offsets within <c>SceneryItem_V</c>:
  /// <list type="table">
  /// <item><term>8</term><description><c>position_type</c> (u16)</description></item>
  /// <item><term>10</term><description><c>structure_version</c> (u16)</description></item>
  /// <item><term>16</term><description><c>squares_x</c></description></item>
  /// <item><term>20</term><description><c>squares_z</c></description></item>
  /// <item><term>28-39</term><description><c>position</c></description></item>
  /// <item><term>40-51</term><description><c>size</c></description></item>
  /// <item><term>60</term><description><c>cost</c> (i32)</description></item>
  /// <item><term>64</term><description><c>removal_cost</c> (i32)</description></item>
  /// <item><term>72</term><description><c>type</c></description></item>
  /// <item><term>76</term><description><c>supports*</c></description></item>
  /// <item><term>80</term><description><c>svd_count</c></description></item>
  /// <item><term>84</term><description><c>svds_ref*</c></description></item>
  /// <item><term>88</term><description><c>icon_ref*</c></description></item>
  /// <item><term>92</term><description><c>group_icon_ref*</c></description></item>
  /// <item><term>96</term><description><c>group_name_ref*</c></description></item>
  /// <item><term>100</term><description><c>ovl_path*</c></description></item>
  /// <item><term>104</term><description><c>param_count</c></description></item>
  /// <item><term>108</term><description><c>params*</c></description></item>
  /// <item><term>112</term><description><c>sound_count</c></description></item>
  /// <item><term>116</term><description><c>sounds*</c></description></item>
  /// <item><term>120</term><description><c>name_ref*</c></description></item>
  /// <item><term>140-151</term><description><c>default_col1-3</c></description></item>
  /// <item><term>184</term><description><c>anr_count</c></description></item>
  /// <item><term>188</term><description><c>individual_animation_anr_names*</c></description></item>
  /// </list>
  /// </remarks>
  private static SceneryItem ReadSid(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var sidAddress))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(sidAddress, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve SceneryItem block for {file.Name}");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, VStructSize);

    var positionType = BitConverter.ToUInt16(span[8..10]);
    var structureVersion = BitConverter.ToUInt16(span[10..12]);
    var squaresX = BitConverter.ToUInt32(span[16..20]);
    var squaresZ = BitConverter.ToUInt32(span[20..24]);
    var position = ReadVector3(span[28..40]);
    var size = ReadVector3(span[40..52]);
    var cost = BitConverter.ToInt32(span[60..64]);
    var removalCost = BitConverter.ToInt32(span[64..68]);
    var sceneryType = BitConverter.ToUInt32(span[72..76]);
    var svdCount = BitConverter.ToUInt32(span[80..84]);
    var paramCount = BitConverter.ToUInt32(span[104..108]);
    var soundCount = BitConverter.ToUInt32(span[112..116]);
    var primaryColor = BitConverter.ToUInt32(span[140..144]);
    var secondaryColor = BitConverter.ToUInt32(span[144..148]);
    var tertiaryColor = BitConverter.ToUInt32(span[148..152]);
    var anrCount = BitConverter.ToUInt32(span[184..188]);

    var addonPack = Addon.Vanilla;
    var genericAddon = 0u;
    var unknownF = 0f;
    var billboardAspect = NoBillboardAspect;
    if (structureVersion >= 1 && o + VStructSize + SextSize <= block.Length) {
      var sextSpan = block.AsSpan(o + VStructSize, SextSize);
      addonPack = (Addon)BitConverter.ToUInt32(sextSpan[8..12]);
      genericAddon = BitConverter.ToUInt32(sextSpan[12..16]);
    }
    if (structureVersion >= 2 && o + VStructSize + SextSize + WextSize <= block.Length) {
      var wextSpan = block.AsSpan(o + VStructSize + SextSize, WextSize);
      unknownF = BitConverter.ToSingle(wextSpan[0..4]);
      billboardAspect = BitConverter.ToUInt32(wextSpan[4..8]);
    }

    // name_ref/icon_ref/group_name_ref/group_icon_ref are assignSymbolReference-driven cross-resource
    // references (ManagerSID.cpp), resolved via the symbol-reference table. In real archives these
    // commonly name a symbol in a *different* OVL file (e.g. a pack-wide shared text/icon catalog), so
    // they legitimately stay empty when only a single item's own common/unique pair is loaded.
    var name = ovl.TryResolveSymbolReference(sidAddress + 120, out var nameFile)
      ? Text.TryExtractOne(ovl, nameFile) ?? string.Empty : string.Empty;
    var icon = ovl.TryResolveSymbolReference(sidAddress + 88, out var iconFile) ? iconFile.Name : string.Empty;
    var group = ovl.TryResolveSymbolReference(sidAddress + 96, out var groupFile)
      ? Text.TryExtractOne(ovl, groupFile) ?? string.Empty : string.Empty;
    var groupIcon = ovl.TryResolveSymbolReference(sidAddress + 92, out var groupIconFile) ? groupIconFile.Name : string.Empty;
    var supportsRef = ovl.TryGetRelocationSource(sidAddress + 76, out var supportsAddress)
      && ovl.TryResolveString(supportsAddress, out var resolvedSupports) ? resolvedSupports : string.Empty;
    var ovlPath = ovl.TryGetRelocationSource(sidAddress + 100, out var ovlPathAddress)
      && ovl.TryResolveString(ovlPathAddress, out var resolvedOvlPath) ? resolvedOvlPath : string.Empty;

    var tiles = ReadTiles(ovl, sidAddress, squaresX * squaresZ);
    var sounds = ReadSounds(ovl, sidAddress, soundCount);
    var svdRefs = ReadSymbolArray(ovl, sidAddress + 84, svdCount);
    var parameters = ReadParams(ovl, sidAddress, paramCount);
    var anrRefs = ReadStringArray(ovl, sidAddress + 188, anrCount);

    var listing = new SceneryItemListing(name, icon, group, groupIcon, sceneryType, cost, removalCost);
    var itemPosition = new SceneryItemPosition((Placement)positionType, squaresX, squaresZ, position, size, supportsRef);
    var extra = new SceneryItemExtra(structureVersion, addonPack, genericAddon, unknownF, billboardAspect);

    return new SceneryItem(
      file.Name, ovlPath, listing, itemPosition, primaryColor, secondaryColor, tertiaryColor,
      tiles, extra, sounds, svdRefs, parameters, anrRefs
    );
  }

  private static List<SceneryItemTile> ReadTiles(Ovl ovl, uint sidAddress, uint tileCount) {
    var tiles = new List<SceneryItemTile>(Convert.ToInt32(tileCount));
    if (tileCount == 0 || !ovl.TryGetRelocationSource(sidAddress + 24, out var dataAddress)
        || !ovl.TryResolveRelocation(dataAddress, out var dataBlock, out var dataOffset)) return tiles;

    var d = Convert.ToInt32(dataOffset);
    for (var i = 0; i < tileCount; i++) {
      var tSpan = dataBlock.AsSpan(d + i * 20, 20);
      tiles.Add(new SceneryItemTile(
        BitConverter.ToUInt32(tSpan[0..4]),
        BitConverter.ToInt32(tSpan[4..8]),
        BitConverter.ToInt32(tSpan[8..12]),
        BitConverter.ToUInt32(tSpan[16..20])
      ));
    }
    return tiles;
  }

  private static List<SceneryItemSound> ReadSounds(Ovl ovl, uint sidAddress, uint soundCount) {
    var sounds = new List<SceneryItemSound>(Convert.ToInt32(soundCount));
    if (soundCount == 0 || !ovl.TryGetRelocationSource(sidAddress + 116, out var soundsAddress)
        || !ovl.TryResolveRelocation(soundsAddress, out var soundsBlock, out var soundsOffset)) return sounds;

    var so = Convert.ToInt32(soundsOffset);
    for (var s = 0; s < soundCount; s++) {
      var entryAddress = soundsAddress + Convert.ToUInt32(s * 16);
      var sSpan = soundsBlock.AsSpan(so + s * 16, 16);
      var refCount = BitConverter.ToUInt32(sSpan[0..4]);
      var scriptCount = BitConverter.ToUInt32(sSpan[8..12]);

      var soundRefs = ReadSymbolArray(ovl, entryAddress + 4, refCount);

      var scripts = new List<SoundScript>(Convert.ToInt32(scriptCount));
      if (scriptCount > 0 && ovl.TryGetRelocationSource(entryAddress + 12, out var scriptArrayAddress)) {
        for (var an = 0; an < scriptCount; an++) {
          var slotAddress = scriptArrayAddress + Convert.ToUInt32(an * 4);
          scripts.Add(ovl.TryGetRelocationSource(slotAddress, out var scriptAddress)
            ? ReadSoundScript(ovl, scriptAddress)
            : new SoundScript([]));
        }
      }

      sounds.Add(new SceneryItemSound(soundRefs, scripts));
    }
    return sounds;
  }

  // Walks variable-size (8- or 16-byte) commands starting at a resolved SoundScript* address,
  // terminated by an instruction == 0 command - see sceneryrevised.h's SoundScript doc comment.
  private static SoundScript ReadSoundScript(Ovl ovl, uint address) {
    var commands = new List<ReadOnlyMemory<byte>>();
    if (!ovl.TryResolveRelocation(address, out var block, out var offset)) return new SoundScript(commands);

    var pos = Convert.ToInt32(offset);
    while (pos + 8 <= block.Length) {
      var instruction = BitConverter.ToUInt32(block, pos + 4);
      var size = instruction is 3 or 4 ? 16 : 8;
      if (pos + size > block.Length) break;
      commands.Add(new ReadOnlyMemory<byte>(block, pos, size));
      pos += size;
      if (instruction == 0) break;
    }
    return new SoundScript(commands);
  }

  private static List<SceneryParam> ReadParams(Ovl ovl, uint sidAddress, uint paramCount) {
    var parameters = new List<SceneryParam>(Convert.ToInt32(paramCount));
    if (paramCount == 0 || !ovl.TryGetRelocationSource(sidAddress + 108, out var paramsArrayAddress)) return parameters;

    for (var i = 0; i < paramCount; i++) {
      var entryAddress = paramsArrayAddress + Convert.ToUInt32(i * 8);
      var key = ovl.TryGetRelocationSource(entryAddress, out var keyAddress)
        && ovl.TryResolveString(keyAddress, out var resolvedKey) ? resolvedKey : string.Empty;
      var value = ovl.TryGetRelocationSource(entryAddress + 4, out var valueAddress)
        && ovl.TryResolveString(valueAddress, out var resolvedValue) ? resolvedValue : string.Empty;
      parameters.Add(new SceneryParam(key, value));
    }
    return parameters;
  }

  // Reads an array of assignSymbolReference-driven slots (e.g. svds_ref, sound_refs): the array
  // pointer field itself is a regular (base) relocation to the array's address, but each element is a
  // direct cross-resource symbol reference, resolved via the symbol-reference table.
  private static List<string> ReadSymbolArray(Ovl ovl, uint fieldAddress, uint count) {
    var refs = new List<string>(Convert.ToInt32(count));
    if (count == 0 || !ovl.TryGetRelocationSource(fieldAddress, out var arrayAddress)) return refs;

    for (var i = 0; i < count; i++) {
      var slotAddress = arrayAddress + Convert.ToUInt32(i * 4);
      if (ovl.TryResolveSymbolReference(slotAddress, out var symFile))
        refs.Add(symFile.Name);
    }
    return refs;
  }

  // Reads an array of relocation-gated plain-string slots (single indirection: each slot resolves
  // directly to a string address, not a symbol) - e.g. individual_animation_anr_names.
  private static List<string> ReadStringArray(Ovl ovl, uint fieldAddress, uint count) {
    var values = new List<string>(Convert.ToInt32(count));
    if (count == 0 || !ovl.TryGetRelocationSource(fieldAddress, out var arrayAddress)) return values;

    for (var i = 0; i < count; i++) {
      var slotAddress = arrayAddress + Convert.ToUInt32(i * 4);
      if (ovl.TryGetRelocationSource(slotAddress, out var strAddress) && ovl.TryResolveString(strAddress, out var value))
        values.Add(value);
    }
    return values;
  }

  private static Vector3 ReadVector3(ReadOnlySpan<byte> span) =>
    new(BitConverter.ToSingle(span[0..4]), BitConverter.ToSingle(span[4..8]), BitConverter.ToSingle(span[8..12]));
}

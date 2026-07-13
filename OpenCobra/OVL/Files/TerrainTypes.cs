// TerrainTypes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes "ter" (TerrainType) entries per rct3-importer's terraintype.h/ManagerTER.cpp.
using System.Collections.Concurrent;
using NLog;

namespace OpenCobra.OVL.Files;

/// <summary>Terrain surface type classification (Ground Unblended/Cliff/Ground Blended).</summary>
public enum TerrainType : uint {
  GroundUnblended = 0,
  Cliff = 1,
  GroundBlended = 2
}

public readonly record struct TerrainParameters(
  uint ColorSimple,
  uint ColorMap,
  float InvWidth,
  float InvHeight
);

public readonly record struct TerrainUnknowns(
  /// <summary>Always 0 across all observed entries. Likely reserved/unused.</summary>
  uint Unk02,
  /// <remarks>
  /// Varies by entry, clustered by apparent visual "roughness" family (0.02 darkest/smoothest rock,
  /// 0.1 sand/dirt, 0.3 grass, 0.5-0.7 rocky/mountainous). All Cliff_* entries share 0.3. Speculative:
  /// a blend-noise scale for the Ground Blended auto-paint system.
  /// Not confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk13,
  /// <remarks>
  /// Ranges -1..4. Pattern does not correlate with color/altitude as originally hypothesized
  /// (Terrain_31, a near-black entry, has Unk14=4, same as light-colored entries). Speculative purpose unknown.
  /// Not confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk14,
  /// <remarks>
  /// Ranges -0.5..1, loosely tracks Unk14's sign. No independent pattern found.
  /// Not confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk15
);

public readonly record struct TerrainTypeEntry(
  string Name,
  string? DescriptionName,
  string? IconName,
  string? TextureRef,
  uint Version,
  uint Addon,
  uint Number,
  TerrainType Type,
  TerrainParameters Parameters,
  TerrainUnknowns Unknowns
);

public static class TerrainTypes {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>Extracts all TerrainType entries from an OVL archive.</summary>
  public static IReadOnlyList<TerrainTypeEntry> Extract(Ovl ovl) {
    var terFiles = ovl.Keys.Where(file => file.Type == FileType.TerrainType).ToList();
    var bag = new ConcurrentBag<TerrainTypeEntry>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(terFiles, file => {
      try {
        bag.Add(ReadTerrainType(ovl, file));
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} terrain types from {x} OVL{suffix}.",
        failures.Count,
        terFiles.Count,
        terFiles.Count != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  private static TerrainTypeEntry ReadTerrainType(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve TerrainType block for {file.Name}");

    var o = Convert.ToInt32(offset);
    if (block.Length < o + 60)
      throw new InvalidOperationException($"TerrainType data too short for {file.Name}");

    var version = BitConverter.ToUInt32(block, o);
    var unk02 = BitConverter.ToUInt32(block, o + 4);
    var addon = BitConverter.ToUInt32(block, o + 8);
    var number = BitConverter.ToUInt32(block, o + 12);
    var type = (TerrainType)BitConverter.ToUInt32(block, o + 16);

    // texture_ref (offset 20, per terraintype.h) is a relocated TextureStruct* - but in every
    // production Terrain_RCT3/Terrain_CT archive sampled, this field is zero on disk (no
    // relocation-fixup entry at all, not just a null pointer): it's unused/unpopulated in shipped
    // data. The actual ter<->tex link is the shared symbol name (e.g. ter "Terrain_06" and tex
    // "Terrain_06"), which Terrain.cs's texture lookup uses via TerrainTypeEntry.Name instead.
    var textureRef = ovl.TryGetRelocationSource(address + 20, out var textureAddress)
      && ovl.TryFindSymbol(textureAddress, out var textureFile) ? textureFile.Name : null;
    var descriptionName = ReadRelocationString(ovl, block, o + 24);
    var iconName = ReadRelocationString(ovl, block, o + 28);
    var colorSimple = BitConverter.ToUInt32(block, o + 32);
    var colorMap = BitConverter.ToUInt32(block, o + 36);
    var invWidth = BitConverter.ToSingle(block, o + 40);
    var invHeight = BitConverter.ToSingle(block, o + 44);
    var unk13 = BitConverter.ToSingle(block, o + 48);
    var unk14 = BitConverter.ToSingle(block, o + 52);
    var unk15 = BitConverter.ToSingle(block, o + 56);

    var parameters = new TerrainParameters(colorSimple, colorMap, invWidth, invHeight);
    var unknowns = new TerrainUnknowns(unk02, unk13, unk14, unk15);

    return new TerrainTypeEntry(
      file.Name,
      descriptionName,
      iconName,
      textureRef,
      version,
      addon,
      number,
      type,
      parameters,
      unknowns
    );
  }

  private static string? ReadRelocationString(Ovl ovl, byte[] block, int offset) {
    var relocationValue = BitConverter.ToUInt32(block, offset);
    if (relocationValue == 0)
      return null;

    if (ovl.TryResolveRelocation(relocationValue, out var stringBlock, out var stringOffset)) {
      var resolvedOffset = Convert.ToInt32(stringOffset);
      if (resolvedOffset >= stringBlock.Length)
        return null;

      var nullIndex = Array.IndexOf(stringBlock, (byte)0, resolvedOffset);
      var endIndex = nullIndex >= 0 ? nullIndex : stringBlock.Length;
      var length = endIndex - resolvedOffset;

      if (length <= 0)
        return null;

      return System.Text.Encoding.UTF8.GetString(stringBlock, resolvedOffset, length);
    }

    return null;
  }
}

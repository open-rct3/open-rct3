// TerrainTypes
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Buffers.Binary;
using System.Text;

namespace OpenCobra.OVL.Files;

/// <summary>The supported rendering role of an RCT3 terrain type.</summary>
public enum TerrainTypeKind : uint {
  GroundUnblended = 0,
  Cliff = 1,
  GroundBlended = 2
}

/// <summary>A typed OVL resource name referenced by a terrain definition.</summary>
public sealed record TerrainResourceReference(string Name, FileType Type) {
  public string QualifiedName => $"{Name}:{Type.ToTagString()}";
}

/// <summary>Color and texture-scale parameters stored by a terrain definition.</summary>
public sealed record TerrainParameters(
  uint Color01,
  uint Color02,
  float InvWidth,
  float InvHeight
) {
  public uint ColorSimple => Color01;
  public uint ColorMap => Color02;
}

/// <summary>Preserved fields whose rendering purpose is not yet established.</summary>
public sealed record TerrainUnknowns(
  uint Unk02,
  float Unk13,
  float Unk14,
  float Unk15
);

/// <summary>A decoded TerrainType (<c>ter</c>) resource.</summary>
public sealed record TerrainType(
  string Name,
  TerrainResourceReference Description,
  TerrainResourceReference Icon,
  TerrainResourceReference Texture,
  uint Version,
  Addon Addon,
  uint Number,
  TerrainTypeKind Type,
  TerrainParameters Parameters,
  TerrainUnknowns Unknowns
) {
  public static TerrainTypeKind GroundUnblended => TerrainTypeKind.GroundUnblended;
  public static TerrainTypeKind Cliff => TerrainTypeKind.Cliff;
  public static TerrainTypeKind GroundBlended => TerrainTypeKind.GroundBlended;

  public string DescriptionName => Description.Name;
  public string IconName => Icon.Name;
  public string TextureRef => Texture.Name;
}

/// <summary>Decodes TerrainType (<c>ter</c>) resources from an OVL pair.</summary>
/// <remarks>
/// Layout and reference behavior are ported from
/// <see href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/terraintype.h">terraintype.h</see>,
/// <see href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/ManagerTER.cpp">ManagerTER.cpp</see>,
/// and
/// <see href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/LodSymRefManager.cpp">LodSymRefManager.cpp</see>.
/// </remarks>
public static class TerrainTypes {
  // The disk structure contains fifteen 32-bit fields in this exact order; its three pointer
  // fields remain 32-bit on 64-bit hosts.
  internal const int RecordSize = 60;
  private const int TextureReferenceOffset = 20;
  private const int DescriptionReferenceOffset = 24;
  private const int IconReferenceOffset = 28;

  internal readonly record struct StringTableBlock(uint Address, long Offset, uint Size);

  internal sealed record ArchiveLayout(
    IReadOnlyList<StringTableBlock> StringBlocks,
    uint DataSize
  );

  /// <summary>Extracts every terrain definition, failing if any record or reference is malformed.</summary>
  public static IReadOnlyList<TerrainType> Extract(Ovl ovl) {
    ArgumentNullException.ThrowIfNull(ovl);
    var entries = ovl.Keys
      .Where(file => file.Type == FileType.TerrainType)
      .Select(file => (File: file, Address: GetDataAddress(ovl, file)))
      .ToList();

    if (entries.Count == 0) return [];

    foreach (var (file, _) in entries) {
      if (!file.Path.EndsWith(".unique.ovl", StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException($"Terrain resource '{file}' is not stored in a unique OVL.");
    }

    var resolver = new OvlTerrainReferenceResolver(
      ovl,
      entries.ToDictionary(entry => checked(entry.Address + TextureReferenceOffset), entry => entry.Address)
    );

    var terrains = new List<TerrainType>(entries.Count);
    foreach (var (file, address) in entries) {
      if (!ovl.TryReadBytes(address, RecordSize, out var data))
        throw new InvalidDataException($"Terrain resource '{file}' is truncated; expected {RecordSize} bytes.");

      terrains.Add(Decode(
        file.Name,
        data,
        (offset, type) => resolver.Resolve(checked(address + Convert.ToUInt32(offset)), type)
      ));
    }

    return terrains;
  }

  internal static TerrainType Decode(
    string name,
    ReadOnlySpan<byte> data,
    Func<int, FileType, TerrainResourceReference> resolveReference
  ) {
    if (string.IsNullOrWhiteSpace(name))
      throw new InvalidDataException("Terrain resource name cannot be empty.");
    if (data.Length != RecordSize)
      throw new InvalidDataException($"Terrain resource '{name}' is {data.Length} bytes; expected {RecordSize}.");

    var version = ReadUInt32(data, 0);
    if (version != 1)
      throw new InvalidDataException($"Terrain resource '{name}' has unsupported version {version}.");

    var addonValue = ReadUInt32(data, 8);
    if (!Enum.IsDefined(typeof(Addon), addonValue))
      throw new InvalidDataException($"Terrain resource '{name}' has invalid addon value {addonValue}.");

    var typeValue = ReadUInt32(data, 16);
    if (!Enum.IsDefined(typeof(TerrainTypeKind), typeValue))
      throw new InvalidDataException($"Terrain resource '{name}' has invalid type value {typeValue}.");

    var description = ResolveReference(
      resolveReference, DescriptionReferenceOffset, FileType.Text, name);
    var icon = ResolveReference(
      resolveReference, IconReferenceOffset, FileType.GuiSkinItem, name);
    var texture = ResolveReference(
      resolveReference, TextureReferenceOffset, FileType.Texture, name);

    var invWidth = ReadFiniteSingle(data, 40, name);
    var invHeight = ReadFiniteSingle(data, 44, name);
    if (invWidth <= 0 || invHeight <= 0)
      throw new InvalidDataException($"Terrain resource '{name}' has non-positive inverse dimensions.");

    var parameters = new TerrainParameters(
      ReadUInt32(data, 32),
      ReadUInt32(data, 36),
      invWidth,
      invHeight
    );
    var unknowns = new TerrainUnknowns(
      ReadUInt32(data, 4),
      ReadFiniteSingle(data, 48, name),
      ReadFiniteSingle(data, 52, name),
      ReadFiniteSingle(data, 56, name)
    );

    return new TerrainType(
      name,
      description,
      icon,
      texture,
      version,
      (Addon)addonValue,
      ReadUInt32(data, 12),
      (TerrainTypeKind)typeValue,
      parameters,
      unknowns
    );
  }

  private static uint GetDataAddress(Ovl ovl, OvlFile file) {
    if (ovl.TryGetDataPointer(file, out var address)) return address;
    throw new InvalidDataException($"Terrain resource '{file}' has no relocated data address.");
  }

  private static TerrainResourceReference ResolveReference(
    Func<int, FileType, TerrainResourceReference> resolver,
    int offset,
    FileType expectedType,
    string terrainName
  ) {
    var reference = resolver(offset, expectedType);
    if (reference.Type != expectedType)
      throw new InvalidDataException(
        $"Terrain resource '{terrainName}' has a {reference.Type.ToTagString()} reference at byte {offset}; " +
        $"expected {expectedType.ToTagString()}.");
    if (string.IsNullOrWhiteSpace(reference.Name))
      throw new InvalidDataException(
        $"Terrain resource '{terrainName}' has an empty {expectedType.ToTagString()} reference.");
    return reference;
  }

  private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));

  private static float ReadFiniteSingle(ReadOnlySpan<byte> data, int offset, string name) {
    var value = BitConverter.ToSingle(data.Slice(offset, sizeof(float)));
    if (!float.IsFinite(value))
      throw new InvalidDataException($"Terrain resource '{name}' has a non-finite value at byte {offset}.");
    return value;
  }

  internal static StringTableBlock ReadStringTableBlock(BinaryReader reader) =>
    OvlTerrainReferenceResolver.ReadStringTableBlock(reader);

  internal static bool TryReadStringTableStart(BinaryReader reader, out string value) =>
    OvlTerrainReferenceResolver.TryReadStringTableStart(reader, out value);

  internal static bool TryReadString(BinaryReader reader, uint address, out string value) =>
    OvlTerrainReferenceResolver.TryReadString(reader, address, out value);

  private sealed class OvlTerrainReferenceResolver(
    Ovl ovl,
    IReadOnlyDictionary<uint, uint> terrainAddressesByTextureSlot
  ) {
    // Shipped TER archives exercised by the installed-data scan are far below these ceilings.
    // Keep generous format headroom while preventing file-controlled counts from driving
    // unbounded loops or allocations before their byte ranges are validated.
    private const int MaximumExternalReferences = 65_536;
    private const int MaximumLoaderHeaders = 4_096;
    private const int MaximumBlocksPerType = 65_536;
    private const int MaximumTotalBlocks = 65_536;

    private IReadOnlyDictionary<uint, TerrainResourceReference>? textureReferences;
    private ArchiveLayout? commonArchiveLayout;
    private string? commonArchivePath;

    public TerrainResourceReference Resolve(uint fieldAddress, FileType expectedType) =>
      expectedType == FileType.Texture
        ? ResolveTexture(fieldAddress)
        : ResolveString(fieldAddress, expectedType);

    private TerrainResourceReference ResolveString(uint fieldAddress, FileType expectedType) {
      if (!ovl.TryGetRelocationSource(fieldAddress, out var targetAddress))
        throw new InvalidDataException(
          $"Terrain {expectedType.ToTagString()} field at 0x{fieldAddress:X} is not a relocation source.");
      if (!TryResolveString(targetAddress, out var name) || string.IsNullOrWhiteSpace(name))
        throw new InvalidDataException(
          $"Terrain {expectedType.ToTagString()} field at 0x{fieldAddress:X} has an invalid target.");
      return new TerrainResourceReference(name, expectedType);
    }

    private bool TryResolveString(uint address, out string value) {
      // Terrain names and symbol names are shared through the common archive's type-0 string blocks.
      // Parse those declared ranges directly so address zero remains valid while wrong-block and
      // unterminated values fail closed.
      commonArchivePath ??= FindCommonArchivePath();
      if (commonArchivePath == null) {
        value = "";
        return false;
      }

      using var stream = File.OpenRead(commonArchivePath);
      using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
      commonArchiveLayout ??= ReadArchiveLayout(reader);
      return TryReadString(reader, commonArchiveLayout, address, out value);
    }

    private string? FindCommonArchivePath() {
      var commonPaths = ovl.Keys
        .Select(file => ToCommonPath(file.Path))
        .Where(File.Exists)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
      return commonPaths.Count == 1 ? commonPaths[0] : null;
    }

    internal static bool TryReadStringTableStart(BinaryReader reader, out string value) =>
      TryReadString(reader, 0, out value);

    internal static bool TryReadString(BinaryReader reader, uint address, out string value) {
      var layout = ReadArchiveLayout(reader);
      return TryReadString(reader, layout, address, out value);
    }

    private static bool TryReadString(
      BinaryReader reader,
      ArchiveLayout layout,
      uint address,
      out string value
    ) {
      const int maximumNameLength = 4096;
      StringTableBlock? containingBlock = null;
      foreach (var block in layout.StringBlocks) {
        if (block.Size != 0 && address >= block.Address && address - block.Address < block.Size) {
          containingBlock = block;
          break;
        }
      }
      if (containingBlock == null) {
        value = "";
        return false;
      }

      var stringBlock = containingBlock.Value;
      var offsetInBlock = address - stringBlock.Address;
      reader.BaseStream.Position = checked(stringBlock.Offset + offsetInBlock);
      var bytes = new List<byte>();
      var remainingInBlock = stringBlock.Size - offsetInBlock;
      var bytesToScan = Convert.ToInt32(Math.Min(remainingInBlock, (uint)maximumNameLength));
      foreach (var _ in Enumerable.Range(0, bytesToScan)) {
        var next = reader.BaseStream.ReadByte();
        if (next == 0) {
          value = Encoding.ASCII.GetString([.. bytes]);
          return true;
        }
        if (next > 127) break;
        bytes.Add(Convert.ToByte(next));
      }

      value = "";
      return false;
    }

    private static string ToCommonPath(string path) {
      const string uniqueSuffix = ".unique.ovl";
      if (!path.EndsWith(uniqueSuffix, StringComparison.OrdinalIgnoreCase)) return path;
      return path[..^uniqueSuffix.Length] + ".common.ovl";
    }

    internal static StringTableBlock ReadStringTableBlock(BinaryReader reader) {
      var layout = ReadArchiveLayout(reader);
      if (layout.StringBlocks.Count == 0)
        throw new InvalidDataException("OVL has no common string-table block.");
      var firstBlock = layout.StringBlocks[0];
      if (firstBlock.Size == 0)
        throw new InvalidDataException("OVL has an empty common string-table block.");
      return firstBlock;
    }

    internal static ArchiveLayout ReadArchiveLayout(BinaryReader reader) {
      Require(reader, 16);
      if (reader.ReadUInt32() != 0x4b524746)
        throw new InvalidDataException("Invalid OVL magic while resolving the common string table.");
      reader.ReadUInt32();
      var version = reader.ReadUInt32();
      var headerReferences = reader.ReadUInt32();
      if (version is not 1 and not 4 and not 5)
        throw new InvalidDataException($"Unsupported OVL version {version}.");

      var subVersionFlag = 0u;
      var referenceCount = headerReferences;
      if (version == 4) {
        Require(reader, 4);
        referenceCount = reader.ReadUInt32();
      } else if (version == 5) {
        Require(reader, 4);
        subVersionFlag = reader.ReadUInt32();
        if (subVersionFlag != 0) {
          Skip(reader, 12);
          ReadNullTerminatedBytes(reader);
          while (reader.BaseStream.Position % 4 != 0) Skip(reader, 1);
        }
        Require(reader, 4);
        referenceCount = reader.ReadUInt32();
      }

      var boundedReferenceCount = BoundedCount(
        reader, referenceCount, 2, MaximumExternalReferences, "external reference");
      foreach (var _ in Enumerable.Range(0, boundedReferenceCount)) {
        Require(reader, 2);
        Skip(reader, reader.ReadUInt16());
      }

      Require(reader, 8);
      reader.ReadUInt32();
      var loaderCount = reader.ReadUInt32();
      var boundedLoaderCount = BoundedCount(
        reader, loaderCount, 10, MaximumLoaderHeaders, "loader header");
      foreach (var _ in Enumerable.Range(0, boundedLoaderCount)) {
        SkipLengthPrefixedString(reader);
        SkipLengthPrefixedString(reader);
        Skip(reader, 4);
        SkipLengthPrefixedString(reader);
      }
      if (version == 5) Skip(reader, checked(boundedLoaderCount * 8L));

      var blockCounts = new int[9];
      var blockSizes = Enumerable.Range(0, 9).Select(_ => new List<uint>()).ToArray();
      var totalBlockCount = 0;
      foreach (var typeIndex in Enumerable.Range(0, 9)) {
        Require(reader, 4);
        var blockCount = reader.ReadUInt32();
        if (version > 1) {
          Skip(reader, 4);
          if (version == 5 && subVersionFlag != 0) Skip(reader, 4);
        }
        var boundedBlockCount = BoundedCount(
          reader, blockCount, 4, MaximumBlocksPerType, $"type-{typeIndex} block");
        if (boundedBlockCount > MaximumTotalBlocks - totalBlockCount)
          throw new InvalidDataException("OVL aggregate block count exceeds decoder limits.");
        totalBlockCount += boundedBlockCount;
        blockCounts[typeIndex] = boundedBlockCount;
        if (version > 1) {
          blockSizes[typeIndex] = new List<uint>(boundedBlockCount);
          foreach (var _ in Enumerable.Range(0, boundedBlockCount)) {
            Require(reader, 4);
            blockSizes[typeIndex].Add(reader.ReadUInt32());
          }
        }
      }

      if (version == 4) {
        Skip(reader, 8);
      } else if (version == 5) {
        Require(reader, 4);
        Skip(reader, reader.ReadUInt32());
        Require(reader, 4);
        Skip(reader, checked(Convert.ToInt64(reader.ReadUInt32()) * 4));
      }

      var stringBlocks = new List<StringTableBlock>(blockCounts[0]);
      var relativeOffset = 0u;
      foreach (var typeIndex in Enumerable.Range(0, 9)) {
        foreach (var blockIndex in Enumerable.Range(0, blockCounts[typeIndex])) {
          uint blockSize;
          if (version == 1) {
            Require(reader, 4);
            blockSize = reader.ReadUInt32();
          } else {
            blockSize = blockSizes[typeIndex][blockIndex];
          }

          var fileOffset = reader.BaseStream.Position;
          Require(reader, blockSize);
          if (typeIndex == 0)
            stringBlocks.Add(new StringTableBlock(relativeOffset, fileOffset, blockSize));
          if (blockSize > uint.MaxValue - relativeOffset)
            throw new InvalidDataException("OVL block data exceeds the 32-bit address space.");
          relativeOffset += blockSize;
          Skip(reader, blockSize);
        }
      }

      return new ArchiveLayout(stringBlocks, relativeOffset);
    }

    private static int BoundedCount(
      BinaryReader reader,
      uint count,
      int minimumBytesPerItem,
      int maximumCount,
      string description
    ) {
      if (count > maximumCount)
        throw new InvalidDataException($"OVL {description} count exceeds decoder limits.");
      var remainingBytes = reader.BaseStream.Length - reader.BaseStream.Position;
      if (minimumBytesPerItem > 0 && count > remainingBytes / minimumBytesPerItem)
        throw new InvalidDataException($"OVL {description} count exceeds the remaining file data.");
      return Convert.ToInt32(count);
    }

    private static void SkipLengthPrefixedString(BinaryReader reader) {
      Require(reader, 2);
      Skip(reader, reader.ReadUInt16());
    }

    private static void ReadNullTerminatedBytes(BinaryReader reader) {
      const int maximumLength = 4096;
      foreach (var _ in Enumerable.Range(0, maximumLength)) {
        Require(reader, 1);
        if (reader.ReadByte() == 0) return;
      }
      throw new InvalidDataException("OVL header string exceeds decoder limits.");
    }

    private static void Skip(BinaryReader reader, long length) {
      if (length < 0) throw new InvalidDataException("OVL field has a negative length.");
      Require(reader, length);
      reader.BaseStream.Seek(length, SeekOrigin.Current);
    }

    private static void Require(BinaryReader reader, long length) {
      if (length < 0 || reader.BaseStream.Length - reader.BaseStream.Position < length)
        throw new InvalidDataException("OVL header is truncated.");
    }

    private TerrainResourceReference ResolveTexture(uint fieldAddress) {
      textureReferences ??= ReadTextureReferences();
      if (textureReferences.TryGetValue(fieldAddress, out var reference)) return reference;
      throw new InvalidDataException(
        $"Terrain tex field at 0x{fieldAddress:X} has no valid symbol reference.");
    }

    private IReadOnlyDictionary<uint, TerrainResourceReference> ReadTextureReferences() {
      var references = new Dictionary<uint, TerrainResourceReference>();
      var scanEnd = GetReferenceScanEnd();
      if (scanEnd < 12) return references;

      // Block starts need not be 4-byte aligned in shipped archives. Locate the SymbolRefStruct /
      // SymbolRefStruct2 records whose `reference` target is a TER texture_ref slot, then prove
      // their symbol and owning-loader relocations.
      for (var sourceAddress = 0u; sourceAddress <= scanEnd - 12; sourceAddress++) {
        if (!ovl.TryGetRelocationSource(sourceAddress, out var fieldAddress) ||
            !terrainAddressesByTextureSlot.TryGetValue(fieldAddress, out var terrainAddress))
          continue;

        if (!ovl.TryGetRelocationSource(checked(sourceAddress + 4), out var symbolAddress) ||
            !TryResolveString(symbolAddress, out var qualifiedName))
          throw new InvalidDataException(
            $"Terrain tex symbol reference at 0x{sourceAddress:X} has an invalid symbol target.");
        if (!ovl.TryGetRelocationSource(checked(sourceAddress + 8), out var loaderAddress) ||
            !ovl.TryGetRelocationSource(checked(loaderAddress + 4), out var loaderDataAddress) ||
            loaderDataAddress != terrainAddress)
          throw new InvalidDataException(
            $"Terrain tex symbol reference at 0x{sourceAddress:X} has an invalid loader target.");

        var reference = ParseQualifiedReference(qualifiedName, FileType.Texture, sourceAddress);
        if (!references.TryAdd(fieldAddress, reference))
          throw new InvalidDataException(
            $"Terrain tex field at 0x{fieldAddress:X} has duplicate symbol references.");
      }

      return references;
    }

    private uint GetReferenceScanEnd() {
      long length = 0;
      foreach (var path in ovl.Keys.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase)) {
        if (!File.Exists(path))
          throw new InvalidDataException($"OVL source file no longer exists: '{path}'.");
        length = checked(length + new FileInfo(path).Length);
      }
      if (length > uint.MaxValue)
        throw new InvalidDataException("Combined OVL size exceeds the 32-bit relocation address space.");
      return Convert.ToUInt32(length);
    }

    private static TerrainResourceReference ParseQualifiedReference(
      string qualifiedName,
      FileType expectedType,
      uint sourceAddress
    ) {
      var separator = qualifiedName.LastIndexOf(':');
      if (separator <= 0 || separator == qualifiedName.Length - 1)
        throw new InvalidDataException(
          $"Terrain symbol reference at 0x{sourceAddress:X} has an invalid name '{qualifiedName}'.");

      var name = qualifiedName[..separator];
      var type = qualifiedName[(separator + 1)..].ToFileType();
      if (type != expectedType)
        throw new InvalidDataException(
          $"Terrain symbol reference '{qualifiedName}' is not a {expectedType.ToTagString()} resource.");
      return new TerrainResourceReference(name, type);
    }
  }
}

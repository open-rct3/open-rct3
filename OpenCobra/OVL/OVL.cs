// OVL.cs
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using OpenCobra.OVL.Files;

namespace OpenCobra.OVL;

/// <summary>OVL archive entry (resource) identifier.</summary>
public record OvlFile(string Name, FileType Type, string Path) {
  public override string ToString() => $"{Name}.{Type.ToTagString()}";
  public override int GetHashCode() => HashCode.Combine(Name, Type, Path);
}

/// <summary>Location and size of a resource within the OVL archive.</summary>
public record OvlEntry(uint Offset, uint Size);

internal record LoaderHeader(string Loader, string Name, uint Type, string Tag, uint SymbolCount);
internal record OvlLoaderEntry(string Tag, uint DataAddress, string SourcePath, uint StructAddress);
internal record OvlBlockEntry(
  uint Address,
  byte[] Data,
  string SourcePath,
  Version Version,
  int RecordStride,
  uint RecordCount
);

internal class FileBlock {
  /// <summary>
  /// Source OVL archive path.
  /// </summary>
  public required string Path;
  /// <summary>
  /// Absolute offset within the OVL archive.
  /// </summary>
  public ulong Offset;
  /// <summary>
  /// Size of the block in bytes.
  /// </summary>
  public uint Size;
  // FIXME: Is this summary correct?
  /// <summary>
  /// Offset within the OVL archive, relative to the end of the last block.
  /// </summary>
  public uint RelativeOffset;
  public int TypeIndex;
  public byte[]? Data;
}

internal class FileTypeBlock {
  public uint Count;
  public uint Size;
  public uint UnknownV5Extra;
  public List<FileBlock> Blocks = [];
}

/// <summary>Represents an OVL archive, providing methods to load and extract resource entries.</summary>
public sealed class Ovl(string name) : IDictionary<OvlFile, OvlEntry>, IDisposable {
  public const string UnnamedOvl = "Untitled OVL";
  // Keep archive-controlled FileBlock materialization bounded even if a malicious file pads enough
  // bytes to satisfy the structural size-table preflight below.
  private const int MaxBlocksPerArchive = 65_536;

  public readonly string Name = name;
  public Version Version => version;

  private Version version;
  private readonly Dictionary<OvlFile, OvlEntry> entries = [];
  private readonly Dictionary<OvlFile, uint> entryDataPtrs = [];
  private readonly Dictionary<uint, OvlFile> symbolsByDataPointer = [];
  private readonly Dictionary<uint, OvlFile> symbolReferenceTargets = [];
  private readonly HashSet<(string Name, FileType Type)> symbolReferences = [];
  private readonly List<FileTypeBlock[]> allFileTypeBlocks = [];
  private readonly List<LoaderHeader[]> allLoaderHeaders = [];
  private readonly List<Version> allVersions = [];
  private readonly List<Dictionary<uint, List<byte[]>>> allExtraData = [];
  // Relocation-fixup table (Part 6 Finding 3 / rct3tex.cpp:1830-1842's DoReloc): a flat
  // sourceAddress -> rawValueAtThatAddress map. "Source address" here is a location in block data
  // that the archive's own linker flagged as needing pointer interpretation; the raw bytes stored
  // there are only trustworthy as a real pointer if the address is listed here - unlisted locations
  // are unpatched placeholder bytes (e.g. Tex fields for textureless entries like render targets).
  private readonly Dictionary<uint, uint> relocations = [];
  // Ordered (per file, in on-disk LoaderStruct order) loader entries - see Part 6
  // Finding 4: "btbl"/"flic" are loader-category tags only, never discoverable as classified
  // symbols, so callers that need every loader instance (not just symbol-backed resources) must
  // walk this instead of ovl.Keys. SourcePath keeps common and unique table state independent.
  private readonly List<OvlLoaderEntry> loaderEntriesInOrder = [];
  // Exact type-2 SymbolRef blocks. LodSymRefManager allocates subblock 2 only when the sum of
  // LoaderStruct.SymbolsToResolve for that source file is nonzero; otherwise a third type-2 block
  // may belong to an unrelated manager.
  private readonly List<OvlBlockEntry> symbolReferenceBlocksInOrder = [];
  private uint relocationOffset;
  private bool disposed = false;

  /// <summary>
  /// Every loader instance in the archive, in on-disk order (common file first, then unique), with
  /// its category tag (e.g. "btbl", "flic", "tex") and relocation-resolved data address. Unlike
  /// <see cref="Keys"/>, this includes loader categories (like "btbl"/"flic") that are never
  /// classified as their own symbol - see Part 6 Finding 4 of the texture-decoding bug doc.
  /// </summary>
  internal IReadOnlyList<OvlLoaderEntry> LoaderEntriesInOrder => loaderEntriesInOrder;
  /// <summary>Exact per-source SymbolRef blocks and their serialized layout metadata.</summary>
  internal IReadOnlyList<OvlBlockEntry> SymbolReferenceBlocksInOrder =>
    symbolReferenceBlocksInOrder;

  /// <summary>
  /// Every typed target named by a serialized SymbolRef record, including targets that are defined
  /// by another archive and therefore cannot be resolved to an <see cref="OvlFile"/> locally.
  /// </summary>
  public IReadOnlyCollection<(string Name, FileType Type)> SymbolReferences => symbolReferences;

  /// <summary>Reads <paramref name="length"/> raw bytes at a relocation-resolved data address.</summary>
  public bool TryReadBytes(uint address, int length, [MaybeNullWhen(false)] out byte[] data) {
    if (length < 0 || !TryResolveRelocation(address, out var block, out var offset)) {
      data = null;
      return false;
    }

    var start = Convert.ToInt32(offset);
    if (length > block.Length - start) {
      data = null;
      return false;
    }

    data = block.AsSpan(start, length).ToArray();
    return true;
  }

  #region IDictionary<OvlFile, OvlEntry>
  public ICollection<OvlFile> Keys => ((IDictionary<OvlFile, OvlEntry>)entries).Keys;
  public ICollection<OvlEntry> Values => ((IDictionary<OvlFile, OvlEntry>)entries).Values;
  public int Count => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).Count;
  public bool IsReadOnly => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).IsReadOnly;
  public OvlEntry this[OvlFile key] { get => ((IDictionary<OvlFile, OvlEntry>)entries)[key]; set => ((IDictionary<OvlFile, OvlEntry>)entries)[key] = value; }

  public void Add(OvlFile key, OvlEntry value) => ((IDictionary<OvlFile, OvlEntry>)entries).Add(key, value);
  public bool ContainsKey(OvlFile key) => ((IDictionary<OvlFile, OvlEntry>)entries).ContainsKey(key);
  public bool Remove(OvlFile key) => ((IDictionary<OvlFile, OvlEntry>)entries).Remove(key);
  public bool TryGetValue(OvlFile key, [MaybeNullWhen(false)] out OvlEntry value) => ((IDictionary<OvlFile, OvlEntry>)entries).TryGetValue(key, out value);
  public void Add(KeyValuePair<OvlFile, OvlEntry> item) => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).Add(item);
  public void Clear() => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).Clear();
  public bool Contains(KeyValuePair<OvlFile, OvlEntry> item) => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).Contains(item);
  public void CopyTo(KeyValuePair<OvlFile, OvlEntry>[] array, int arrayIndex) => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).CopyTo(array, arrayIndex);
  public bool Remove(KeyValuePair<OvlFile, OvlEntry> item) => ((ICollection<KeyValuePair<OvlFile, OvlEntry>>)entries).Remove(item);
  public IEnumerator<KeyValuePair<OvlFile, OvlEntry>> GetEnumerator() => ((IEnumerable<KeyValuePair<OvlFile, OvlEntry>>)entries).GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)entries).GetEnumerator();
  #endregion

  /// <summary>Load an OVL archive and extract all resource entries.</summary>
  public static Ovl Load(string ovlPath) {
    var ovl = new Ovl(Path.GetFileName(ovlPath));
    ovl.version = ovl.IngestArchive(ovlPath);
    return ovl;
  }

  /// <summary>Find a resource by name.</summary>
  public OvlFile? Find(string? name, FileType? type = null) => entries.Keys.FirstOrDefault(key => {
    var nameProvided = name != null;
    var nameMatch = name == null || key.Name.Contains(name, StringComparison.OrdinalIgnoreCase);
    var typeMatch = type == null || key.Type == type;

    // If a name is given, return true if the name matches.
    // If a name and type is given, return true if both match.
    // Otherwise, return true if the type matches.
    return nameProvided
      ? nameMatch && (type == null || typeMatch)
      : typeMatch;
  });

  /// <summary>Read the resource data for a given file.</summary>
  public byte[]? ReadResource(OvlFile file) {
    if (!entries.TryGetValue(file, out var entry) || entry.Size > int.MaxValue ||
        string.IsNullOrEmpty(file.Path)) return null;

    try {
      using var fs = File.OpenRead(file.Path);
      var offset = Convert.ToInt64(entry.Offset);
      var size = Convert.ToInt64(entry.Size);
      if (offset > fs.Length || size > fs.Length - offset) return null;

      var bytes = new byte[Convert.ToInt32(entry.Size)];
      fs.Seek(offset, SeekOrigin.Begin);
      fs.ReadExactly(bytes, 0, bytes.Length);
      return bytes;
    } catch (IOException) {
      return null;
    } catch (UnauthorizedAccessException) {
      return null;
    }
  }

  /// <summary>
  /// Resolves a relocated data pointer to its absolute block data.
  /// </summary>
  /// <param name="dataPtr">Relative offset pointer from OVL block data</param>
  /// <param name="data">The resolved block's full data, or null if unresolved</param>
  /// <param name="offset">Offset within the resolved data where the pointer refers</param>
  /// <returns>True if resolution succeeded.</returns>
  public bool TryResolveRelocation(uint dataPtr, [MaybeNullWhen(false)] out byte[] data, out uint offset) {
    // A null (zero) pointer never resolves - without this guard it spuriously "resolves" to
    // whatever block happens to start at RelativeOffset 0 (see TryResolveString's matching guard).
    var resolvedBlock = dataPtr == 0 ? null : FindBlock(dataPtr);
    if (resolvedBlock?.Data == null ||
        !TryGetBlockOffset(resolvedBlock, dataPtr, out var resolvedOffset)) {
      data = null;
      offset = 0;
      return false;
    }

    data = resolvedBlock.Data;
    offset = Convert.ToUInt32(resolvedOffset);
    return true;
  }

  /// <summary>
  /// Looks up a location in block data that the archive's own relocation-fixup table lists as
  /// needing pointer interpretation (see <see cref="relocations"/>), and returns the raw value
  /// stored there. Used to chase relocated pointer chains (e.g. <c>Tex.FlicPtr</c>, a double
  /// pointer needing two chained lookups - see Part 6 Finding 2 of the texture-decoding bug doc)
  /// without trusting arbitrary unpatched placeholder bytes as if they were real pointers.
  /// </summary>
  /// <param name="address">Relative offset address of the field to look up</param>
  /// <param name="rawValue">The raw value stored at that address on disk, if listed</param>
  /// <returns>True if <paramref name="address"/> is listed in the relocation-fixup table.</returns>
  public bool TryGetRelocationSource(uint address, out uint rawValue) =>
    relocations.TryGetValue(address, out rawValue);

  private FileBlock? FindBlock(uint address) => allFileTypeBlocks
    .SelectMany(ftb => ftb.SelectMany(b => b.Blocks))
    .FirstOrDefault(fb => TryGetBlockOffset(fb, address, out _));

  private static bool TryGetBlockOffset(FileBlock block, uint address, out int offset) {
    offset = 0;
    if (block.Data == null || address < block.RelativeOffset) return false;

    var relativeOffset = address - block.RelativeOffset;
    if (relativeOffset >= block.Size || relativeOffset >= block.Data.Length) return false;

    offset = Convert.ToInt32(relativeOffset);
    return true;
  }

  private static bool TryGetBlockSlice(FileBlock block, uint address, int length, out int offset) {
    offset = 0;
    if (length < 0 || !TryGetBlockOffset(block, address, out offset)) return false;
    return length <= block.Data!.Length - offset;
  }

  /// <summary>
  /// Reads the "extra data" chunks attached to a loader, e.g. Flic pixel data or a bitmap-table
  /// index. This data is written after the relocation-fixup table and is not part of any
  /// relocatable block, so it cannot be reached via <see cref="TryResolveRelocation"/>: it must be
  /// looked up by the raw data-pointer value of the *loader* that owns it (see LoaderStruct.data
  /// in ManagerFLIC.cpp/OVLDump.cpp's MakeLoaders), not the pointer of the symbol that references it.
  /// </summary>
  /// <param name="dataPtr">Relative offset pointer identifying the owning loader</param>
  /// <param name="chunks">The loader's extra-data chunks, in on-disk order, or null if none exist</param>
  /// <returns>True if any extra data chunks were found for this loader.</returns>
  public bool TryReadExtraData(uint dataPtr, [MaybeNullWhen(false)] out IReadOnlyList<byte[]> chunks) {
    foreach (var extraData in allExtraData.Where(extraData => extraData.ContainsKey(dataPtr))) {
      chunks = extraData[dataPtr];
      return true;
    }

    chunks = null;
    return false;
  }

  /// <summary>
  /// Reads the "extra data" chunks attached to the loader for a named resource. See the
  /// <see cref="TryReadExtraData(uint, out IReadOnlyList{byte[]})"/> overload for why this data
  /// cannot be reached via <see cref="TryResolveRelocation"/>.
  /// </summary>
  public bool TryReadExtraData(OvlFile file, [MaybeNullWhen(false)] out IReadOnlyList<byte[]> chunks) {
    if (entryDataPtrs.TryGetValue(file, out var dataPtr))
      return TryReadExtraData(dataPtr, out chunks);

    chunks = null;
    return false;
  }

  /// <summary>Looks up a resolved resource's own (relative offset) data pointer address.</summary>
  public bool TryGetDataPointer(OvlFile file, out uint dataPtr) => entryDataPtrs.TryGetValue(file, out dataPtr);

  /// <summary>Resolves a resource data address back to its local symbol.</summary>
  public bool TryFindSymbol(uint dataPtr, [MaybeNullWhen(false)] out OvlFile file) =>
    symbolsByDataPointer.TryGetValue(dataPtr, out file);

  /// <summary>
  /// Resolves a SymbolRef field address to the local symbol it targets. References to another
  /// archive remain available through <see cref="SymbolReferences"/> but cannot produce an
  /// <see cref="OvlFile"/> from this archive.
  /// </summary>
  public bool TryResolveSymbolReference(uint fieldAddress, [MaybeNullWhen(false)] out OvlFile file) =>
    symbolReferenceTargets.TryGetValue(fieldAddress, out file);

  /// <summary>
  /// Resolves a relocated string pointer to its text value.
  /// </summary>
  /// <param name="ptr">Relative offset pointer to a null-terminated ASCII string in OVL block data</param>
  /// <param name="value">The resolved string, or null if unresolved</param>
  /// <returns>True if resolution succeeded.</returns>
  public bool TryResolveString(uint ptr, [MaybeNullWhen(false)] out string value) {
    if (!TryResolveRelocation(ptr, out var data, out var resolvedOffset)) {
      value = null;
      return false;
    }

    var offset = Convert.ToInt32(resolvedOffset);
    var end = Array.IndexOf(data, (byte)0, offset);
    if (end < 0) end = data.Length;
    value = Encoding.ASCII.GetString(data, offset, end - offset);
    return true;
  }

  private Version IngestArchive(string ovlPath) {
    var version = Version.Unknown;
    var basePath = Path.GetDirectoryName(ovlPath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(ovlPath).Split('.')[0];

    var commonPath = Path.Combine(basePath, $"{fileName}.common.ovl");
    if (File.Exists(commonPath))
      version = ProcessFile(commonPath);

    var uniquePath = Path.Combine(basePath, $"{fileName}.unique.ovl");
    if (File.Exists(uniquePath)) {
      var v = ProcessFile(uniquePath);
      version = version == Version.Unknown ? v : version;
    }

    ExtractResources();

    return version;
  }

  private Version ProcessFile(string filePath) {
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.UTF8, false);

    var magic = ReadUInt32(reader, "header magic");
    if (magic != 0x4b524746) throw new InvalidDataException("Invalid OVL magic.");
    ReadUInt32(reader, "header reserved field");
    var rawVersion = ReadUInt32(reader, "header version");
    var version = (Version) rawVersion;
    if (version != Version.One && version != Version.Four && version != Version.Five)
      throw new InvalidDataException($"Unsupported OVL version {rawVersion}.");
    var headerRefs = ReadUInt32(reader, "header reference count");

    Debug.WriteLine($"[OVL] Loading {Path.GetFileName(filePath)} (v{version})");

    var subVersionFlag = 0u;
    var referenceCount = version switch {
      Version.Five => ReadV5References(reader, out subVersionFlag),
      Version.Four => ReadUInt32(reader, "v4 reference count"),
      _ => headerRefs
    };

    Debug.WriteLine($"[OVL] subVersionFlag: {subVersionFlag}, referenceCount: {referenceCount}");

    foreach (var _ in Enumerable.Range(0, ToCount(referenceCount, "reference count"))) {
      var len = ReadUInt16(reader, "reference name length");
      ReadBytes(reader, len, "reference name");
    }

    ReadUInt32(reader, "secondary header unknown field"); // OvlHeader2.unk
    var fileTypeCount = ReadUInt32(reader, "loader count");
    if (fileTypeCount >= 1024)
      throw new InvalidDataException($"OVL loader count {fileTypeCount} exceeds the supported limit.");
    var loaderHeaders = ReadLoaderHeaders(reader, Convert.ToInt32(fileTypeCount));
    if (version == Version.Five) {
      ReadV5SymbolCounts(reader, loaderHeaders);
    }
    allLoaderHeaders.Add([.. loaderHeaders]);
    allVersions.Add(version);

    var blocks = ReadFileTypeBlocks(filePath, reader, version, subVersionFlag);
    allFileTypeBlocks.Add(blocks);

    ReadPostBlockUnknowns(reader, version);
    ReadBlockData(reader, blocks, version);
    ReadRelocations(reader);

    if (version == Version.Four || version >= Version.Five && (subVersionFlag & 1) != 0)
      ReadBytes(reader, sizeof(uint), "post-relocation field");

    allExtraData.Add(ReadLoaderExtraData(reader, blocks, version, loaderHeaders));

    return version;
  }

  private static int ToCount(uint count, string section) {
    if (count > int.MaxValue)
      throw new InvalidDataException($"OVL {section} exceeds the supported count.");
    return Convert.ToInt32(count);
  }

  private static void EnsureRemaining(BinaryReader reader, long length, string section) {
    var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
    if (length < 0 || length > remaining)
      throw new InvalidDataException($"Truncated OVL {section}.");
  }

  private static ushort ReadUInt16(BinaryReader reader, string section) {
    EnsureRemaining(reader, sizeof(ushort), section);
    return reader.ReadUInt16();
  }

  private static uint ReadUInt32(BinaryReader reader, string section) {
    EnsureRemaining(reader, sizeof(uint), section);
    return reader.ReadUInt32();
  }

  private static byte[] ReadBytes(BinaryReader reader, int length, string section) {
    EnsureRemaining(reader, length, section);
    return reader.ReadBytes(length);
  }

  /// <summary>
  /// Reads the per-loader "extra data" chunk stream that immediately follows the relocation-fixup
  /// table. See LoaderStruct in ovlstructs.h and the HasExtraData/ExtraChunk handling in
  /// OVLDump.cpp's MakeLoaders.
  ///
  /// Keyed by each loader's relocation-resolved data address, not its raw on-disk `data` field
  /// value: per Part 6 Finding 3 (Root cause B), `LoaderStruct.data` is itself a fixup-table-only
  /// pointer, just like `Tex.FlicPtr`. The reference (btbl.rs::decode_entry) reads
  /// `entry.data_address` directly via a plain address read with no further relocation lookup,
  /// which only works if `data_address` was already resolved through the relocation table when the
  /// loader-entry list was built - so this does the same one-hop resolution here, falling back to
  /// the raw field value when it isn't a listed relocation source (e.g. a v1/v4 archive without a
  /// populated relocation table, where the raw on-disk value is already the intended address).
  /// </summary>
  private Dictionary<uint, List<byte[]>> ReadLoaderExtraData(
    BinaryReader reader, FileTypeBlock[] blocks, Version version, List<LoaderHeader> loaderHeaders
  ) {
    var extraData = new Dictionary<uint, List<byte[]>>();
    if (blocks.Length <= 2 || blocks[2].Blocks.Count <= 1) return extraData;

    var loaderBlock = blocks[2].Blocks[1];
    if (loaderBlock.Data == null || loaderBlock.Size == 0) return extraData;

    // LoaderStruct: LoaderType(4), data(ptr, 4), HasExtraData(4), Sym(ptr, 4), SymbolsToResolve(4)
    const int loaderStructSize = 20;
    if (loaderBlock.Size % loaderStructSize != 0 || loaderBlock.Data.Length != loaderBlock.Size)
      throw new InvalidDataException("OVL loader table has a truncated record.");
    var loaderCount = loaderBlock.Data.Length / loaderStructSize;
    ulong symbolReferenceCount = 0;
    foreach (var i in Enumerable.Range(0, loaderCount)) {
      var offset = i * loaderStructSize;
      var loaderType = BitConverter.ToUInt32(loaderBlock.Data, offset);
      var rawDataPtr = BitConverter.ToUInt32(loaderBlock.Data, offset + 4);
      var dataFieldAddress = loaderBlock.RelativeOffset + Convert.ToUInt32(offset + 4);
      var dataPtr = TryGetRelocationSource(dataFieldAddress, out var resolved) ? resolved : rawDataPtr;
      var hasExtraDataRaw = BitConverter.ToUInt32(loaderBlock.Data, offset + 8);
      symbolReferenceCount += BitConverter.ToUInt32(loaderBlock.Data, offset + 16);
      if (symbolReferenceCount > uint.MaxValue)
        throw new InvalidDataException("OVL SymbolRef count exceeds the addressable range.");
      // v5 packs a 16-bit extra-data count and a 16-bit unknown into this field; v1/v4 use it whole.
      var hasExtraData = version == Version.Five ? hasExtraDataRaw & 0xFFFF : hasExtraDataRaw;

      // LoaderType is a direct, on-disk-position index into loaderHeaders (Part 6 Finding 1).
      if (loaderType >= loaderHeaders.Count)
        throw new InvalidDataException($"OVL loader type index {loaderType} is out of range.");
      loaderEntriesInOrder.Add(new OvlLoaderEntry(
        loaderHeaders[Convert.ToInt32(loaderType)].Tag,
        dataPtr,
        loaderBlock.Path,
        loaderBlock.RelativeOffset + Convert.ToUInt32(offset)));

      foreach (var _ in Enumerable.Range(0, ToCount(hasExtraData, "loader extra-data count"))) {
        var chunkSize = ReadUInt32(reader, "loader extra-data size");
        var chunk = ReadBytes(
          reader, ToCount(chunkSize, "loader extra-data size"), "loader extra-data chunk");

        if (!extraData.TryGetValue(dataPtr, out var chunks))
          extraData[dataPtr] = chunks = [];
        chunks.Add(chunk);
      }
    }
    IndexSymbolReferenceBlock(
      blocks, version, Convert.ToUInt32(symbolReferenceCount));
    return extraData;
  }

  private void IndexSymbolReferenceBlock(
    FileTypeBlock[] blocks,
    Version version,
    uint recordCount
  ) {
    // LodSymRefManager's aggregate SymbolsToResolve count is the serialized discriminator. Block
    // position or relocation-looking contents alone cannot prove this is a SymbolRef table.
    if (recordCount == 0) return;
    if (blocks.Length <= 2 || blocks[2].Blocks.Count <= 2)
      throw new InvalidDataException("OVL SymbolRef table is missing.");
    var block = blocks[2].Blocks[2];
    var stride = version == Version.One ? 12 : 16;
    var expectedSize = Convert.ToUInt64(recordCount) * Convert.ToUInt64(stride);
    if (expectedSize > uint.MaxValue || block.Size != expectedSize ||
        block.Data == null || Convert.ToUInt64(block.Data.Length) != expectedSize)
      throw new InvalidDataException(
        $"OVL SymbolRef table size {block.Size} does not match " +
        $"{recordCount} records of {stride} bytes.");
    symbolReferenceBlocksInOrder.Add(
      new OvlBlockEntry(
        block.RelativeOffset, block.Data, block.Path, version, stride, recordCount));
  }

  private static uint ReadV5References(BinaryReader reader, out uint subVersionFlag) {
    subVersionFlag = ReadUInt32(reader, "v5 subversion flag");
    if (subVersionFlag == 0) return ReadUInt32(reader, "v5 reference count");

    ReadBytes(reader, 12, "v5 extended header");
    var terminated = false;
    var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
    if (remaining > int.MaxValue)
      throw new InvalidDataException("OVL v5 extended-header string exceeds the supported size.");
    foreach (var _ in Enumerable.Range(0, Convert.ToInt32(remaining))) {
      if (reader.ReadByte() != 0) continue;
      terminated = true;
      break;
    }
    if (!terminated) throw new InvalidDataException("Truncated OVL v5 extended-header string.");

    var padding = Convert.ToInt32((4 - reader.BaseStream.Position % 4) % 4);
    ReadBytes(reader, padding, "v5 extended-header padding");
    return ReadUInt32(reader, "v5 reference count");
  }

  private static List<LoaderHeader> ReadLoaderHeaders(BinaryReader reader, int fileTypeCount) {
    var loaderHeaders = new List<LoaderHeader>();
    foreach (var _ in Enumerable.Range(0, fileTypeCount)) {
      var loaderLen = ReadUInt16(reader, "loader name length");
      var loader = Encoding.ASCII.GetString(ReadBytes(reader, loaderLen, "loader name"));

      var nameLen = ReadUInt16(reader, "loader display-name length");
      var name = Encoding.ASCII.GetString(ReadBytes(reader, nameLen, "loader display name"));

      var loaderType = ReadUInt32(reader, "loader type");

      var tagLen = ReadUInt16(reader, "loader tag length");
      var tag = Encoding.ASCII.GetString(ReadBytes(reader, tagLen, "loader tag"));

      loaderHeaders.Add(new LoaderHeader(loader, name, loaderType, tag, 0));
    }
    return loaderHeaders;
  }

  private static void ReadV5SymbolCounts(BinaryReader reader, List<LoaderHeader> loaderHeaders) {
    foreach (var _ in Enumerable.Range(0, loaderHeaders.Count)) {
      var idx = ReadUInt32(reader, "v5 loader symbol-count index");
      var symCount = ReadUInt32(reader, "v5 loader symbol count");
      if (idx >= loaderHeaders.Count)
        throw new InvalidDataException($"OVL loader symbol-count index {idx} is out of range.");
      loaderHeaders[Convert.ToInt32(idx)] = loaderHeaders[Convert.ToInt32(idx)] with {
        SymbolCount = symCount,
      };
    }
  }

  private static FileTypeBlock[] ReadFileTypeBlocks(
    string filePath, BinaryReader reader, Version version, uint subVersionFlag
  ) {
    var blocks = new FileTypeBlock[9];
    var totalBlockCount = 0;
    foreach (var i in Enumerable.Range(0, blocks.Length)) {
      blocks[i] = new FileTypeBlock { Count = ReadUInt32(reader, $"block type {i} count") };
      if (version > Version.One) {
        ReadUInt32(reader, $"block type {i} unknown field");
        if (version == Version.Five && (subVersionFlag & 1) != 0)
          blocks[i].UnknownV5Extra = ReadUInt32(reader, $"block type {i} v5 field");
      }

      var blockCount = PreflightBlockCount(
        reader, blocks[i].Count, totalBlockCount, blocks.Length - i - 1,
        version, subVersionFlag, i);
      totalBlockCount += blockCount;
      blocks[i].Blocks = [.. Enumerable.Range(0, blockCount)
        .Select(_ => new FileBlock() { Path = filePath})];

      // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
      if (version > Version.One) foreach (var block in blocks[i].Blocks) {
        block.Size = ReadUInt32(reader, $"block type {i} size");
        if (block.Size > uint.MaxValue - blocks[i].Size)
          throw new InvalidDataException($"OVL block type {i} total size exceeds 32 bits.");
        blocks[i].Size += block.Size;
      }

      if (blocks[i].Size > 0)
        Debug.WriteLine($"[OVL] Type {i} count {blocks[i].Count} totalSize {blocks[i].Size}");
    }
    return blocks;
  }

  private static int PreflightBlockCount(
    BinaryReader reader, uint rawCount, int totalBlockCount, int remainingTypeCount,
    Version version, uint subVersionFlag, int typeIndex
  ) {
    var count = ToCount(rawCount, $"block type {typeIndex} count");
    if (count > MaxBlocksPerArchive - totalBlockCount)
      throw new InvalidDataException(
        $"OVL block count exceeds the supported maximum of {MaxBlocksPerArchive}.");

    var bytesPerTypeHeader = version switch {
      Version.One => sizeof(uint),
      Version.Five when (subVersionFlag & 1) != 0 => sizeof(uint) * 3,
      _ => sizeof(uint) * 2,
    };
    var pendingSizeCount = version == Version.One ? totalBlockCount + count : count;
    var requiredBytes = Convert.ToInt64(pendingSizeCount) * sizeof(uint) +
      Convert.ToInt64(remainingTypeCount) * bytesPerTypeHeader;
    EnsureRemaining(reader, requiredBytes, $"block type {typeIndex} metadata");
    return count;
  }

  private static void ReadPostBlockUnknowns(BinaryReader reader, Version version) {
    switch (version) {
      case Version.Four:
        ReadBytes(reader, 8, "v4 post-block metadata");
        break;
      case >= Version.Five: {
        var bytesCount = ReadUInt32(reader, "v5 post-block byte count");
        ReadBytes(
          reader, ToCount(bytesCount, "v5 post-block byte count"), "v5 post-block bytes");

        var longCount = ReadUInt32(reader, "v5 post-block uint count");
        var longBytes = Convert.ToInt64(longCount) * sizeof(uint);
        if (longBytes > int.MaxValue)
          throw new InvalidDataException("OVL v5 post-block uint data exceeds the supported size.");
        ReadBytes(reader, Convert.ToInt32(longBytes), "v5 post-block uint data");
        break;
      }
    }
  }

  private void ReadBlockData(BinaryReader reader, FileTypeBlock[] blocks, Version version) {
    foreach (var i in Enumerable.Range(0, blocks.Length)) {
      foreach (var block in blocks[i].Blocks) {
        if (version == Version.One && block.Size == 0) {
          block.Size = ReadUInt32(reader, $"v1 block type {i} size");
        }

        block.RelativeOffset = relocationOffset;
        block.TypeIndex = i;
        if (block.Size > uint.MaxValue - relocationOffset)
          throw new InvalidDataException(
            "OVL block address range exceeds the 32-bit relocation space.");
        relocationOffset += block.Size;

        if (block.Size == 0) continue;
        if (block.Size > int.MaxValue)
          throw new InvalidDataException($"OVL block type {i} exceeds the supported size.");
        block.Offset = Convert.ToUInt64(reader.BaseStream.Position);
        block.Data = ReadBytes(reader, Convert.ToInt32(block.Size), $"block type {i} data");
        Debug.WriteLine($"[OVL] Seek past block {i} size {block.Size} at relOffset 0x{block.RelativeOffset:X}");
      }
    }
  }

  /// <summary>
  /// Reads the relocation-fixup table (Part 6 Finding 3 / rct3tex.cpp:1830-1842's DoReloc): a flat
  /// list of <c>relCount</c> source addresses, each naming a location in block data whose raw
  /// stored value should be trusted as a real pointer once "fixed up" by the archive's own loader
  /// (previously discarded entirely by the method this replaces, <c>SkipRelocations</c>). Consumes
  /// exactly the same number of bytes from the stream as before - it just also records what it
  /// reads into <see cref="relocations"/> instead of discarding it.
  /// </summary>
  private void ReadRelocations(BinaryReader reader) {
    var relCount = ReadUInt32(reader, "relocation count");
    var bytesToRead = Convert.ToInt64(relCount) * 4;
    EnsureRemaining(reader, bytesToRead, "relocation table");

    foreach (var _ in Enumerable.Range(0, ToCount(relCount, "relocation count"))) {
      var sourceAddress = reader.ReadUInt32();
      var block = FindBlock(sourceAddress);
      if (block?.Data == null ||
          !TryGetBlockSlice(block, sourceAddress, sizeof(uint), out var offset))
        continue;

      relocations[sourceAddress] = BitConverter.ToUInt32(block.Data, offset);
    }
  }

  private void ExtractResources() {
    var allBlocks = allFileTypeBlocks
        .SelectMany(ftb => ftb.SelectMany(b => b.Blocks))
        .Where(b => b.Data != null)
        .ToList();

    for (var fileIndex = 0; fileIndex < allFileTypeBlocks.Count; fileIndex++) {
      var blocks = allFileTypeBlocks[fileIndex];
      if (blocks.Length <= 2 || blocks[2].Blocks.Count == 0) continue;

      var symbolBlock = blocks[2].Blocks[0];
      if (symbolBlock.Size == 0) continue;

      // Symbol record layout is fixed by archive version, never guessed: v1 uses the 12-byte
      // SymbolStruct (Symbol, data, IsPointer); v4/v5 use the 16-byte SymbolStruct2, which adds a
      // 2-byte IsPointer/2-byte unknown/4-byte name hash in place of the 4-byte IsPointer. Neither
      // layout has a header before the symbol table. Guessing the stride from the block size (e.g.
      // any size that is a multiple of 48 divides evenly by both 12 and 16) silently misaligns every
      // name/data pointer read for the rest of the file once it picks wrong.
      var version = fileIndex < allVersions.Count ? allVersions[fileIndex] : Version.Unknown;
      var symbolSize = version == Version.One ? 12 : 16;
      if (symbolBlock.Size % symbolSize != 0) continue;

      var loaderHeaders = fileIndex < allLoaderHeaders.Count ? allLoaderHeaders[fileIndex] : [];
      var loaderIdx = 0;
      var loaderSymbolRemaining = loaderHeaders.Length > 0 ? loaderHeaders[0].SymbolCount : 0u;

      foreach (var symOffset in Enumerable.Range(0, Convert.ToInt32(symbolBlock.Size) / symbolSize)
                 .Select(i => i * symbolSize)) {
        var namePtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset);
        var rawName = ReadString(allBlocks, namePtr);
        if (rawName == null) continue;

        var dataPtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 4);

        // Every symbol name is written as "Name:Tag" (e.g. "RomPil_1H:svd") regardless of version,
        // so the tag suffix is the authoritative source for FileType and the real resource name.
        // The loader-header/SymbolCount walk below only groups symbols contiguously by tag for v5
        // archives; v1/v4 archives carry no per-loader symbol count at all, so it can only serve as
        // a fallback when a name is somehow missing its tag suffix.
        var colonIndex = rawName.LastIndexOf(':');
        var name = rawName;
        var fileType = FileType.Unknown;
        if (colonIndex >= 0) {
          var candidateType = rawName[(colonIndex + 1)..].ToFileType();
          if (candidateType != FileType.Unknown) {
            name = rawName[..colonIndex];
            fileType = candidateType;
          }
        }

        if (loaderIdx < loaderHeaders.Length && loaderSymbolRemaining == 0) {
          loaderIdx = Math.Min(loaderIdx + 1, loaderHeaders.Length - 1);
          loaderSymbolRemaining = loaderHeaders[loaderIdx].SymbolCount;
        }
        if (fileType == FileType.Unknown && loaderIdx < loaderHeaders.Length)
          fileType = loaderHeaders[loaderIdx].Tag.ToFileType();

        var resolvedBlock = allBlocks.FirstOrDefault(fb => TryGetBlockOffset(fb, dataPtr, out _));
        if (resolvedBlock?.Data != null &&
            TryGetBlockOffset(resolvedBlock, dataPtr, out var blockOffset)) {
          var relOffset = Convert.ToUInt32(blockOffset);
          // Neither SymbolStruct nor SymbolStruct2 stores a resource byte size; the archive
          // format has no reliable per-entry length, so read to the end of the resolved block.
          var effectiveSize = Convert.ToUInt32(resolvedBlock.Data.Length - blockOffset);
          var absoluteOffset = resolvedBlock.Offset + relOffset;
          if (absoluteOffset > uint.MaxValue) continue;
          var file = new OvlFile(name, fileType, resolvedBlock.Path);
          entries[file] = new OvlEntry(
            Convert.ToUInt32(absoluteOffset),
            effectiveSize
          );
          entryDataPtrs[file] = dataPtr;
          symbolsByDataPointer.TryAdd(dataPtr, file);
        }

        if (loaderSymbolRemaining > 0) loaderSymbolRemaining--;
      }
    }

    ReadSymbolReferences();
  }

  private void ReadSymbolReferences() {
    var filesByNameAndType = new Dictionary<(string Name, FileType Type), OvlFile>();
    foreach (var file in entries.Keys)
      filesByNameAndType.TryAdd((file.Name, file.Type), file);

    foreach (var block in symbolReferenceBlocksInOrder) {
      foreach (var index in Enumerable.Range(0, Convert.ToInt32(block.RecordCount))) {
        var recordAddress = block.Address + Convert.ToUInt32(index * block.RecordStride);
        if (!TryGetRelocationSource(recordAddress + 4, out var symbolAddress) ||
            !TryResolveString(symbolAddress, out var rawName)) continue;

        var (name, type) = SplitSymbolNameTag(rawName);
        if (type == FileType.Unknown) continue;
        symbolReferences.Add((name, type));
        if (!TryGetRelocationSource(recordAddress, out var fieldAddress) ||
            !filesByNameAndType.TryGetValue((name, type), out var target)) continue;
        symbolReferenceTargets.TryAdd(fieldAddress, target);
      }
    }
  }

  private static (string Name, FileType Type) SplitSymbolNameTag(string rawName) {
    var separator = rawName.LastIndexOf(':');
    if (separator < 0) return (rawName, FileType.Unknown);

    var type = rawName[(separator + 1)..].ToFileType();
    return type == FileType.Unknown ? (rawName, type) : (rawName[..separator], type);
  }

  private static string? ReadString(List<FileBlock> blocks, uint ptr) {
    foreach (var fb in blocks.Where(fb => fb.TypeIndex == 0)) {
      if (fb.Data == null || !TryGetBlockOffset(fb, ptr, out var offset)) continue;

      var end = Array.IndexOf(fb.Data, (byte)0, offset);
      if (end < 0) end = fb.Data.Length;
      return Encoding.ASCII.GetString(fb.Data, offset, end - offset);
    }
    foreach (var fb in blocks) {
      if (fb.Data == null || !TryGetBlockOffset(fb, ptr, out var offset)) continue;

      var end = Array.IndexOf(fb.Data, Convert.ToByte(0), offset);
      if (end < 0) end = fb.Data.Length;
      return Encoding.ASCII.GetString(fb.Data, offset, end - offset);
    }
    return null;
  }

  private void Dispose(bool disposing) {
    if (disposed) return;

    // Empty large fields
    if (disposing) {
      entries.Clear();
      entryDataPtrs.Clear();
      symbolsByDataPointer.Clear();
      symbolReferenceTargets.Clear();
      symbolReferences.Clear();
      allFileTypeBlocks.Clear();
      allLoaderHeaders.Clear();
      allExtraData.Clear();
      loaderEntriesInOrder.Clear();
      symbolReferenceBlocksInOrder.Clear();
    }

    disposed = true;
  }

  public void Dispose() {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    // ReSharper disable once GCSuppressFinalizeForTypeWithoutDestructor because this class doesn't use unamanged data
    GC.SuppressFinalize(this);
  }
}

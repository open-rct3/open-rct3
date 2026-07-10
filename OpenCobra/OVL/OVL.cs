// OVL.cs
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

  public readonly string Name = name;
  public Version Version => version;

  private Version version;
  private readonly Dictionary<OvlFile, OvlEntry> entries = [];
  private readonly Dictionary<OvlFile, uint> entryDataPtrs = [];
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
  // Ordered (per file, in on-disk LoaderStruct order) (Tag, DataAddress) pairs - see Part 6
  // Finding 4: "btbl"/"flic" are loader-category tags only, never discoverable as classified
  // symbols, so callers that need every loader instance (not just symbol-backed resources) must
  // walk this instead of ovl.Keys.
  private readonly List<(string Tag, uint DataAddress)> loaderEntriesInOrder = [];
  private uint relocationOffset;
  private bool disposed = false;

  /// <summary>
  /// Every loader instance in the archive, in on-disk order (common file first, then unique), with
  /// its category tag (e.g. "btbl", "flic", "tex") and relocation-resolved data address. Unlike
  /// <see cref="Keys"/>, this includes loader categories (like "btbl"/"flic") that are never
  /// classified as their own symbol - see Part 6 Finding 4 of the texture-decoding bug doc.
  /// </summary>
  internal IReadOnlyList<(string Tag, uint DataAddress)> LoaderEntriesInOrder => loaderEntriesInOrder;

  /// <summary>Reads <paramref name="length"/> raw bytes at a relocation-resolved data address.</summary>
  public bool TryReadBytes(uint address, int length, [MaybeNullWhen(false)] out byte[] data) {
    if (!TryResolveRelocation(address, out var block, out var offset) || offset + length > block.Length) {
      data = null;
      return false;
    }
    data = block.AsSpan(Convert.ToInt32(offset), length).ToArray();
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
    // Otherwise, return true if either the name or type matches.
    return nameProvided
      ? nameMatch && (type == null || typeMatch)
      : nameMatch || typeMatch;
  });

  /// <summary>Read the resource data for a given file.</summary>
  public byte[]? ReadResource(OvlFile file) {
    var entry = entries.GetValueOrDefault(file);
    if (entry == null) return null;

    var bytes = new byte[entry.Size];
    using var fs = File.OpenRead(file.Path);
    fs.Seek(Convert.ToInt32(entry.Offset), SeekOrigin.Begin);
    fs.ReadExactly(bytes, 0, Convert.ToInt32(entry.Size));
    return bytes;
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
    if (resolvedBlock?.Data == null) {
      data = null;
      offset = 0;
      return false;
    }

    data = resolvedBlock.Data;
    offset = dataPtr - resolvedBlock.RelativeOffset;
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
    .FirstOrDefault(fb => fb.Data != null && address >= fb.RelativeOffset && address < fb.RelativeOffset + fb.Size);

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
    foreach (var extraData in allExtraData) {
      if (!extraData.TryGetValue(dataPtr, out var found)) continue;
      chunks = found;
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

  /// <summary>
  /// Resolves a relocated string pointer to its text value.
  /// </summary>
  /// <param name="ptr">Relative offset pointer to a null-terminated ASCII string in OVL block data</param>
  /// <param name="value">The resolved string, or null if unresolved</param>
  /// <returns>True if resolution succeeded.</returns>
  public bool TryResolveString(uint ptr, [MaybeNullWhen(false)] out string value) {
    var resolvedBlock = ptr == 0 ? null : FindBlock(ptr);
    if (resolvedBlock?.Data == null) {
      value = null;
      return false;
    }

    var offset = Convert.ToInt32(ptr - resolvedBlock.RelativeOffset);
    var end = Array.IndexOf(resolvedBlock.Data, (byte)0, offset);
    if (end < 0) end = resolvedBlock.Data.Length;
    value = Encoding.ASCII.GetString(resolvedBlock.Data, offset, end - offset);
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

    var magic = reader.ReadUInt32();
    Debug.Assert(magic == 0x4b524746, "Invalid OVL magic");
    var reserved = reader.ReadUInt32();
    var version = (Version) reader.ReadUInt32();
    var headerRefs = reader.ReadUInt32();

    Debug.WriteLine($"[OVL] Loading {Path.GetFileName(filePath)} (v{version})");

    var subVersionFlag = 0u;
    var referenceCount = version switch {
      Version.Five => ReadV5References(reader, out subVersionFlag),
      Version.Four => reader.ReadUInt32(),
      _ => headerRefs
    };

    Debug.WriteLine($"[OVL] subVersionFlag: {subVersionFlag}, referenceCount: {referenceCount}");

    for (var i = 0; i < referenceCount && reader.BaseStream.Position < reader.BaseStream.Length; i++) {
      var len = reader.ReadUInt16();
      if (len > 0 && reader.BaseStream.Position + len <= reader.BaseStream.Length)
        reader.ReadBytes(len);
    }

    var loaderHeaders = new List<LoaderHeader>();
    if (reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
      reader.ReadUInt32(); // OvlHeader2.unk
      var fileTypeCount = reader.ReadUInt32();
      if (fileTypeCount > 0 && fileTypeCount < 1024) {
        loaderHeaders = ReadLoaderHeaders(reader, (int)fileTypeCount);
        if (version == Version.Five) ReadV5SymbolCounts(reader, loaderHeaders);
      }
    }
    allLoaderHeaders.Add([.. loaderHeaders]);
    allVersions.Add(version);

    var blocks = ReadFileTypeBlocks(filePath, reader, version, subVersionFlag);
    allFileTypeBlocks.Add(blocks);

    ReadPostBlockUnknowns(reader, version);
    ReadBlockData(reader, blocks, version);
    ReadRelocations(reader);

    if (version >= Version.Four && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
      if (version == Version.Four || (subVersionFlag & 1) != 0) reader.ReadBytes(4);
    }

    allExtraData.Add(ReadLoaderExtraData(reader, blocks, version, loaderHeaders));

    return version;
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
    var loaderCount = Convert.ToInt32(loaderBlock.Size) / loaderStructSize;
    for (var i = 0; i < loaderCount; i++) {
      var offset = i * loaderStructSize;
      var loaderType = BitConverter.ToUInt32(loaderBlock.Data, offset);
      var rawDataPtr = BitConverter.ToUInt32(loaderBlock.Data, offset + 4);
      var dataFieldAddress = loaderBlock.RelativeOffset + Convert.ToUInt32(offset + 4);
      var dataPtr = TryGetRelocationSource(dataFieldAddress, out var resolved) ? resolved : rawDataPtr;
      var hasExtraDataRaw = BitConverter.ToUInt32(loaderBlock.Data, offset + 8);
      // v5 packs a 16-bit extra-data count and a 16-bit unknown into this field; v1/v4 use it whole.
      var hasExtraData = version == Version.Five ? hasExtraDataRaw & 0xFFFF : hasExtraDataRaw;

      // LoaderType is a direct, on-disk-position index into loaderHeaders (Part 6 Finding 1).
      if (loaderType < loaderHeaders.Count)
        loaderEntriesInOrder.Add((loaderHeaders[Convert.ToInt32(loaderType)].Tag, dataPtr));

      for (var c = 0; c < hasExtraData; c++) {
        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
        var chunkSize = reader.ReadUInt32();
        if (reader.BaseStream.Position + chunkSize > reader.BaseStream.Length) break;
        var chunk = reader.ReadBytes(Convert.ToInt32(chunkSize));

        if (!extraData.TryGetValue(dataPtr, out var chunks))
          extraData[dataPtr] = chunks = [];
        chunks.Add(chunk);
      }
    }
    return extraData;
  }

  private static uint ReadV5References(BinaryReader reader, out uint subVersionFlag) {
    subVersionFlag = reader.ReadUInt32();
    if (subVersionFlag == 0 || reader.BaseStream.Position + 12 > reader.BaseStream.Length)
      return reader.BaseStream.Position + 4 <= reader.BaseStream.Length ? reader.ReadUInt32() : 0;
    reader.ReadBytes(12);
    while (reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadByte() != 0) { }
    while (reader.BaseStream.Position < reader.BaseStream.Length && reader.BaseStream.Position % 4 != 0)
      reader.ReadByte();
    return reader.BaseStream.Position + 4 <= reader.BaseStream.Length ? reader.ReadUInt32() : 0;
  }

  private static List<LoaderHeader> ReadLoaderHeaders(BinaryReader reader, int fileTypeCount) {
    var loaderHeaders = new List<LoaderHeader>();
    for (var i = 0; i < fileTypeCount; i++) {
      if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
      var loaderLen = reader.ReadUInt16();
      if (reader.BaseStream.Position + loaderLen > reader.BaseStream.Length) break;
      var loader = Encoding.ASCII.GetString(reader.ReadBytes(loaderLen));

      if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
      var nameLen = reader.ReadUInt16();
      if (reader.BaseStream.Position + nameLen > reader.BaseStream.Length) break;
      var name = Encoding.ASCII.GetString(reader.ReadBytes(nameLen));

      if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
      var loaderType = reader.ReadUInt32();

      if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
      var tagLen = reader.ReadUInt16();
      if (reader.BaseStream.Position + tagLen > reader.BaseStream.Length) break;
      var tag = Encoding.ASCII.GetString(reader.ReadBytes(tagLen));

      loaderHeaders.Add(new LoaderHeader(loader, name, loaderType, tag, 0));
    }
    return loaderHeaders;
  }

  private static void ReadV5SymbolCounts(BinaryReader reader, List<LoaderHeader> loaderHeaders) {
    for (var i = 0; i < loaderHeaders.Count; i++) {
      if (reader.BaseStream.Position + 8 > reader.BaseStream.Length) break;
      var idx = reader.ReadUInt32();
      var symCount = reader.ReadUInt32();
      if (idx < loaderHeaders.Count)
        loaderHeaders[(int)idx] = loaderHeaders[(int)idx] with { SymbolCount = symCount };
    }
  }

  private static FileTypeBlock[] ReadFileTypeBlocks(
    string filePath, BinaryReader reader, Version version, uint subVersionFlag
  ) {
    var blocks = new FileTypeBlock[9];
    for (var i = 0; i < blocks.Length; i++) {
      if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) {
        blocks[i] = new FileTypeBlock();
        continue;
      }
      blocks[i] = new FileTypeBlock { Count = reader.ReadUInt32() };
      if (version > Version.One && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
        reader.ReadUInt32();
        if (version == Version.Five && (subVersionFlag & 1) != 0 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
          blocks[i].UnknownV5Extra = reader.ReadUInt32();
      }

      blocks[i].Blocks = [.. Enumerable.Range(0, Convert.ToInt32(blocks[i].Count))
        .Select(_ => new FileBlock() { Path = filePath})];

      // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
      if (version > Version.One) foreach (var block in blocks[i].Blocks) {
        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
        block.Size = reader.ReadUInt32();
        blocks[i].Size += block.Size;
      }

      if (blocks[i].Size > 0)
        Debug.WriteLine($"[OVL] Type {i} count {blocks[i].Count} totalSize {blocks[i].Size}");
    }
    return blocks;
  }

  private static void ReadPostBlockUnknowns(BinaryReader reader, Version version) {
    switch (version) {
      case Version.Four when reader.BaseStream.Position + 8 <= reader.BaseStream.Length:
        reader.ReadBytes(8);
        break;
      case >= Version.Five when reader.BaseStream.Position + 4 <= reader.BaseStream.Length: {
        var bytesCount = reader.ReadUInt32();
        if (bytesCount > 0 && bytesCount <= reader.BaseStream.Length - reader.BaseStream.Position)
          reader.ReadBytes(Convert.ToInt32(bytesCount));

        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return;
        var longCount = reader.ReadUInt32();
        if (Convert.ToInt64(longCount * 4) <= reader.BaseStream.Length - reader.BaseStream.Position)
          reader.ReadBytes(Convert.ToInt32(longCount * 4));
        break;
      }
    }
  }

  private void ReadBlockData(BinaryReader reader, FileTypeBlock[] blocks, Version version) {
    for (var i = 0; i < blocks.Length; i++) {
      foreach (var block in blocks[i].Blocks) {
        if (version == Version.One && block.Size == 0) {
          if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return;
          block.Size = reader.ReadUInt32();
        }

        block.RelativeOffset = relocationOffset;
        block.TypeIndex = i;
        relocationOffset += block.Size;

        if (block.Size <= 0 || reader.BaseStream.Position + block.Size > reader.BaseStream.Length) continue;
        block.Offset = Convert.ToUInt64(reader.BaseStream.Position);
        block.Data = reader.ReadBytes(Convert.ToInt32(block.Size));
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
    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return;
    var relCount = reader.ReadUInt32();
    var bytesToRead = Convert.ToInt64(relCount) * 4;
    if (bytesToRead > reader.BaseStream.Length - reader.BaseStream.Position) return;

    for (var i = 0; i < relCount; i++) {
      var sourceAddress = reader.ReadUInt32();
      var block = FindBlock(sourceAddress);
      if (block?.Data == null) continue;

      var offset = Convert.ToInt32(sourceAddress - block.RelativeOffset);
      if (offset + 4 > block.Data.Length) continue;

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

        var resolvedBlock = allBlocks.FirstOrDefault(fb => dataPtr >= fb.RelativeOffset && dataPtr < fb.RelativeOffset + fb.Size);
        if (resolvedBlock != null) {
          var relOffset = dataPtr - resolvedBlock.RelativeOffset;
          // Neither SymbolStruct nor SymbolStruct2 stores a resource byte size; the archive
          // format has no reliable per-entry length, so read to the end of the resolved block.
          var effectiveSize = resolvedBlock.Size - relOffset;
          var file = new OvlFile(name, fileType, resolvedBlock.Path);
          entries[file] = new OvlEntry(
            Convert.ToUInt32(resolvedBlock.Offset + relOffset),
            effectiveSize
          );
          entryDataPtrs[file] = dataPtr;
        }

        if (loaderSymbolRemaining > 0) loaderSymbolRemaining--;
      }
    }
  }

  private static string? ReadString(List<FileBlock> blocks, uint ptr) {
    foreach (var fb in blocks.Where(fb => fb.TypeIndex == 0)) {
      if (fb.Data == null) continue;
      if (ptr < fb.RelativeOffset || ptr >= fb.RelativeOffset + fb.Size) continue;

      var offset = (int)(ptr - fb.RelativeOffset);
      var end = Array.IndexOf(fb.Data, (byte)0, offset);
      if (end < 0) end = fb.Data.Length;
      return Encoding.ASCII.GetString(fb.Data, offset, end - offset);
    }
    foreach (var fb in blocks) {
      if (fb.Data == null) continue;
      if (ptr < fb.RelativeOffset || ptr >= fb.RelativeOffset + fb.Size) continue;

      var offset = (int)(ptr - fb.RelativeOffset);
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
      allFileTypeBlocks.Clear();
      allLoaderHeaders.Clear();
      allExtraData.Clear();
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

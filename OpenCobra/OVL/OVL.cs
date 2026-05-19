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
  public uint Offset;
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

internal class FileBlockGroup {
  public uint Count;
  public uint Size;
  public uint UnknownV5Extra;
  public List<FileBlock> Blocks = [];
}

internal struct RelocationSource(int index, uint offset) {
  /// <summary>
  /// The index of this relocation in the relocation table.
  /// </summary>
  public readonly int Index = index;
  /// <summary>
  /// The location of a pointer stored in the archive, relative to the start of the archive's data section.
  /// </summary>
  public readonly uint Offset = offset;
}

internal record Relocation(RelocationSource Source) {
  public uint? TargetOffset { get; internal set; } = null;
}

/// <summary>Represents an OVL archive, providing methods to load and extract resource entries.</summary>
public sealed class Ovl(string name) : IDictionary<OvlFile, OvlEntry>, IDisposable {
  public const string UnnamedOvl = "Untitled OVL";

  public readonly string Name = name;
  public Version Version { get; private set; }

  private readonly Dictionary<OvlFile, OvlEntry> entries = [];
  private readonly List<LoaderHeader[]> allLoaderHeaders = [];
  private readonly List<FileBlockGroup[]> fileBlocks = [];
  private readonly List<Relocation> relocations = [];
  private uint relocationOffset;
  private bool disposed = false;

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
    ovl.Version = ovl.IngestArchive(ovlPath);
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
    using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    fs.Seek(Convert.ToInt32(entry.Offset), SeekOrigin.Begin);
    fs.ReadExactly(bytes, 0, Convert.ToInt32(entry.Size));
    return bytes;
  }

  /// <summary>
  /// Read the resource data for a given relocation pointer.
  /// </summary>
  public byte[]? ReadResource(RelocationPointer ptr) {
    var relocation = relocations.FirstOrDefault(rel => rel.Source.Offset == ptr.Value);
    if (relocation == null) return null;

    // Look up which file block ptr is relocated to
    var blocks =
      from groups in fileBlocks
      from @group in groups
      from fileBlock in @group.Blocks
      let offset = ptr.Value
      where offset >= fileBlock.RelativeOffset && offset < fileBlock.RelativeOffset + fileBlock.Size
      where fileBlock.Offset - relocationOffset == ptr.Value
      select fileBlock;
    var block = blocks.FirstOrDefault();
    if (block == null) return null;

    // Read the block of data from the archive
    var entry = new OvlEntry(block.Offset, block.Size);
    var bytes = new byte[entry.Size];
    using var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    fs.Seek(Convert.ToInt32(entry.Offset), SeekOrigin.Begin);
    fs.ReadExactly(bytes, 0, Convert.ToInt32(entry.Size));
    return bytes;
  }

  private Version IngestArchive(string ovlPath) {
    // ReSharper disable once LocalVariableHidesMember
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

    // Resolve relocations, if any
    if (relocations.Count > 0) ResolveRelocations();

    // Build dictionary of files in the archive
    ExtractResources();

    return version;
  }

  private Version ProcessFile(string filePath) {
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.UTF8, false);

    var magic = reader.ReadUInt32();
    Debug.Assert(magic == 0x4b524746, "Invalid OVL magic");
    var reserved = reader.ReadUInt32();
    // ReSharper disable once LocalVariableHidesMember
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
      if (fileTypeCount is > 0 and < 1024) {
        loaderHeaders = ReadLoaderHeaders(reader, (int)fileTypeCount);
        if (version == Version.Five) ReadV5SymbolCounts(reader, loaderHeaders);
      }
    }
    allLoaderHeaders.Add([.. loaderHeaders]);

    var blocks = ReadFileTypeBlocks(filePath, reader, version, subVersionFlag);
    fileBlocks.Add(blocks);

    ReadPostBlockUnknowns(reader, version);
    ReadBlockData(reader, blocks, version);
    relocations.AddRange(ReadRelocations(reader));

    if (version < Version.Four || reader.BaseStream.Position + 4 > reader.BaseStream.Length) return version;
    if (version == Version.Four || (subVersionFlag & 1) != 0) reader.ReadBytes(4);

    return version;
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

  private static FileBlockGroup[] ReadFileTypeBlocks(
    string filePath, BinaryReader reader, Version version, uint subVersionFlag
  ) {
    var blocks = new FileBlockGroup[9];
    for (var i = 0; i < blocks.Length; i++) {
      if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) {
        blocks[i] = new FileBlockGroup();
        continue;
      }
      blocks[i] = new FileBlockGroup { Count = reader.ReadUInt32() };
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

  private void ReadBlockData(BinaryReader reader, FileBlockGroup[] blocks, Version version) {
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
        block.Offset = Convert.ToUInt32(reader.BaseStream.Position);
        block.Data = reader.ReadBytes(Convert.ToInt32(block.Size));
        Debug.WriteLine($"[OVL] Seek past block {i} size {block.Size} at relOffset 0x{block.RelativeOffset:X}");
      }
    }
  }

  private static List<Relocation> ReadRelocations(BinaryReader reader) {
    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
      throw new EndOfStreamException("Unexpected end of file");

    var count = reader.ReadUInt32();
    if (count == 0) return [];

    var relocations = reader.ReadBytes(Convert.ToInt32(count * 4));

    return Enumerable.Range(0, Convert.ToInt32(count))
      .Select(i => new Relocation(
        new RelocationSource(i, BitConverter.ToUInt32(relocations, i * 4))
      )).ToList();
  }

  /// <summary>
  /// Resolves resource relocations by following <see cref="Relocation.Source.Offset"/> and assigning
  /// <see cref="Relocation.TargetOffset"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <see cref="Relocation.TargetOffset"/> is the offset of the target resource, relative to the start of the data
  /// block.
  /// </para>
  /// <para>
  /// See the RCT3 Importer reference implementation:
  /// <a href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLDump/OVLDump.cpp#L484">cOVLDump::ResolveRelocation</a>.
  /// </para>
  /// </remarks>
  private void ResolveRelocations() {
    // Threshold where unique file starts (i.e., end of common file's data)
    // This is the relative offset of the first block in the unique file's first block type
    var uniqueStartOffset = fileBlocks.Count > 1 && fileBlocks[1].Length > 0 && fileBlocks[1][0].Blocks.Count > 0
      ? fileBlocks[1][0].Blocks[0].RelativeOffset
      : 0;

    foreach (var rel in relocations) {
      var sourceOffset = rel.Source.Offset;

      // Determine which file (common or unique) the relocation belongs to
      var isCommon = sourceOffset < uniqueStartOffset;

      // Find the block type and block containing this offset
      var block = FindBlockContaining(isCommon ? 0 : 1, sourceOffset);
      if (block?.Data == null) {
        relocations[rel.Source.Index].TargetOffset = null;
        continue;
      }

      // Read the pointer value at sourceOffset location
      var offsetInBlock = sourceOffset - block.RelativeOffset;
      var targetOffset = BitConverter.ToUInt32(block.Data, (int)offsetInBlock);

      // Assign resolved target offset
      relocations[rel.Source.Index].TargetOffset = targetOffset;
    }
  }

  private FileBlock? FindBlockContaining(int fileIndex, uint offset) {
    var blocks = fileBlocks[fileIndex];
    return blocks.SelectMany(
      group => group.Blocks.Where(block => offset >= block.RelativeOffset && offset < block.RelativeOffset + block.Size)
    ).FirstOrDefault();
  }

  private void ExtractResources() {
    var allBlocks = fileBlocks
        .SelectMany(ftb => ftb.SelectMany(b => b.Blocks))
        .Where(b => b.Data != null)
        .ToList();

    for (var fileIndex = 0; fileIndex < fileBlocks.Count; fileIndex++) {
      var blocks = fileBlocks[fileIndex];
      if (blocks.Length <= 2 || blocks[2].Blocks.Count == 0) continue;

      var symbolBlock = blocks[2].Blocks[0];
      if (symbolBlock.Size == 0) continue;

      var symbolSize = 0;
      var blockOffset = 0;
      if (symbolBlock.Size % 16 == 0) {
        symbolSize = 16;
      } else if (symbolBlock.Size > 4 && (symbolBlock.Size - 4) % 16 == 0) {
        symbolSize = 16;
        blockOffset = 4;
      } else if (symbolBlock.Size % 12 == 0) {
        symbolSize = 12;
      } else if (symbolBlock.Size > 4 && (symbolBlock.Size - 4) % 12 == 0) {
        symbolSize = 12;
        blockOffset = 4;
      } else continue;

      var loaderHeaders = fileIndex < allLoaderHeaders.Count ? allLoaderHeaders[fileIndex] : [];
      var loaderIdx = 0;
      var loaderSymbolRemaining = loaderHeaders.Length > 0 ? loaderHeaders[0].SymbolCount : 0u;

      foreach (var symOffset in Enumerable.Range(0, (Convert.ToInt32(symbolBlock.Size) - blockOffset) / symbolSize)
                 .Select(i => blockOffset + i * symbolSize)) {
        var namePtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset);
        var name = ReadString(allBlocks, namePtr);
        if (name == null) continue;

        var dataPtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 4);
        var size = symbolSize == 16 ? BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 12) : 0u;

        if (loaderIdx < loaderHeaders.Length && loaderSymbolRemaining == 0) {
          loaderIdx = Math.Min(loaderIdx + 1, loaderHeaders.Length - 1);
          loaderSymbolRemaining = loaderHeaders[loaderIdx].SymbolCount;
        }

        var fileType = loaderIdx < loaderHeaders.Length
            ? loaderHeaders[loaderIdx].Tag.ToFileType()
            : FileType.Unknown;
        if (name.Contains(':')) {
          var parts = name.Split(':');
          name = parts[0];
          fileType = parts[1].ToFileType();
        }

        var resolvedBlock = allBlocks.FirstOrDefault(fb => dataPtr >= fb.RelativeOffset && dataPtr < fb.RelativeOffset + fb.Size);
        if (resolvedBlock == null) continue;

        var relOffset = dataPtr - resolvedBlock.RelativeOffset;
        var effectiveSize = size == 0
            ? resolvedBlock.Size - relOffset
            : Math.Min(size, resolvedBlock.Size - relOffset);
        entries[new OvlFile(name, fileType, resolvedBlock.Path)] = new OvlEntry(
          Convert.ToUInt32(resolvedBlock.Offset + relOffset),
          effectiveSize
        );

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
      fileBlocks.Clear();
      allLoaderHeaders.Clear();
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

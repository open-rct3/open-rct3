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
  public override string ToString() => $"{Name}.{Type}";
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
public class Ovl : IDictionary<OvlFile, OvlEntry>, IDisposable {
  public const string UnnamedOvl = "Untitled OVL";

  private readonly Dictionary<OvlFile, OvlEntry> entries = [];
  private readonly List<FileTypeBlock[]> allFileTypeBlocks = [];
  private readonly List<LoaderHeader[]> allLoaderHeaders = [];
  private readonly List<uint> allVersions = [];
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
    var ovl = new Ovl();
    ovl.IngestArchive(ovlPath);
    return ovl;
  }

  /// <summary>Find a resource by name.</summary>
  public OvlFile? Find(string? name) => entries.Keys.FirstOrDefault(key =>
    key.Type == FileType.Texture &&
    (name == null || key.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
  );

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

  private void IngestArchive(string ovlPath) {
    var basePath = Path.GetDirectoryName(ovlPath) ?? "";
    var fileName = Path.GetFileNameWithoutExtension(ovlPath).Split('.')[0];

    var commonPath = Path.Combine(basePath, $"{fileName}.common.ovl");
    if (File.Exists(commonPath)) ProcessFile(commonPath);

    var uniquePath = Path.Combine(basePath, $"{fileName}.unique.ovl");
    if (File.Exists(uniquePath)) ProcessFile(uniquePath);

    ExtractResources();
  }

  private void ProcessFile(string filePath) {
    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.UTF8, false);

    var magic = reader.ReadUInt32();
    Debug.Assert(magic == 0x4b524746, "Invalid OVL magic");
    var reserved = reader.ReadUInt32();
    var version = reader.ReadUInt32();
    var headerRefs = reader.ReadUInt32();

    Debug.WriteLine($"[OVL] Loading {Path.GetFileName(filePath)} (v{version})");

    var subVersionFlag = 0u;
    uint referenceCount = 0;

    if (version == 5) referenceCount = ReadV5References(reader, out subVersionFlag);
    else if (version == 4) referenceCount = reader.ReadUInt32();
    else referenceCount = headerRefs;

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
        if (version == 5) ReadV5SymbolCounts(reader, loaderHeaders);
      }
    }
    allLoaderHeaders.Add([.. loaderHeaders]);
    allVersions.Add(version);

    var blocks = ReadFileTypeBlocks(filePath, reader, version, subVersionFlag);
    allFileTypeBlocks.Add(blocks);

    ReadPostBlockUnknowns(reader, version);
    ReadBlockData(reader, blocks, version);
    SkipRelocations(reader);

    if (version >= 4 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
      if (version == 4 || (subVersionFlag & 1) != 0) reader.ReadBytes(4);
    }
  }

  private static uint ReadV5References(BinaryReader reader, out uint subVersionFlag) {
    subVersionFlag = reader.ReadUInt32();
    if (subVersionFlag != 0) {
      if (reader.BaseStream.Position + 12 <= reader.BaseStream.Length) {
        reader.ReadBytes(12);
        while (reader.BaseStream.Position < reader.BaseStream.Length && reader.ReadByte() != 0) { }
        while (reader.BaseStream.Position < reader.BaseStream.Length && reader.BaseStream.Position % 4 != 0)
          reader.ReadByte();
      }
    }
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
    string filePath, BinaryReader reader, uint version, uint subVersionFlag
  ) {
    var blocks = new FileTypeBlock[9];
    for (var i = 0; i < blocks.Length; i++) {
      if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) {
        blocks[i] = new FileTypeBlock();
        continue;
      }
      blocks[i] = new FileTypeBlock { Count = reader.ReadUInt32() };
      if (version > 1 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
        reader.ReadUInt32();
        if (version == 5 && (subVersionFlag & 1) != 0 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
          blocks[i].UnknownV5Extra = reader.ReadUInt32();
      }

      blocks[i].Blocks = [.. Enumerable.Range(0, Convert.ToInt32(blocks[i].Count))
        .Select(_ => new FileBlock() { Path = filePath})];

      // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
      if (version > 1) foreach (var block in blocks[i].Blocks) {
        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) break;
        block.Size = reader.ReadUInt32();
        blocks[i].Size += block.Size;
      }

      if (blocks[i].Size > 0)
        Debug.WriteLine($"[OVL] Type {i} count {blocks[i].Count} totalSize {blocks[i].Size}");
    }
    return blocks;
  }

  private static void ReadPostBlockUnknowns(BinaryReader reader, uint version) {
    if (version == 4 && reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
      reader.ReadBytes(8);
    }
    else if (version >= 5 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
      var bytesCount = reader.ReadUInt32();
      if (bytesCount > 0 && bytesCount <= reader.BaseStream.Length - reader.BaseStream.Position)
        reader.ReadBytes(Convert.ToInt32(bytesCount));

      if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
        var longCount = reader.ReadUInt32();
        if (Convert.ToInt64(longCount * 4) <= reader.BaseStream.Length - reader.BaseStream.Position)
          reader.ReadBytes(Convert.ToInt32(longCount * 4));
      }
    }
  }

  private void ReadBlockData(BinaryReader reader, FileTypeBlock[] blocks, uint version) {
    for (var i = 0; i < blocks.Length; i++) {
      foreach (var block in blocks[i].Blocks) {
        if (version == 1 && block.Size == 0) {
          if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return;
          block.Size = reader.ReadUInt32();
        }

        block.RelativeOffset = relocationOffset;
        block.TypeIndex = i;
        relocationOffset += block.Size;
        if (block.Size > 0 && reader.BaseStream.Position + block.Size <= reader.BaseStream.Length) {
          block.Offset = Convert.ToUInt64(reader.BaseStream.Position);
          block.Data = reader.ReadBytes(Convert.ToInt32(block.Size));
          Debug.WriteLine($"[OVL] Seek past block {i} size {block.Size} at relOffset 0x{block.RelativeOffset:X}");
        }
      }
    }
  }

  private static void SkipRelocations(BinaryReader reader) {
    if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) return;
    var relCount = reader.ReadUInt32();
    var bytesToSkip = Convert.ToInt64(relCount) * 4;
    if (bytesToSkip <= reader.BaseStream.Length - reader.BaseStream.Position)
      reader.BaseStream.Seek(bytesToSkip, SeekOrigin.Current);
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
      var version = fileIndex < allVersions.Count ? allVersions[fileIndex] : 0u;
      var symbolSize = version == 1 ? 12 : 16;
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
          entries[new OvlFile(name, fileType, resolvedBlock.Path)] = new OvlEntry(
            Convert.ToUInt32(resolvedBlock.Offset + relOffset),
            effectiveSize
          );
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

  protected virtual void Dispose(bool disposing) {
    if (disposed) return;

    // Empty large fields
    if (disposing) {
      entries.Clear();
      allFileTypeBlocks.Clear();
      allLoaderHeaders.Clear();
    }

    disposed = true;
  }

  public void Dispose() {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}

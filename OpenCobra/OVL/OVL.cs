// OVL.cs
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System.Diagnostics;
using System.Text;
using OpenCobra.OVL.Files;

namespace OpenCobra.OVL;

/// <summary>OVL archive entry (resource) identifier.</summary>
public record OvlFile(string Name, FileType Type) {
  public override string ToString() => $"{Name}.{Type}";
  public override int GetHashCode() => HashCode.Combine(Name, Type);
}

/// <summary>Location and size of a resource within the OVL archive.</summary>
public record OvlEntry(long Offset, uint Size);

internal record LoaderHeader(string Loader, string Name, uint Type, string Tag, uint SymbolCount);

internal class FileBlock {
  public uint Size;
  public uint RelOffset;
  public ulong FileOffset;
  public byte[]? Data;
}

internal class FileTypeBlock {
  public uint Count;
  public uint Size;
  public uint RelOffset;
  public uint UnknownV5_Extra;
  public List<FileBlock> Blocks = [];
}

/// <summary>Represents an OVL archive, providing methods to load and extract resource entries.</summary>
public class Ovl {
  private readonly Dictionary<OvlFile, OvlEntry> entries = [];
  private readonly List<FileTypeBlock[]> allFileTypeBlocks = [];
  private readonly List<LoaderHeader> allLoaderHeaders = [];

  /// <summary>Load an OVL archive and extract all resource entries.</summary>
  public static Dictionary<OvlFile, OvlEntry> Load(string ovlPath) {
    var ovl = new Ovl();
    ovl.IngestArchive(ovlPath);
    return ovl.entries;
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
    using var stream = new FileStream(filePath, FileMode.Open);
    using var reader = new BinaryReader(stream, Encoding.UTF8, false);

    var magic = reader.ReadUInt32();
    Debug.Assert(magic == 0x4b524746, "Invalid OVL magic");
    var reserved = reader.ReadUInt32();
    var version = reader.ReadUInt32();
    var headerRefs = reader.ReadUInt32();

    Console.WriteLine($"[OVL] Loading {Path.GetFileName(filePath)} (v{version})");

    var subVersionFlag = 0u;
    uint referenceCount = 0;

    if (version == 5) referenceCount = ReadV5References(reader, out subVersionFlag);
    else if (version == 4) referenceCount = reader.ReadUInt32();
    else referenceCount = headerRefs;

    Console.WriteLine($"[OVL] subVersionFlag: {subVersionFlag}, referenceCount: {referenceCount}");

    for (var i = 0; i < referenceCount && reader.BaseStream.Position < reader.BaseStream.Length; i++) {
      var len = reader.ReadUInt16();
      if (len > 0 && reader.BaseStream.Position + len <= reader.BaseStream.Length)
        reader.ReadBytes(len);
    }

    var loaderHeaders = new List<LoaderHeader>();
    if (version >= 4 && reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
      reader.ReadUInt32(); // Unk
      var fileTypeCount = reader.ReadUInt32();
      if (fileTypeCount > 0 && fileTypeCount < 1024) {
        loaderHeaders = ReadLoaderHeaders(reader, (int)fileTypeCount);
        if (version == 5 && fileTypeCount > 0) ReadV5SymbolCounts(reader, loaderHeaders);
      }
    }
    allLoaderHeaders.AddRange(loaderHeaders);

    var blocks = ReadFileTypeBlocks(reader, version, subVersionFlag);
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

  private static FileTypeBlock[] ReadFileTypeBlocks(BinaryReader reader, uint version, uint subVersionFlag) {
    var blocks = new FileTypeBlock[10];
    for (var i = 0; i < 10; i++) {
      if (reader.BaseStream.Position + 4 > reader.BaseStream.Length) {
        blocks[i] = new FileTypeBlock();
        continue;
      }
      blocks[i] = new FileTypeBlock { Count = reader.ReadUInt32() };
      if (version > 1 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length) {
        blocks[i].RelOffset = reader.ReadUInt32();
        if (version == 5 && (subVersionFlag & 1) != 0 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
          blocks[i].UnknownV5_Extra = reader.ReadUInt32();
      }
      blocks[i].Blocks = [];
      for (var m = 0; m < blocks[i].Count && reader.BaseStream.Position + 4 <= reader.BaseStream.Length; m++) {
        var size = reader.ReadUInt32();
        blocks[i].Size += size;
        blocks[i].Blocks.Add(new FileBlock { Size = size });
      }
      if (blocks[i].Size > 0) Console.WriteLine($"[OVL] Type {i} count {blocks[i].Count} baseRelOffset 0x{blocks[i].RelOffset:X} totalSize {blocks[i].Size}");
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

  private static void ReadBlockData(BinaryReader reader, FileTypeBlock[] blocks, uint version) {
    var cumulativeOffset = 0u;
    for (var i = 0; i < 10; i++) {
      var currentRelOffset = (blocks[i].RelOffset > 0x10) ? blocks[i].RelOffset : cumulativeOffset;
      foreach (var block in blocks[i].Blocks) {
        block.RelOffset = currentRelOffset;
        currentRelOffset += block.Size;
        cumulativeOffset += block.Size;
        if (block.Size > 0 && reader.BaseStream.Position + block.Size <= reader.BaseStream.Length) {
          block.FileOffset = Convert.ToUInt64(reader.BaseStream.Position);
          block.Data = reader.ReadBytes(Convert.ToInt32(block.Size));
          Console.WriteLine($"[OVL] Read block {i} size {block.Size} at relOffset 0x{block.RelOffset:X}");
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
    var allBlocks = allFileTypeBlocks.SelectMany(ftb => ftb.SelectMany(b => b.Blocks)).ToList();

    foreach (var b in allBlocks.Where(sb => sb.Data != null)) {
      var symbolSize = 24;
      var blockOffset = 0;
      if (b.Data!.Length >= 24 && b.Data.Length % 24 == 0) blockOffset = 0;
      else if (b.Data.Length >= 24 + 4 && (b.Data.Length - 4) % 24 == 0) blockOffset = 4;
      else if (b.Data!.Length >= 12 && b.Data.Length % 12 == 0) symbolSize = 12;
      else continue;

      var count = (b.Data.Length - blockOffset) / symbolSize;
      for (var i = 0; i < count; i++) {
        var symOffset = blockOffset + i * symbolSize;
        var namePtr = BitConverter.ToUInt32(b.Data, symOffset);
        var name = ReadString(allBlocks, namePtr);
        if (string.IsNullOrEmpty(name)) continue;

        uint dataPtr, size = 0, loaderIdx = 0;
        if (symbolSize == 24) {
          loaderIdx = BitConverter.ToUInt32(b.Data, symOffset + 4);
          dataPtr = BitConverter.ToUInt32(b.Data, symOffset + 8);
          size = BitConverter.ToUInt32(b.Data, symOffset + 20);
        }
        else {
          dataPtr = BitConverter.ToUInt32(b.Data, symOffset + 8);
        }

        FileType fileType = FileType.Unknown;
        if (allLoaderHeaders.Count > 0 && loaderIdx < (uint)allLoaderHeaders.Count) {
          // Use the tag from loader headers if available
          fileType = allLoaderHeaders[(int)loaderIdx].Tag.ToFileType();
        }
        if (fileType == FileType.Unknown && name.Contains(':')) {
          // If not found, try to infer from name (e.g., "Texture:MyTexture.dds")
          fileType = name.Split(':')[0].ToFileType();
        }

        // Default entry creation
        foreach (var fb in allBlocks) {
          if (dataPtr >= fb.RelOffset && dataPtr < fb.RelOffset + fb.Size) {
            var relOffset = dataPtr - fb.RelOffset;
            // Ensure that the size does not exceed the remaining space in the block.
            // If the provided size is 0 or larger than the remaining space, use the remaining space.
            var effectiveSize = size == 0
                ? fb.Size - relOffset
                : Math.Min(size, fb.Size - relOffset);
            entries[new OvlFile(name, fileType)] = new OvlEntry(Convert.ToInt64(fb.FileOffset + relOffset), effectiveSize);
            break;
          }
        }
      }
    }
  }

  private static string ReadString(List<FileBlock> blocks, uint ptr) {
    if (ptr == 0) return "";
    foreach (var fb in blocks) {
      if (fb.Data == null) continue;
      if (ptr >= fb.RelOffset && ptr < fb.RelOffset + fb.Size) {
        var offset = (int)(ptr - fb.RelOffset);
        var end = Array.IndexOf(fb.Data, (byte)0, offset);
        if (end < 0) end = fb.Data.Length;
        return Encoding.ASCII.GetString(fb.Data, offset, end - offset);
      }
    }
    return "";
  }
}

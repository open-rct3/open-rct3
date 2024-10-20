// OVL
//
// Authors:
//  - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.Diagnostics;
using System.Numerics;
using System.IO;
using System.Text;
using System;
using System.Runtime.InteropServices;
using System.Drawing;

namespace OVL;

// See https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types
// See https://stackoverflow.com/a/4159471/1363247

// FIXME: Make sure OVLs use 32-bit uints
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct OvlHeader {
  public uint magic;
  public uint reserved;
  public uint version;
  public uint references;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct OvlFilesHeader {
  public uint unk;
  public uint fileTypeCount;
}

public record struct File {
  public ulong size;
  public ulong offset;
  public ulong relativeOffset;
  /// This is `unsigned char*` in Importer
  public byte[]? data;
  public uint unk;
}

public record struct Symbol {
  public string symbol;
  public ulong[] data;
  public uint isPointer;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SymbolHashed {
  public Symbol symbol;
  public uint checksum;
}

public record struct Loader {
  public uint loaderType;
  public ulong[] data;
  public uint hasExtraData;
  public Symbol? sym;
  public uint symbolsToResolve;
}

public record struct SymbolRef {
  public ulong? reference;
  public string symbol;
  public Loader? loader;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SymbolRefHashed {
  public SymbolRef reference;
  public uint checksum;
}

public enum OvlType {
  Common,
  Unique
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextString {
  public string name;
  public string value;
}

/// Used in Type 0 Files
public record struct Resource {
  public uint length;
  public ulong[] data;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3D {
  public float x, y, z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex {
  public Vector3D position;
  public Vector3D normal;
  public uint color;
  public float tu, tv;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector4 {
  public byte x, y, z, w;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexWeighted {
  public Vector3D position;
  public Vector3D normal;
  public Vector4 bone;
  public Vector4 boneWeight;
  public uint color;
  public float tu, tv;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FaceVertex {
  public int x, y, z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FaceTexCoord {
  public int x, y, z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Face {
  public FaceVertex vertex;
  public FaceTexCoord uvs;
  public int smoothing;
  public int materialId;
  public int ab;
  public int bc;
  public int ca;
}

public struct Mesh {
  public string name;
  public string textureName;
  public string textureStyle;
  public Vector3D boudingBox1;
  public Vector3D boudingBox2;
  // FIXME: public MeshData? mesh;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Color {
  public byte blue, green, red, alpha;
}

public record struct FlexiTextureData {
  public uint scale;
  public uint width, height;
  /// Combinable recolorability flags.
  public Recolorable recolorable;
  public byte[] palette;
  public byte[] texture;
  public byte[] alpha;
}

public record struct FlexiTextureInfo {
  public uint scale;
  public uint width;
  public uint height;
  /// Animation Speed, approx. frames per second.
  public uint fps;
  public Recolorable recolorable;
  public uint offsetCount;
  public ulong? offset1;
  public uint nextCount;
  public FlexiTextureData? next;
}

public record struct FlexiTexture {
  public string textureName;
  public byte[] data;
  public byte[] alphaChannel;
  public FlexiTextureData flexi;
  public FlexiTextureInfo flexiInfo;
  public Color colors;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EffectPoint {
  public string name;
  public Matrix4x4 transform;
}

public class Ovl {
  public readonly OvlType type;
  private Stream file;
  private long fileSize;
  private BinaryReader reader;
  private File[] files = new File[9];
  private List<string> references = new();

  // QUESTION: Is char FileName[MAX_PATH]; in importer?
  public readonly string name;
  // Added to store the unique ID.
  public EffectPoint[] effectPoints = Array.Empty<EffectPoint>();
  public Mesh[] meshes = Array.Empty<Mesh>();
  public FlexiTextureInfo[] flexiTextureItems = Array.Empty<FlexiTextureInfo>();

  public Ovl(Stream stream, string? fileName = null) {
    file = stream;
    name = fileName ?? "OVL";
    type = Path.GetFileName(fileName)?.ToLower().EndsWith(".common.ovl") ?? true ? OvlType.Common : OvlType.Unique;
    reader = new BinaryReader(file, Encoding.ASCII, false);
    fileSize = fileName != null ? file.Length : 0;
  }

  public static Ovl Open(string filePath) {
    var invalidOvlError = $"File is not an OVL archive: {filePath}";

    var file = System.IO.File.Open(filePath, FileMode.Open);
    Debug.Assert(file.Length >= Marshal.SizeOf<OvlHeader>(), invalidOvlError);
    return Read(file, filePath);
  }

  public static Ovl Read(Stream stream, string filePath = "Unnamed OVL") {
    var invalidOvlError = $"File is not an OVL archive: {filePath}";
    Debug.Assert(filePath == "Unnamed OVL" || new FileInfo(filePath).Exists || true);
    var ovl = new Ovl(stream, filePath);

    var maybeHeader = stream.ReadStruct<OvlHeader>();
    Debug.Assert(maybeHeader.HasValue);
    var header = maybeHeader.Value;
    // Ensure archive's magic string matches "FGRK"
    // QUESTION: How to properly handle endianness?
    // 0x4647524b
    // 0x4b524746
    // FGDK: Frontier Graphics Development Kit (?)
    // FGRK: Frontier Graphics Resource Kit (?)
    // "FGRK"c.representation
    Debug.Assert(header.magic == 0x4b524746, invalidOvlError);

    // Read reference count
    if (header.version == (uint) Version.one)
      ovl.references.EnsureCapacity((int) header.references);
    // ReSharper disable once MergeIntoLogicalPattern
    else if (header.version != 4 || header.version != 5)
      throw new Exception($"Unknown OVL version: {header.version}");
    else if (header.version == 5) {
      // Skip unknowns
      var subversionFlag = ovl.reader.ReadInt32();
      if (subversionFlag > 0) {
        ovl.file.Seek(12, SeekOrigin.Current);
        char c;
        var padding = 0;
        do {
          c = ovl.reader.ReadChar();
          padding += 1;
          if (padding == 4) padding = 0;
        } while (c != 0);
      }
    }

    if (header.version != (uint) Version.one)
      ovl.references.EnsureCapacity(ovl.reader.ReadInt32());

    // Read references
    ovl.references = ovl.references.Select(reference => ovl.ReadString()).ToList();

    // Read file index header
    var filesHeader = stream.ReadStruct<OvlFilesHeader>();
    Debug.Assert(filesHeader.HasValue, "Could not read file index!");
    Debug.WriteLine(filesHeader);

    // Read file loader headers
    Debug.Assert(ovl.file.Position < ovl.fileSize, "Expected file loaders!");
    var loaders = new LoaderHeader[filesHeader.Value.fileTypeCount];
    for (var i = 0; i < loaders.Length; i += 1)  {
      loaders[i] = new LoaderHeader() {
        loader = ovl.ReadString(),
        name = ovl.ReadString(),
        type = ovl.reader.ReadInt32(),
        tag = ovl.ReadString()
      };
    }

    // V5 Loader Header stuff, number of symbols for each file type / loader header by index
    // This applies to the current common/unique file
    // The order the loaders appear here is important for the symbol order, they are primarily
    // sorted by the file type in this order, secondarily they are sorted by hash
    if (header.version == 5) for (var i = 0; i < loaders.Length; i += 1)
      loaders[ovl.reader.ReadInt64()].symbolCount = ovl.reader.ReadInt64();

    // Read file index, i.e. nine file blocks common to all OVL archives
    var fileBlocks = new FileBlock[9];
    for (var i = 0; i < fileBlocks.Length; i += 1) {
      var block = fileBlocks[i] = new FileBlock() {
        fileSizes = new long[ovl.reader.ReadUInt32()]
      };

      if (header.version == 1) continue;

      // Skip unknowns
      ovl.file.Seek(4 + (header.version == 5 ? 8 : 4), SeekOrigin.Current);

      // Read the size of each file in this block
      for (var v = 0; v < block.fileSizes.Length; v++)
        block.fileSizes[v] = ovl.reader.ReadUInt32();

      // Sum the total size of the files in this block
      block.size = (long) Math.Round(block.fileSizes.Sum(size => (decimal) size));
    }

    // Skip unknowns
    if (header.version == 4) ovl.file.Seek(8, SeekOrigin.Current);
    if (header.version == 5) {
      var unkBytesCount = ovl.reader.ReadUInt32();
      ovl.file.Seek(4 + unkBytesCount, SeekOrigin.Current);
      for (ulong x = 0; x < ovl.reader.ReadUInt32(); x += 1) ovl.file.Seek(4, SeekOrigin.Current);
    }

    // Read file table
    var offset = ovl.file.Position;
    foreach (var file in ovl.files) {
      var i = Array.IndexOf(ovl.files, file);
      Debug.Assert(i < ovl.files.Length, "File index is out-of-range!");
      fileBlocks[i] = fileBlocks[i] with { relativeOffset = ovl.file.Position - offset };
      for (var fileSizeIndex = 0; fileSizeIndex < fileBlocks[i].fileSizes.Length; fileSizeIndex += 1) {
        if (ovl.file.Position == ovl.fileSize) throw new EndOfStreamException($"File overflow at ({i}, {fileSizeIndex})");

        // Read size
        if (header.version == 1) {
          fileBlocks[i].fileSizes[fileSizeIndex] = ovl.reader.ReadInt32();
          fileBlocks[i] = fileBlocks[i] with {
            size = fileBlocks[i].size + fileBlocks[i].fileSizes[fileSizeIndex]
          };
        }

        // Read the data
        var size = fileBlocks[i].fileSizes[fileSizeIndex];
        if (ovl.file.Position == ovl.fileSize) continue;
        var filePosition = (ulong) ovl.file.Position;
        ovl.files[i] = file with {
          offset = filePosition,
          relativeOffset = filePosition - (ulong) offset,
          data = size > 0 ? ovl.reader.ReadBytes((int) size) : null
        };
        offset += size;
      }
    }

    // Read relocations
    var relocations = new ulong[ovl.reader.ReadUInt32()];
    for (var i = 0; i < relocations.Length; i++)
      relocations[i] = ovl.reader.ReadUInt32();
    // Skip relocation unknowns (this may be a false assumption)
    if (header.version > 1) ovl.file.Seek(4, SeekOrigin.Current);

    // Read checksum
    var checksum = ovl.reader.ReadChars(2);
    // TODO: Assert the checksum matches internal state?

    // TODO: Read the rest of the data for unique OVLs…
    if (ovl.type == OvlType.Common) Debug.Assert(
      ovl.file.Position == ovl.fileSize,
      "Archive was not ingested in its entirety!");

    ovl.file.Close();
    return ovl;
  }

  private string ReadString() {
    Debug.Assert(file.Position < fileSize, "Unexpected EOF!");
    return new string(reader.ReadChars(reader.ReadUInt16()));
  }
}

internal record struct LoaderHeader {
  public string loader;
  public string name;
  public long type;
  public string tag;
  public long symbolCount;
  public int symbolCountOrder;
}

internal record struct FileBlock {
  public long[] fileSizes;
  public long relativeOffset;
  public long size;
}

public static class StreamExtensions {
  public static T? ReadStruct<T>(this Stream stream) where T : struct {
    var size = Marshal.SizeOf(typeof(T));
    var buffer = new byte[size];

    // Returns null if the structure cannot be read
    if (stream.Read(buffer, 0, size) != size) return null;

    // Map the byte data to the generic structure
    var pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    var structure = Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(), typeof(T));
    pinnedBuffer.Free();
    return (T?) structure;
  }
}

// OVL
//
// Authors:
//  - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Drawing;

namespace OVL;

/// <seealso href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types"/>
/// <seealso href="https://stackoverflow.com/a/4159471/1363247"/>

#region On-disk header structs

/// <summary>OVL archive file header.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct OvlHeader {
  public uint magic;
  public uint reserved;
  public uint version;
  public uint references;
}

/// <summary>Header preceding the file type / loader index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct OvlFilesHeader {
  public uint unk;
  public uint fileTypeCount;
}

#endregion

#region On-disk block 2 structs (32-bit virtual addresses as uint)

/// <summary>Symbol table entry for v1 archives (block 2, sub-block 0). 12 bytes.</summary>
/// <remarks>See <c>SymbolStruct</c> in rct3importer <c>ovlstructs.h</c>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct SymbolStruct {
  /// <summary>Virtual address into block 0 (string table) for the symbol name.</summary>
  public uint Symbol;
  /// <summary>Virtual address to the symbol data.</summary>
  public uint Data;
  public uint IsPointer;
}

/// <summary>Symbol table entry for v4/v5 archives (block 2, sub-block 0). 16 bytes.</summary>
/// <remarks>See <c>SymbolStruct2</c> in rct3importer <c>ovlstructs.h</c>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct SymbolStruct2 {
  public uint Symbol;
  public uint Data;
  public ushort IsPointer;
  /// <summary>0xFFFF for v5, 0x0000 for v4.</summary>
  public ushort Unknown;
  /// <summary>djb2 hash of the lowercased symbol name.</summary>
  public uint Hash;
}

/// <summary>Loader table entry (block 2, sub-block 1). 20 bytes.</summary>
/// <remarks>See <c>LoaderStruct</c> in rct3importer <c>ovlstructs.h</c>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LoaderStruct {
  /// <summary>Index into the <see cref="Ovl.LoaderHeaders"/> array.</summary>
  public uint LoaderType;
  /// <summary>Virtual address to the loader data.</summary>
  public uint Data;
  /// <summary>v5: low word is extra data count, high word is unknown.</summary>
  public uint HasExtraData;
  /// <summary>Virtual address to a <see cref="SymbolStruct"/>.</summary>
  public uint Sym;
  public uint SymbolsToResolve;
}

/// <summary>Symbol reference entry for v1 archives (block 2, sub-block 2). 12 bytes.</summary>
/// <remarks>See <c>SymbolRefStruct</c> in rct3importer <c>ovlstructs.h</c>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct SymbolRefStruct {
  /// <summary>Virtual address to the reference site.</summary>
  public uint Reference;
  /// <summary>Virtual address to the symbol name string.</summary>
  public uint Symbol;
  /// <summary>Virtual address to a <see cref="LoaderStruct"/>.</summary>
  public uint Ldr;
}

/// <summary>Symbol reference entry for v4/v5 archives (block 2, sub-block 2). 16 bytes.</summary>
/// <remarks>See <c>SymbolRefStruct2</c> in rct3importer <c>ovlstructs.h</c>.</remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct SymbolRefStruct2 {
  public uint Reference;
  public uint Symbol;
  public uint Ldr;
  public uint Hash;
}

#endregion

#region Public data types

/// <summary>A single file block within an OVL archive.</summary>
public record struct File {
  public ulong size;
  public ulong offset;
  public ulong relativeOffset;
  /// <summary>Raw block data. Equivalent to <c>unsigned char*</c> in the Importer.</summary>
  public byte[]? data;
  public uint unk;
}

/// <summary>A named symbol in the archive.</summary>
public record struct Symbol {
  public string symbol;
  public ulong[] data;
  public uint isPointer;
}

/// <summary>A symbol with its CRC-32 checksum.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SymbolHashed {
  public Symbol symbol;
  public uint checksum;
}

/// <summary>A resource loader descriptor.</summary>
public record struct Loader {
  public uint loaderType;
  public ulong[] data;
  public uint hasExtraData;
  public Symbol? sym;
  public uint symbolsToResolve;
}

/// <summary>A cross-reference to a named symbol.</summary>
public record struct SymbolRef {
  public ulong? reference;
  public string symbol;
  public Loader? loader;
}

/// <summary>A symbol reference with its CRC-32 checksum.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SymbolRefHashed {
  public SymbolRef reference;
  public uint checksum;
}

/// <summary>OVL archive type — common (shared resources) or unique (archive-specific resources).</summary>
public enum OvlType {
  Common,
  Unique
}

/// <summary>A named text string value.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextString {
  public string name;
  public string value;
}

/// <summary>A resource entry used in type-0 file blocks.</summary>
public record struct Resource {
  public uint length;
  public ulong[] data;
}

/// <summary>Three-component floating-point vector.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector3D {
  public float x, y, z;
}

/// <summary>Vertex with position, normal, color, and texture coordinates.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vertex {
  public Vector3D position;
  public Vector3D normal;
  public uint color;
  public float tu, tv;
}

/// <summary>Four-component byte vector.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Vector4 {
  public byte x, y, z, w;
}

/// <summary>Weighted vertex with bone assignment for skeletal animation.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexWeighted {
  public Vector3D position;
  public Vector3D normal;
  public Vector4 bone;
  public Vector4 boneWeight;
  public uint color;
  public float tu, tv;
}

/// <summary>Triangle face vertex index triplet.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FaceVertex {
  public int x, y, z;
}

/// <summary>Texture coordinate index triplet for a face.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FaceTexCoord {
  public int x, y, z;
}

/// <summary>A triangle face with vertex, texture, smoothing, and material data.</summary>
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

/// <summary>Mesh geometry metadata.</summary>
public struct Mesh {
  public string name;
  public string textureName;
  public string textureStyle;
  public Vector3D boudingBox1;
  public Vector3D boudingBox2;
  // QUESTION: Add `MeshData? mesh` field?
}

/// <summary>32-bit RGBA color (BGRA byte order).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Color {
  public byte blue, green, red, alpha;
}

/// <summary>Flexi-texture frame data.</summary>
public record struct FlexiTextureData {
  public uint scale;
  public uint width, height;
  /// <summary>Combinable recolorability flags.</summary>
  public Recolorable recolorable;
  public byte[] palette;
  public byte[] texture;
  public byte[] alpha;
}

/// <summary>Flexi-texture animation metadata.</summary>
public record struct FlexiTextureInfo {
  public uint scale;
  public uint width;
  public uint height;
  /// <summary>Animation speed, approx. frames per second.</summary>
  public uint fps;
  public Recolorable recolorable;
  public uint offsetCount;
  public ulong? offset1;
  public uint nextCount;
  public FlexiTextureData? next;
}

/// <summary>Flexi-texture resource with palette, data, and animation info.</summary>
public record struct FlexiTexture {
  public string textureName;
  public byte[] data;
  public byte[] alphaChannel;
  public FlexiTextureData flexi;
  public FlexiTextureInfo flexiInfo;
  public Color colors;
}

/// <summary>An effect point with a world-space transform.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EffectPoint {
  public string name;
  public Matrix4x4 transform;
}

/// <summary>A resolved relocation entry mapping a source virtual address to a target.</summary>
public record struct OvlRelocation {
  public OvlType SourceFileType;
  public uint Address;
  public int SourceFile;
  public uint SourceBlock;
  public uint TargetAddress;
  public OvlType TargetFileType;
  public int TargetFile;
  public uint TargetBlock;
  public bool IsSymbolRef;
}

/// <summary>A parsed symbol from the symbol table (block 2, sub-block 0).</summary>
public record struct OvlSymbol {
  public string Name;
  public uint DataAddress;
  public bool IsPointer;
  public uint Hash;
}

/// <summary>A parsed loader entry from the loader table (block 2, sub-block 1).</summary>
public record struct OvlLoaderEntry {
  public uint LoaderType;
  public string Name;
  public string Tag;
  public uint DataAddress;
  public string SymbolName;
  public uint SymbolsToResolve;
  public bool HasExtraData;
  /// <summary>Source file name (e.g. "Water.common.ovl").</summary>
  public string SourceFile;
}

#endregion

/// <summary>Parsed state for a single OVL file (common or unique).</summary>
/// <remarks>Mirrors the per-type state in <c>cOVLDump</c> (<c>m_header</c>, <c>m_fileblocks</c>, <c>m_relocations</c>, etc.).</remarks>
internal class OvlFileData {
  public OvlHeader Header;
  public LoaderHeader[] LoaderHeaders = Array.Empty<LoaderHeader>();
  public FileBlockEntry[] FileBlocks = new FileBlockEntry[9];
  /// <summary>Raw data for each of the 9 blocks' sub-blocks [blockIndex][subBlockIndex].</summary>
  public byte[][][] FileBlockData = new byte[9][][];
  public uint[] Relocations = Array.Empty<uint>();
  public long ReloOffset;
  public string FilePath = string.Empty;
}

/// <summary>Per-file-block metadata enriched with virtual address tracking.</summary>
/// <remarks>Mirrors <c>OvlFileBlock</c> + <c>OvlFileTypeBlock</c> from <c>ovldumperstructs.h</c>.</remarks>
internal record struct FileBlockEntry {
  public uint[] SubBlockSizes;
  /// <summary>Base virtual address for this block's sub-blocks.</summary>
  public uint RelOffset;
  /// <summary>Number of sub-blocks.</summary>
  public uint Count;
  /// <summary>Sum of <see cref="SubBlockSizes"/>.</summary>
  public uint TotalSize;
  /// <summary>Unknown field read after count (v4/v5).</summary>
  public uint UnknownV45;
  /// <summary>Extra unknown for v5 with subversion flag.</summary>
  public uint UnknownV5Extra;
}

public record struct LoaderHeader {
  public string loader;
  public string name;
  public int type;
  public string tag;
  public uint symbolCount;
  public int symbolCountOrder;
}

public class Ovl : IComparable<Ovl>, ICloneable, IDisposable, INotifyPropertyChanging {
  public const string UnnamedOvl = "Unnamed OVL";

  private string description;
  private ObservableCollection<string> references = new();
  private ObservableCollection<EffectPoint> effectPoints = new();
  private ObservableCollection<Mesh> meshes = new();
  private ObservableCollection<FlexiTextureInfo> flexiTextureItems = new();

  /// <summary>The nine file blocks common to all OVL archives.</summary>
  public readonly File[] Files = new File[9];

  public event PropertyChangingEventHandler? PropertyChanging;
  public string FileName { get; }
  public string Description {
    get => description;
    set {
      PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Description)));
      description = value;
    }
  }
  public OvlType Type { get; }

  // --- Paired archive state (populated by Load) ---
  private OvlFileData? commonData;
  private OvlFileData? uniqueData;
  private long uniqueReloBase;

  private readonly List<OvlRelocation> allRelocations = new();
  private readonly List<string> allStrings = new();
  private readonly List<OvlSymbol> allSymbols = new();
  private readonly List<OvlLoaderEntry> allLoaderEntries = new();
  private readonly Dictionary<string, Dictionary<string, uint>> structureMap = new(StringComparer.OrdinalIgnoreCase);

  // --- Public properties ---

  /// <summary>Loader headers from the common file (or the single file for non-paired reads).</summary>
  public LoaderHeader[] LoaderHeaders { get; private set; } = Array.Empty<LoaderHeader>();
  /// <summary>OVL archive dependency references.</summary>
  public IReadOnlyList<string> References => references;
  /// <summary>Resolved relocation entries across both common and unique files.</summary>
  public IReadOnlyList<OvlRelocation> Relocations => allRelocations;
  /// <summary>Parsed string table entries.</summary>
  public IReadOnlyList<string> Strings => allStrings;
  /// <summary>Parsed symbol table entries.</summary>
  public IReadOnlyList<OvlSymbol> Symbols => allSymbols;
  /// <summary>Parsed loader entries.</summary>
  public IReadOnlyList<OvlLoaderEntry> LoaderEntries => allLoaderEntries;
  /// <summary>Structure map: tag → symbol name → data virtual address.</summary>
  public IReadOnlyDictionary<string, Dictionary<string, uint>> StructureMap => structureMap;
  /// <summary>Common file data (only populated for paired archives via Load).</summary>
  internal OvlFileData? CommonData => commonData;
  /// <summary>Unique file data (only populated for paired archives via Load).</summary>
  internal OvlFileData? UniqueData => uniqueData;

  // --- Constructors ---

  public Ovl(string fileName, OvlType type = OvlType.Common) {
    FileName = fileName;
    description = Path.GetFileName(fileName);
    Type = Path.GetFileName(fileName)?.ToLower().EndsWith(".common.ovl") ?? true ? OvlType.Common : OvlType.Unique;
  }

  // --- Static API ---

  /// <summary>Open an OVL archive.</summary>
  /// <remarks>
  /// Automatically loads the paired archive when available. For <c>.unique.ovl</c> files,
  /// loads the paired <c>.common.ovl</c> if available. Single-file archives are read and
  /// post-processed directly.
  /// </remarks>
  public static Ovl Open(string filePath) {
    if (filePath.ToLower().EndsWith(".common.ovl") && System.IO.File.Exists(filePath)) {
      var uniquePath = filePath.Substring(0, filePath.Length - ".common.ovl".Length) + ".unique.ovl";
      if (System.IO.File.Exists(uniquePath))
        return Load(filePath);
    } else if (filePath.ToLower().EndsWith(".unique.ovl") && System.IO.File.Exists(filePath)) {
      var commonPath = filePath.Substring(0, filePath.Length - ".unique.ovl".Length) + ".common.ovl";
      if (System.IO.File.Exists(commonPath))
        return Load(commonPath);
    }

    return Read(System.IO.File.Open(filePath, FileMode.Open), filePath);
  }

  /// <summary>Load a paired common/unique OVL archive with full post-processing.</summary>
  /// <remarks>Mirrors <c>cOVLDump::Load</c> in <c>OVLDump.cpp</c>.</remarks>
  public static Ovl Load(string commonPath) {
    var uniquePath = commonPath.Substring(0, commonPath.Length - ".common.ovl".Length) + ".unique.ovl";
    Debug.Assert(System.IO.File.Exists(commonPath), $"Common OVL not found: {commonPath}");
    Debug.Assert(System.IO.File.Exists(uniquePath), $"Paired unique OVL not found: {uniquePath}");

    var ovl = new Ovl(commonPath);

    // Read both files. Order matters due to the relocation offset.
    // Mirrors ReadFile(OVLT_COMMON) then ReadFile(OVLT_UNIQUE).
    var commonOvl = Read(System.IO.File.Open(commonPath, FileMode.Open, FileAccess.Read), commonPath);
    ovl.commonData = commonOvl.commonData;

    ovl.uniqueReloBase = ovl.commonData!.ReloOffset;
    var uniqueOvl = Read(System.IO.File.Open(uniquePath, FileMode.Open, FileAccess.Read), uniquePath);
    ovl.uniqueData = uniqueOvl.commonData;

    ovl.LoaderHeaders = ovl.commonData.LoaderHeaders;

    // Re-resolve relocations across both files (Read() only resolves intra-file).
    // Mirrors the two ResolveRelocation loops in cOVLDump::Load.
    ovl.allRelocations.Clear();
    ovl.allStrings.Clear();
    ovl.allSymbols.Clear();
    ovl.allLoaderEntries.Clear();
    ovl.structureMap.Clear();
    ovl.ResolveRelocations();

    // Parse string table from block 0 of each file.
    // Mirrors MakeStrings(OVLT_COMMON) then MakeStrings(OVLT_UNIQUE).
    ovl.ParseStrings(ovl.commonData!);
    var commonStringCount = ovl.allStrings.Count;
    ovl.ParseStrings(ovl.uniqueData!);

    // Parse symbol table from block 2, sub-block 0.
    // Mirrors MakeSymbols(OVLT_COMMON) then MakeSymbols(OVLT_UNIQUE).
    ovl.ParseSymbols(ovl.commonData!);
    ovl.ParseSymbols(ovl.uniqueData!);

    // Parse loader table from block 2, sub-block 1.
    // Mirrors MakeLoaders(OVLT_COMMON) then MakeLoaders(OVLT_UNIQUE).
    // Pass string offset so unique entries can resolve names from the merged string table.
    ovl.ParseLoaders(ovl.commonData!, 0);
    ovl.ParseLoaders(ovl.uniqueData!, commonStringCount);

    return ovl;
  }

  /// <summary>Read a single OVL archive from a stream.</summary>
  /// <remarks>Mirrors <c>cOVLDump::ReadFile</c> for the binary parsing portion (no post-processing).</remarks>
  /// <param name="stream">The binary stream to read from. The stream is closed on return.</param>
  /// <param name="filePath">Optional file path for diagnostics.</param>
  /// <returns>A new <see cref="Ovl"/> instance with <see cref="commonData"/> populated.</returns>
  public static Ovl Read(Stream stream, string filePath = UnnamedOvl) {
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
    var fileSize = stream.Length;

    // Local string reader that asserts EOF before each read.
    // Mirrors the invariant in cOVLDump::ReadFile: c_data < m_data[type] + m_size[type].
    string readString() {
      Debug.Assert(stream.Position < fileSize, $"Unexpected EOF while reading string in {filePath}!");
      var len = reader.ReadUInt16();
      return new string(reader.ReadChars(len));
    }

    var ovl = new Ovl(filePath);
    var data = new OvlFileData { FilePath = filePath, ReloOffset = 0 };

    // Header
    // Mirrors: m_header[type] = *reinterpret_cast<OvlHeader*>(c_data);
    Debug.Assert(stream.Position + Marshal.SizeOf<OvlHeader>() <= fileSize,
      $"Stream too small for OVL header: {filePath}");
    var header = stream.ReadStruct<OvlHeader>();
    Debug.Assert(header.HasValue, $"Could not read OVL header: {filePath}");
    data.Header = header!.Value;
    Debug.Assert(data.Header.magic == 0x4b524746, $"File is not an OVL archive: {filePath}");

    // Extra header (version-specific)
    // Mirrors the switch(m_header[type].version) in cOVLDump::ReadFile
    var subversionFlag = 0u;
    if (data.Header.version == 1) {
      // v1: reference count is in the header
    } else if (data.Header.version == 4) {
      // v4: reference count follows header
    } else if (data.Header.version == 5) {
      subversionFlag = reader.ReadUInt32();
      if (subversionFlag > 0) {
        stream.Seek(12, SeekOrigin.Current); // 3 unknown uint32s
        // Read unknown bytes until null, aligned to 4 bytes
        byte c;
        var padding = 0;
        do {
          c = reader.ReadByte();
          padding += 1;
          if (padding == 4) padding = 0;
        } while (c != 0);
        // Skip padding to 4-byte alignment
        for (var j = padding; j < 4; j++) reader.ReadByte();
      }
    } else {
      throw new Exception($"Unknown OVL version: {data.Header.version}");
    }

    // Read references
    // Mirrors: for (int i = 0; i < m_exheader[type].ReferencesE; ++i) { read string; }
    var referenceCount = data.Header.version == 1 ? (int) data.Header.references : reader.ReadInt32();
    Debug.Assert(referenceCount >= 0, $"Negative reference count: {referenceCount}");
    for (var i = 0; i < referenceCount; i++)
      ovl.references.Add(readString());

    // Read file index header (OvlHeader2)
    // Mirrors: m_header2[type] = *reinterpret_cast<OvlHeader2*>(c_data);
    Debug.Assert(stream.Position + Marshal.SizeOf<OvlFilesHeader>() <= fileSize,
      "Unexpected EOF reading file index header!");
    var fileTypeCount = stream.ReadStruct<OvlFilesHeader>()!.Value.fileTypeCount;
    Debug.Assert(stream.Position < fileSize, "Unexpected EOF after file index header!");

    // Read loader headers
    // Mirrors the OvlLoaderHeader loop in cOVLDump::ReadFile
    var loaders = new LoaderHeader[(int) fileTypeCount];
    for (var i = 0; i < loaders.Length; i++) {
      loaders[i] = new LoaderHeader {
        loader = readString(),
        name = readString(),
        type = reader.ReadInt32(),
        tag = readString()
      };
    }
    data.LoaderHeaders = loaders;
    ovl.LoaderHeaders = loaders;

    // V5: symbol counts per loader header by index
    // Mirrors cOVLDump::ReadFile v5 symbol count loop
    // Each entry is two uint32 values: [index, symbolCount].
    // The order the loaders appear here is important for the symbol order.
    if (data.Header.version == 5) {
      for (var i = 0; i < loaders.Length; i++) {
        var index = reader.ReadUInt32();
        Debug.Assert(index < loaders.Length,
          $"Loader header index out of range: {index} (count: {loaders.Length})");
        loaders[index].symbolCount = reader.ReadUInt32();
        loaders[index].symbolCountOrder = i;
      }
    }


    // Read 9 file block headers
    // Mirrors the "Parse 9 blocks for count and unknown stuff" loop
    var fileBlocks = new FileBlockEntry[9];
    for (var i = 0; i < 9; i++) {
      ref var block = ref fileBlocks[i];
      block.Count = reader.ReadUInt32();
      block.SubBlockSizes = block.Count > 0 ? new uint[block.Count] : Array.Empty<uint>();

      if (data.Header.version > 1) {
        block.UnknownV45 = reader.ReadUInt32();
        if (data.Header.version == 5 && subversionFlag > 0)
          block.UnknownV5Extra = reader.ReadUInt32();

        for (var m = 0; m < block.Count; m++) {
          block.SubBlockSizes[m] = reader.ReadUInt32();
          block.TotalSize += block.SubBlockSizes[m];
        }
      }
    }

    // Post-block unknowns
    // Mirrors cOVLDump::ReadFile "Unknown stuff" section
    if (data.Header.version == 4) {
      stream.Seek(8, SeekOrigin.Current); // 2 unknown uint32s
    }
    if (data.Header.version == 5) {
      var unkBytesCount = reader.ReadUInt32();
      stream.Seek(unkBytesCount, SeekOrigin.Current);
      var longsCount = reader.ReadUInt32();
      stream.Seek(longsCount * 4, SeekOrigin.Current);
    }

    // Read block data
    // Mirrors the "'Read' Data" loop in cOVLDump::ReadFile
    Debug.Assert(stream.Position <= fileSize, "Unexpected EOF before block data!");
    var reloOffset = data.ReloOffset;
    for (var i = 0; i < 9; i++) {
      ref var block = ref fileBlocks[i];
      block.RelOffset = (uint) reloOffset;

      var subBlockData = block.Count > 0 ? new byte[block.Count][] : Array.Empty<byte[]>();

      for (var m = 0; m < block.Count; m++) {
        if (stream.Position >= fileSize)
          throw new EndOfStreamException($"File overflow in block {i}, sub-block {m}");

        if (data.Header.version == 1) {
          // v1: sizes are inline in the data section
          block.SubBlockSizes[m] = reader.ReadUInt32();
          block.TotalSize += block.SubBlockSizes[m];
        }

        var size = block.SubBlockSizes[m];
        subBlockData[m] = size > 0 ? reader.ReadBytes((int) size) : Array.Empty<byte>();
        reloOffset += size;
      }

      data.FileBlockData[i] = subBlockData;
    }
    data.ReloOffset = reloOffset;
    data.FileBlocks = fileBlocks;

    // Read relocations
    // Mirrors cOVLDump::ReadFile relocation loop
    var relocCount = reader.ReadUInt32();
    var relocations = new uint[relocCount];
    for (var i = 0; i < relocCount; i++)
      relocations[i] = reader.ReadUInt32();
    data.Relocations = relocations;

    // Read post-relocation unknowns
    // Mirrors cOVLDump::ReadFile "Post Relocation unknowns" section
    if (data.Header.version == 5 && subversionFlag > 0) {
      if (stream.Position < fileSize)
        stream.Seek(4, SeekOrigin.Current); // unknownv45_postrelocationlong
    } else if (data.Header.version == 4) {
      if (stream.Position < fileSize)
        stream.Seek(4, SeekOrigin.Current); // unknownv45_postrelocationlong (always 0)
    }

    ovl.commonData = data;

    // Post-processing: resolve relocations and parse string/symbol/loader tables.
    ovl.ResolveRelocations(data);
    ovl.ParseStrings(data);
    ovl.ParseSymbols(data);
    ovl.ParseLoaders(data, 0);

    return ovl;
  }

  // --- Post-processing (mirrors cOVLDump methods) ---

  /// <summary>Resolve relocations for both common and unique files.</summary>
  /// <remarks>Mirrors the two relocation loops in <c>cOVLDump::Load</c>.</remarks>
  private void ResolveRelocations() {
    if (commonData != null) ResolveRelocations(commonData);
    if (uniqueData != null) ResolveRelocations(uniqueData);
  }

  /// <summary>Resolve relocations for a single file.</summary>
  /// <remarks>Mirrors the per-relocation loop in <c>cOVLDump::Load</c>.</remarks>
  private void ResolveRelocations(OvlFileData data) {
    var fileType = data == commonData ? OvlType.Common : OvlType.Unique;

    foreach (var relocAddr in data.Relocations) {
      var entry = new OvlRelocation {
        SourceFileType = fileType,
        Address = relocAddr
      };

      // Resolve source: determine which file and block this address belongs to
      if (!ResolveAddress(relocAddr, out var srcFileType, out var srcFile, out var srcBlock, out var srcOffset))
        continue;

      entry.SourceFile = srcFile;
      entry.SourceBlock = srcBlock;

      // Read the target address stored at the relocation site
      // Mirrors: itr->targetrelocation = *(itr->relocationsite);
      var srcData = GetBlockData(srcFileType, srcFile, srcBlock);
      if (srcData == null || srcOffset + 4 > srcData.Length)
        continue;
      var targetAddr = BitConverter.ToUInt32(srcData, (int) srcOffset);
      entry.TargetAddress = targetAddr;

      // Resolve target
      if (ResolveAddress(targetAddr, out var tgtFileType, out var tgtFile, out var tgtBlock, out var tgtOffset)) {
        entry.TargetFileType = tgtFileType;
        entry.TargetFile = tgtFile;
        entry.TargetBlock = tgtBlock;
      }

      allRelocations.Add(entry);
    }
  }

  /// <summary>Resolve a virtual address to file type, block index, sub-block index, and byte offset.</summary>
  /// <remarks>Mirrors <c>cOVLDump::ResolveRelocation</c>.</remarks>
  private bool ResolveAddress(uint address, out OvlType fileType, out int file, out uint block, out uint offset) {
    fileType = OvlType.Common;
    file = -1;
    block = 0;
    offset = 0;

    // Determine file type (common or unique)
    if (uniqueData != null && address >= uniqueReloBase)
      fileType = OvlType.Unique;

    var data = fileType == OvlType.Common ? commonData : uniqueData;
    if (data == null) return false;

    // Find which of the 9 blocks contains this address
    // Mirrors: for (int i = 0; i < 9; ++i) { if (relocation >= blocks[i].reloffset) file = i; }
    for (var i = 0; i < 9; i++) {
      if (address >= data.FileBlocks[i].RelOffset)
        file = i;
    }
    if (file < 0) return false;

    // Find which sub-block within this block contains the address
    // Mirrors: for each sub-block, check if address is in [reloffset, reloffset + size)
    var blockData = data.FileBlocks[file];
    var subOffset = (uint) blockData.RelOffset;
    for (uint i = 0; i < blockData.Count; i++) {
      var subSize = blockData.SubBlockSizes[i];
      if (address >= subOffset && address < subOffset + subSize) {
        block = i;
        offset = address - subOffset;
        return true;
      }
      subOffset += subSize;
    }

    return false;
  }

  /// <summary>Get the raw data for a specific block/sub-block.</summary>
  private byte[]? GetBlockData(OvlType fileType, int blockIndex, uint subBlockIndex) {
    var data = fileType == OvlType.Common ? commonData : uniqueData;
    if (data == null) return null;
    if (blockIndex < 0 || blockIndex >= 9) return null;
    var subBlocks = data.FileBlockData[blockIndex];
    if (subBlockIndex >= subBlocks.Length) return null;
    return subBlocks[subBlockIndex];
  }

  /// <summary>Compute a virtual address from a byte offset within a sub-block.</summary>
  /// <remarks>Mirrors the <c>RelocationFromVar</c> macro.</remarks>
  private uint CalcVirtualAddress(OvlFileData data, int blockIndex, uint subBlockIndex, int byteOffset) {
    return data.FileBlocks[blockIndex].RelOffset +
      SumSizes(data.FileBlocks[blockIndex], subBlockIndex) +
      (uint) byteOffset;
  }

  private static uint SumSizes(FileBlockEntry block, uint upToSubBlock) {
    uint sum = 0;
    for (uint i = 0; i < upToSubBlock && i < block.SubBlockSizes.Length; i++)
      sum += block.SubBlockSizes[i];
    return sum;
  }

  /// <summary>Parse the string table from block 0, sub-block 0.</summary>
  /// <remarks>Mirrors <c>cOVLDump::MakeStrings</c>.</remarks>
  private void ParseStrings(OvlFileData data) {
    if (data.FileBlocks[0].Count == 0) return;
    var blockData = data.FileBlockData[0];
    if (blockData.Length == 0 || blockData[0].Length == 0) return;

    var strData = blockData[0];
    var pos = 0;
    while (pos < strData.Length) {
      var end = Array.IndexOf(strData, (byte) 0, pos);
      if (end < 0) break;
      if (end > pos) {
        var str = Encoding.ASCII.GetString(strData, pos, end - pos);
        allStrings.Add(str);
      }
      pos = end + 1;
    }
  }

  /// <summary>Parse the symbol table from block 2, sub-block 0.</summary>
  /// <remarks>Mirrors <c>cOVLDump::MakeSymbols</c>.</remarks>
  private void ParseSymbols(OvlFileData data) {
    if (data.FileBlocks[2].Count == 0) return;
    var blockData = data.FileBlockData[2];
    if (blockData.Length == 0 || blockData[0].Length == 0) return;

    var symData = blockData[0];
    var isV1 = data.Header.version == 1;
    var structSize = isV1 ? 12 : 16;
    var symCount = symData.Length / structSize;

    for (var s = 0; s < symCount; s++) {
      var offset = s * structSize;
      if (offset + structSize > symData.Length) break;

      uint symNameAddr, dataAddr, isPointer, hash;
      if (isV1) {
        var sym = MemoryMarshal.Read<SymbolStruct>(symData.AsSpan(offset));
        symNameAddr = sym.Symbol;
        dataAddr = sym.Data;
        isPointer = sym.IsPointer;
        hash = 0;
      } else {
        var sym2 = MemoryMarshal.Read<SymbolStruct2>(symData.AsSpan(offset));
        symNameAddr = sym2.Symbol;
        dataAddr = sym2.Data;
        isPointer = sym2.IsPointer;
        hash = sym2.Hash;
      }

      // Resolve the symbol name from the string table (block 0, sub-block 0)
      var name = ResolveStringFromAddress(data, symNameAddr) ?? $"symbol_{s}";

      allSymbols.Add(new OvlSymbol {
        Name = name,
        DataAddress = dataAddr,
        IsPointer = isPointer > 0,
        Hash = hash
      });
    }
  }

  /// <summary>Parse the loader table from block 2, sub-block 1.</summary>
  /// <remarks>Mirrors <c>cOVLDump::MakeLoaders</c>.</remarks>
  private void ParseLoaders(OvlFileData data, int stringOffset = 0) {
    if (data.FileBlocks[2].Count <= 1) return;
    var blockData = data.FileBlockData[2];
    if (blockData.Length <= 1 || blockData[1].Length == 0) return;

    var loaderData = blockData[1];
    var structSize = 20;
    var lodCount = loaderData.Length / structSize;
    // Track position for extra data chunks (mirrors m_dataend[type])
    var dataEnd = lodCount * structSize;

    // For v4/v5: build symbol name list from the string table.
    // The string table entries are symbol names in "name:tag" format.
    // For v5, loader headers' symbolCount fields determine per-type counts.
    // For v4, strings are assigned sequentially to loader entries.
    Dictionary<uint, Queue<string>> symbolNamesByType = new();
    if (data.Header.version >= 4) {
      var symNames = new List<string>();
      var block0Data = data.FileBlockData[0];
      if (block0Data.Length > 0 && block0Data[0].Length > 0) {
        var strData = block0Data[0];
        var pos = 0;
        while (pos < strData.Length) {
          var end = Array.IndexOf(strData, (byte) 0, pos);
          if (end < 0) end = strData.Length;
          if (end > pos)
            symNames.Add(System.Text.Encoding.ASCII.GetString(strData, pos, end - pos));
          pos = end + 1;
        }
      } else if (stringOffset > 0) {
        symNames.AddRange(allStrings.Skip(stringOffset));
      }

      if (data.Header.version == 5) {
        // v5: distribute by symbol count order
        var headersByOrder = data.LoaderHeaders
          .Select((h, i) => (header: h, index: (uint) i, order: h.symbolCountOrder))
          .Where(x => x.header.symbolCount > 0)
          .OrderBy(x => x.order)
          .ToList();

        var strIdx = 0;
        foreach (var (header, index, _) in headersByOrder) {
          var queue = new Queue<string>();
          for (uint s = 0; s < header.symbolCount && strIdx < symNames.Count; s++, strIdx++)
            queue.Enqueue(symNames[strIdx]);
          symbolNamesByType[index] = queue;
        }
      } else {
        // v4: strings are ordered by loader type, matching entry order
        // Group entries by type and assign strings sequentially per type
        var entriesByType = new Dictionary<uint, List<int>>();
        for (var l = 0; l < lodCount; l++) {
          var lt = BitConverter.ToUInt32(loaderData, l * structSize);
          if (!entriesByType.ContainsKey(lt)) entriesByType[lt] = new List<int>();
          entriesByType[lt].Add(l);
        }
        // Match string counts to entry counts per type
        var strIdx = 0;
        foreach (var kv in entriesByType.OrderBy(k => k.Key)) {
          var queue = new Queue<string>();
          for (var s = 0; s < kv.Value.Count && strIdx < symNames.Count; s++, strIdx++)
            queue.Enqueue(symNames[strIdx]);
          symbolNamesByType[kv.Key] = queue;
        }
      }
    }

    for (var l = 0; l < lodCount; l++) {
      var offset = l * structSize;
      if (offset + structSize > loaderData.Length) break;

      var loaderType = BitConverter.ToUInt32(loaderData, offset);
      // v5 has an extra 4-byte field at offset 4, shifting Data and Sym by 4 bytes
      var dataOffset = data.Header.version == 5 ? 8 : 4;
      var dataAddr = BitConverter.ToUInt32(loaderData, offset + dataOffset);
      var hasExtraDataRaw = data.Header.version == 5
        ? BitConverter.ToUInt32(loaderData, offset + 12)
        : BitConverter.ToUInt32(loaderData, offset + 8);
      var symbolsToResolve = data.Header.version == 5
        ? 0u
        : BitConverter.ToUInt32(loaderData, offset + 16);

      var loaderTypeName = loaderType < data.LoaderHeaders.Length
        ? data.LoaderHeaders[loaderType].name
        : $"type_{loaderType}";
      var loaderTag = loaderType < data.LoaderHeaders.Length
        ? data.LoaderHeaders[loaderType].tag
        : "???";

      // Resolve symbol name from the string table queue for this loader type
      string symbolName;
      if (symbolNamesByType.TryGetValue(loaderType, out var queue) && queue.Count > 0) {
        symbolName = queue.Dequeue();
      } else {
        symbolName = "No Symbol";
      }

      var extraDataCount = 0;
      if (data.Header.version == 5) {
        extraDataCount = (int) (hasExtraDataRaw & 0xFFFF);
      } else {
        extraDataCount = (int) hasExtraDataRaw;
      }

      var entry = new OvlLoaderEntry {
        LoaderType = loaderType,
        Name = loaderTypeName,
        Tag = loaderTag,
        DataAddress = dataAddr,
        SymbolName = symbolName,
        SymbolsToResolve = symbolsToResolve,
        HasExtraData = extraDataCount > 0,
        SourceFile = Path.GetFileName(data.FilePath)
      };
      allLoaderEntries.Add(entry);

      // Build structure map: tag → symbolName → data address
      // Mirrors: m_structmap[type][tag][symbolname] = lo.data;
      if (!string.IsNullOrEmpty(symbolName) && symbolName != "No Symbol") {
        if (!structureMap.TryGetValue(loaderTag, out var tagMap))
          structureMap[loaderTag] = tagMap = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        tagMap[symbolName] = dataAddr;
      }

      // Skip extra data chunks
      // Mirrors: for (int i = 0; i < lo.hasextradata; ++i) { read size, read data }
      for (var i = 0; i < extraDataCount; i++) {
        if (dataEnd + 4 > loaderData.Length) break;
        var chunkSize = BitConverter.ToUInt32(loaderData, dataEnd);
        dataEnd += 4 + (int) chunkSize;
      }
    }
  }

  /// <summary>Resolve a <see cref="LoaderStruct.Sym"/> pointer to a symbol name string.</summary>
  /// <summary>Resolve a relocated loader <c>Sym</c> pointer to a symbol name string.</summary>
  /// <remarks>
  /// The <c>symAddr</c> is the resolved target address of the <see cref="LoaderStruct.Sym"/> field,
  /// <summary>Resolve a virtual address to a null-terminated string in block 0, sub-block 0.</summary>
  private string? ResolveStringFromAddress(OvlFileData data, uint address) {
    if (address == 0) return null;

    // The address points into block 0, sub-block 0 (string table)
    if (data.FileBlocks[0].Count == 0) return null;
    var strData = data.FileBlockData[0][0];
    if (strData.Length == 0) return null;

    var blockBase = data.FileBlocks[0].RelOffset;
    var offset = (int) (address - blockBase);
    if (offset < 0 || offset >= strData.Length) return null;

    // Find the null terminator
    var end = Array.IndexOf(strData, (byte) 0, offset);
    if (end < 0) end = strData.Length;
    if (end == offset) return null;

    return Encoding.ASCII.GetString(strData, offset, end - offset);
  }

  public void Dispose() {
    // Stream lifecycle is managed by Read() which owns and closes streams.
  }

  public override int GetHashCode() {
    return Files.GetHashCode();
  }

  public int CompareTo(Ovl? other) {
    if (other == null) return 1;
    if (Type != other.Type) return Type == OvlType.Common ? -1 : 1;
    return GetHashCode().CompareTo(other.GetHashCode());
  }

  public object Clone() {
    throw new NotImplementedException();
  }
}

/// <summary>Extension methods for reading binary structures from streams.</summary>
public static class StreamExtensions {
  /// <summary>Read a blittable struct from the current stream position.</summary>
  /// <typeparam name="T">The struct type to read.</typeparam>
  /// <returns>The deserialized struct, or <see langword="null"/> if not enough bytes remain.</returns>
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

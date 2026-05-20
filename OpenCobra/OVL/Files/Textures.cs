// Textures
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using CommunityToolkit.HighPerformance;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenCobra.OVL.Files;

public class Texture(string name, TextureFormat format, uint width, uint height, uint mipCount = 1) : IDisposable {
  private bool disposed;

  /// <summary>
  /// The symbol name of the texture, e.g. "GUIIcon.txs".
  /// </summary>
  public string Name { get; private set; } = name;
  public readonly TextureFormat Format = format;
  /// <summary>
  /// Symbol reference to a Texture Style (TXS).
  /// </summary>
  public string? Style;
  public readonly uint Width = width;
  public readonly uint Height = height;
  public readonly uint MipCount = mipCount;
  public readonly bool IsCompressed = format.IsCompressed();
  /// <summary>
  /// The decoded texture data.
  /// </summary>
  public readonly Image<Rgba32>[] MipLevels = new Image<Rgba32>[mipCount == 0 ? 1 : mipCount];

  protected virtual void Dispose(bool disposing) {
    if (disposed || !disposing) return;
    foreach (var mipLevel in MipLevels) mipLevel.Dispose();
    disposed = true;
  }

  public void Dispose() {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  /// <returns>A clone of this texture with a new <paramref name="name"/>.</returns>
  public Texture WithName(string name) {
    var clone = MemberwiseClone() as Texture;
    Debug.Assert(clone != null);
    clone.Name = name;
    return clone;
  }
}

public static class Textures {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static bool IsDecodable(TextureFormat _) => false;

  // Extract all textures from an OVL
  public static TextureCollection Extract(Ovl ovl) {
    // Find all tex, flic, and btbl files
    var textureTypes = new[] { FileType.Texture, FileType.Flic, FileType.BitmapTable };
    var textureFilesData =
      from type in textureTypes
      from file in ovl.Keys.Where(file => file.Type == type)
      select new OvlData(ovl.Name, ovl.Version, file, ovl[file]);

    // Decode texture data in parallel
    // QUESTION: What will disk resource contention look like here?
    var bag = new ConcurrentBag<Texture>();
    var failures = new ConcurrentBag<OvlFile>();

    // Read bitmap tables FIRST, because other flics in the archive may depend on it
    // If an archive contains a bitmap table, we cannot collect texture references until it is read
    var textureFiles = textureFilesData as OvlData[] ?? [.. textureFilesData];
    var bitmapTableData = textureFiles.Where(fileData => fileData.File.Type == FileType.BitmapTable);
    var otherTextureData = textureFiles.Where(fileData => fileData.File.Type != FileType.BitmapTable);

    // Read bitmap tables in parallel
    var bitmapTables = new Dictionary<string, Texture[]>();
    Parallel.ForEach(bitmapTableData, data => {
      var name = data.File.ToString();
      using var fs = new FileStream(data.File.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
      fs.Seek(data.Entry.Offset, SeekOrigin.Begin);

      var table = ReadBitmapTable(name, fs);
      bitmapTables[data.OvlName] = table;
      foreach (var texture in table) bag.Add(texture);
    });

    // Read other textures in parallel
    Parallel.ForEach(otherTextureData, data => {
      try {
        var name = data.File.ToString();
        var type = data.File.Type;
        var version = data.Version;
        var bitmapTable = bitmapTables.GetValueOrDefault(data.OvlName);
        using var fs = new FileStream(data.File.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(data.Entry.Offset, SeekOrigin.Begin);

        // ReSharper disable once ConvertIfStatementToSwitchStatement for brevity
        if (type == FileType.Texture)
          foreach (var texture in ReadTextures(name, fs, ovl, bitmapTable))
            bag.Add(texture);
        else if (type == FileType.Flic) {
          var flicData = ovl.ReadResource(data.File);
          if (flicData != null)
            bag.Add(ReadFlic(name, flicData, version, table: bitmapTable));
        }
        else throw new NotImplementedException($"Unknown file type: {type.ToTagString()}");
      } catch (Exception ex) {
        logger.Error($"Failed to decode {data.File}:\n{ex.Message}");
        failures.Add(data.File);
      }
    });

    if (!failures.IsEmpty) logger.Error(
      "Failed to decode {count} textures from {x} OVLs.",
      failures.Count,
      textureFiles.Length
    );

    return [.. bag];
  }

  // See ManagerTEX.cpp
  private static IEnumerable<Texture> ReadTextures(string name, Stream stream, Ovl ovl, Texture[]? table = null) {
    using var reader = new BinaryReader(stream);

    var read = reader.Read<Tex>(out var tex);
    Debug.Assert(read == Marshal.SizeOf<Tex>());

    // A texture is NOT relocated when count is zero
    var count = Math.Max(tex.Count, tex.OtherCount);
    if (count == 0) throw new NotImplementedException("How the hell do you read non-relocated TEX flics?");

    for (var i = 0; i < count; i++) {
      var flicData = ovl.ReadResource(tex.Flic);
      if (flicData != null)
        yield return ReadFlic(name, flicData, ovl.Version, table: table, isRelocatedPtr: true);

      // TODO: Read texture style symbol reference
      // See ManagerTEX.cpp:123
      // See https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/LodSymRefManager.cpp#L161
    }
  }

  // See ManagerFLIC.cpp
  private static Texture ReadFlic(string name, ReadOnlyMemory<byte> data, Version ovlVersion, Texture[]? table = null, bool isRelocatedPtr = false) {
    using var reader = new BinaryReader(data.AsStream());

    if (isRelocatedPtr)
      reader.BaseStream.Position += 4;

    // Flics are either:
    // A: Indices to bitmaps in the same OVL's bitmap table, or
    // B: Standalone flics.
    var read = reader.Read<Flic>(out _);
    Debug.Assert(read == Marshal.SizeOf<Flic>());

    if (table != null) {
      var index = reader.ReadUInt32();
      Debug.Assert(index < table.Length);
      if (ovlVersion == Version.Five)
        reader.BaseStream.Position += Marshal.SizeOf<ExtraDataInfoV5>();
      return table[index].WithName(name);
    }

    var header = reader.ReadFlicHeader();
    var format = header.Format;
    var texture = new Texture(name, format, header.Width, header.Height, header.MipCount);
    for (var i = 0; i < header.MipCount; i++) {
      read = reader.Read<FlicMipHeader>(out var mipHeader);
      Debug.Assert(read == Marshal.SizeOf<FlicMipHeader>());
      // QUESTION: What is the purpose of `mipHeader.pitch`?
      // QUESTION: Ought `mipHeader.blocks` be used to calculate the size?
      var size = Convert.ToInt32(mipHeader.Pitch * mipHeader.Blocks);
      var pixels = reader.ReadBytes(size);
      texture.MipLevels[i] = ReadOnlySpanExtensions.ToImage(pixels, format);
    }

    // NOTE: According to ManagerFLIC.cpp, there's trailing data:
    // - sizeof(currentTexture->second)
    // - A null/empty FlicMipHeader
    // - For Version.Five OVLs: sizeof(ExtraDataInfoV5)
    // QUESTION: Do we need to read this trailing data?

    return texture;
  }

  // See ManagerBTBL.cpp
  private static Texture[] ReadBitmapTable(string name, Stream stream) {
    using var reader = new BinaryReader(stream);

    var header = reader.ReadBitmapTable();
    if (header.Length == 0) return [];

    reader.BaseStream.Position += 8;
    var textures = new Texture[header.Length];
    var headers = new FlicHeader[header.Length];
    for (var i = 0; i < header.Length; i++)
      headers[i] = reader.ReadFlicHeader();

    for (var i = 0; i < header.Length; i++) {
      var flic = headers[i];
      Debug.Assert(flic.MipCount == 0 || flic.MipCount == 9);

      textures[i] = new Texture(name, flic.Format, flic.Width, flic.Height, flic.MipCount);
      var mipCount = flic.MipCount == 0 ? 1u : flic.MipCount;
      for (var mip = 0; mip < mipCount; mip++) {
        var mipDim = Math.Max(1u, flic.Width >> mip);
        var size = Convert.ToInt32(mipDim * mipDim * flic.Format.BlockSize() / 16);
        var pixels = reader.ReadBytes(size);
        textures[i].MipLevels[mip] = ReadOnlySpanExtensions.ToImage(pixels, flic.Format);
      }
    }

    return textures;
  }
}

public class TextureCollection : IReadOnlyList<Texture> {
  private readonly Dictionary<string, Texture> textures = [];

  public Texture this[int index] => textures.Values.ElementAt(index);
  public Texture this[string name] => textures[name];

  Texture IReadOnlyList<Texture>.this[int index] => throw new NotImplementedException();

  public IEnumerable<string> Names => [.. textures.Keys];
  public int Count => textures.Count;

  int IReadOnlyCollection<Texture>.Count => Count;

  public IEnumerator<Texture> GetEnumerator() => textures.Values.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  internal void Add(Texture texture) => textures[texture.Name] = texture;

  // ReSharper disable once ParameterHidesMember
  internal TextureCollection AddRange(IEnumerable<Texture> textures) {
    foreach (var texture in textures)
      this.textures[texture.Name] = texture;
    return this;
  }
}

internal record struct OvlData(string OvlName, Version Version, OvlFile File, OvlEntry Entry);

[StructLayout(LayoutKind.Sequential, Size = 14)]
internal readonly struct ExtraDataInfoV5 {
  readonly uint index;
  readonly ushort unk01; // Usually 0xFFFF, only the second structure for mdl extradata has 0
  readonly ushort unk02; // 1 or 2
  /**
   * 1: mdl (2nd), flic, txt, modelanim
   * 2: bmptbl, mdl (1st)
   */
  readonly uint unk03; // Only ever seen 1
  readonly ushort unk04; // 1 or 2, usually matches unk02
}

[StructLayout(LayoutKind.Sequential, Size = 8)]
internal readonly struct BitmapTable {
  [SeenInGame(Value = 0)]
  public readonly uint Unk;
  /// <summary>
  /// Number of textures stored in the table.
  /// </summary>
  public readonly uint Length;
}

[StructLayout(LayoutKind.Sequential, Size = 12)]
internal readonly struct Flic {
  [SeenInGame(Value = 0)]
  public readonly uint DataRelocation;
  [SeenInGame(Value = 1)]
  public readonly uint Unk1;
  [SeenInGame(Value = 1)]
  public readonly float Unk2;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicHeader {
  public readonly TextureFormat Format;
  public readonly uint Width;
  public readonly uint Height;
  public readonly uint MipCount;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicMipHeader {
  public readonly uint Width;
  public readonly uint Height;
  public readonly uint Pitch;
  public readonly uint Blocks;
}

/// <remarks>
/// See <a href="https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/include/icontexture.h#L167">icontexture.h</a>.
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
internal struct Tex {
  // ReSharper disable once UnusedMember.Global because this struct has initializers
  public Tex() { }

  [field: FieldOffset(0)]
  public uint Unk1 { get; } = 0x70007;

  [field: FieldOffset(4)]
  public uint Unk2 { get; } = 0x70007;

  [field: FieldOffset(8)]
  public uint Unk3 { get; } = 0x70007;

  [field: FieldOffset(12)]
  public uint Unk4 { get; } = 0x70007;

  [field: FieldOffset(16)]
  public uint Unk5 { get; } = 0x70007;

  [field: FieldOffset(20)]
  public uint Unk6 { get; } = 0x70007;

  [field: FieldOffset(24)]
  public uint Unk7 { get; } = 0x70007;

  [field: FieldOffset(28)]
  public uint Unk8 { get; } = 0x70007;

  [FieldOffset(32)]
  public uint Count;

  [FieldOffset(36)]
  [SeenInGame(Value = 8)]
  public readonly uint Unk10;

  [FieldOffset(40)]
  [SeenInGame(Value = 0x10)]
  public readonly uint Unk11;

  [FieldOffset(44)]
  public RelocationPointer TextureData;

  /// <remarks>
  /// <list type="bullet">
  /// <item>Lo-word is a count. See <see cref="OtherCount"/> and <see cref="Count"/>.</item>
  /// <item>
  /// Hi-word is addon pack and in this case determines the number of unknown longs at the end of the structure.
  /// </item>
  /// </list>
  /// </remarks>
  [FieldOffset(48)]
  [SeenInGame(Value = 1)]
  public uint CountAndAddon;

  public readonly ushort OtherCount => Convert.ToUInt16(CountAndAddon & 0xFFFF);
  /// <summary>
  /// Determines the shape of the rest of this structure.
  /// </summary>
  public readonly Addon Addon => (Addon) Convert.ToUInt16((CountAndAddon >> 16) & 0xFFFF);

  /// <summary>
  /// An array of <see cref="Files.Flic"/> textures.
  /// </summary>
  /// <remarks>
  /// <para>This array is <i>not</i> relocated if <see cref="OtherCount"/> or <see cref="Count"/> are zero.</para>
  /// <para>Always points to location before flic data.</para>
  /// </remarks>
  [FieldOffset(52)]
  public RelocationPointer Flic;

  /// <summary>
  /// Pointer to the rest of this structure's data, i.e. <see cref="TexExtra"/>.
  /// </summary>
  [FieldOffset(56)]
  public readonly RelocationPointer ExtraData;
}

[StructLayout(LayoutKind.Explicit)]
internal struct TexExtra {
  /// <summary>
  /// A pointer back to the beginning of the <see cref="Tex"/> struct.
  /// </summary>
  [FieldOffset(0)]
  public readonly RelocationPointer Tex;

  /// <summary>
  /// A relocated array of <see cref="Files.Flic"/> textures.
  /// </summary>
  [FieldOffset(4)]
  public RelocationPointer Flic;
}

internal static class BinaryReaderExtensions {
  public static BitmapTable ReadBitmapTable(this BinaryReader reader) =>
    reader.Read<BitmapTable>(out var table) != 0 ? table : default;

    public static FlicHeader ReadFlicHeader(this BinaryReader reader) =>
      reader.Read<FlicHeader>(out var flic) != 0 ? flic : default;

  /// <summary>
  /// Reads a structure of type <typeparamref name="T"/> from the binary reader and returns the number of bytes read.
  /// </summary>
  public static uint Read<T>(this BinaryReader reader, out T data) {
    byte[] bytes = reader.ReadBytes(Marshal.SizeOf<T>());

    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    var ptr = handle.AddrOfPinnedObject();
    if (ptr == nint.Zero) {
      data = default!;
      return 0;
    }
    var structure = Marshal.PtrToStructure<T>(ptr);
    if (structure == null) data = default!;
    handle.Free();

    data = structure!;
    return Convert.ToUInt32(bytes.Length);
  }
}

internal static class ReadOnlySpanExtensions {
  public static Image<Rgba32> ToImage(this ReadOnlySpan<byte> span, TextureFormat format) {
    if (format == TextureFormat.A8R8G8B8) {
      using var image = Image.Load<Argb32>(span);
      return image.CloneAs<Rgba32>();
    }

    throw new InvalidOperationException($"Unsupported texture format: {format}");
  }
}

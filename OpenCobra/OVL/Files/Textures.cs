// Textures
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
    // Find all tex, flic, and btbl files and read their data, in parallel
    var textureTypes = new[] { FileType.Texture, FileType.Flic, FileType.BitmapTable };
    // QUESTION: What will disk resource contention look like here?
    var textureFilesData =
      from type in textureTypes
      from file in ovl.Keys.Where(file => file.Type == type).AsParallel()
        .WithDegreeOfParallelism(Environment.ProcessorCount)
        .WithMergeOptions(ParallelMergeOptions.NotBuffered)
      let data = ovl.ReadResource(file)
      where data != null
      select new OvlData(ovl.Name, file, data);

    // Decode texture data in parallel
    var bag = new ConcurrentBag<Texture>();
    var failures = new ConcurrentBag<OvlFile>();

    // Read bitmap tables FIRST, because other flics in the archive may depend on it
    // If an archive contains a bitmap table, we cannot collect texture references until it is read
    var textureFiles = textureFilesData as OvlData[] ?? [.. textureFilesData];
    var bitmapTableData = textureFiles.Where(fileData => fileData.File.Type == FileType.BitmapTable);
    var otherTextureData = textureFiles.Where(fileData => fileData.File.Type != FileType.BitmapTable);

    // Read bitmap tables in parallel
    var bitmapTables = new Dictionary<string, Texture[]>();
    Parallel.ForEach(bitmapTableData, fileData => {
      var name = fileData.File.ToString();
      var data = fileData.Data;

      var table = ReadBitmapTable(name, data);
      bitmapTables[fileData.OvlName] = table;
      foreach (var texture in table) bag.Add(texture);
    });

    // Read other textures in parallel
    Parallel.ForEach(otherTextureData, fileData => {
      try {
        var name = fileData.File.ToString();
        var data = fileData.Data;
        var bitmapTable = bitmapTables.GetValueOrDefault(fileData.OvlName);

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (fileData.File.Type == FileType.Texture)
          bag.Add(ReadTexture(name, ovl, data));
        else if (fileData.File.Type == FileType.Flic)
          bag.Add(ReadFlic(name, data, ovl.Version, bitmapTable));
      } catch {
        logger.Error("Failed to decode {FileName}", fileData.File.ToString());
        failures.Add(fileData.File);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} textures from {x} OVL{suffix}.",
        failures.Count,
        textureFiles.Length,
        textureFiles.Length != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  // See ManagerTEX.cpp
  private static Texture ReadTexture(string name, Ovl ovl, ReadOnlyMemory<byte> bytes) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var read = reader.Read<Tex>(out var tex);
    Debug.Assert(read == Marshal.SizeOf<Tex>());
    // Not relocated if `tex.count` or `tex.Unk12` are 0.
    var flics = new Flic[Math.Max(tex.Count, tex.Unk12)];
    for (var i = 0; i < flics.Length; i++) {
      read = reader.Read<Flic>(out var flic);
      Debug.Assert(read == Marshal.SizeOf<Flic>());
      flics[i] = flic;
      // TODO: Read texture style symbol reference
      // See ManagerTEX.cpp:123
      // See https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVLng/LodSymRefManager.cpp#L161
    }

    // Resolve the first flic entry's DataRelocation
    if (flics.Length > 0 && flics[0].DataRelocation != 0) {
      if (ovl.TryResolveRelocation(flics[0].DataRelocation, out var blockData, out var relOffset)) {
        var offset = Convert.ToInt32(relOffset);
        var length = Convert.ToInt32(blockData.Length - offset);
        var texture = ReadFlic(name, new Memory<byte>(blockData, offset, length), ovl.Version, null);

        // Default style to GUI icon for icon textures
        if (tex.Type == TextureType.Icon)
          texture.Style = "GUIIcon";
        // Try to resolve the texture style from the string table, removing the texture type suffix
        else if (tex.Data != 0 && ovl.TryResolveString(tex.Data, out var styleName))
          texture.Style = styleName.Split(':')[0];
      }
    }
    throw new InvalidOperationException($"Failed to resolve flic data for {name}");
  }

  private static Texture ReadFlic(
    string name, ReadOnlyMemory<byte> bytes, Version ovlVersion, Texture[]? table = null
  ) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

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
    Debug.Assert(header.Format != 0);
    Debug.Assert(header.Width > 0 && header.Height > 0);

    var format = header.Format;
    var texture = new Texture(name, format, header.Width, header.Height, header.MipCount);
    // FIXME: Reference computes mip count by right-shifting width until 0, then loops while checking (width != 0) && (height != 0) && (mh.Pitch != 0) && (mh.Blocks != 0). OpenCobra blindly reads header.MipCount times without validating that `mipHeader.Width` and `mipHeader.Height` match expected downsampled dimensions.
    for (var i = 0; i < header.MipCount; i++) {
      read = reader.Read<FlicMipHeader>(out var mipHeader);
      Debug.Assert(read == Marshal.SizeOf<FlicMipHeader>());

      Debug.Assert(mipHeader.Width == Math.Max(1u, header.Width >> i),
        $"Mip {i} width mismatch: expected {Math.Max(1u, header.Width >> i)}, got {mipHeader.Width}");
      Debug.Assert(mipHeader.Height == Math.Max(1u, header.Height >> i),
        $"Mip {i} height mismatch: expected {Math.Max(1u, header.Height >> i)}, got {mipHeader.Height}");
      Debug.Assert(mipHeader.Pitch > 0, $"Mip {i} has zero pitch");
      Debug.Assert(mipHeader.Blocks > 0, $"Mip {i} has zero blocks");

      // QUESTION: What is the purpose of `mipHeader.Pitch`?
      var size = Convert.ToInt32(mipHeader.Pitch * mipHeader.Blocks);
      Debug.Assert(reader.BaseStream.Position + size <= reader.BaseStream.Length, $"Mip {i} data exceeds file size");
      ReadOnlySpan<byte> data = reader.ReadBytes(size);
      texture.MipLevels[i] = data.ToImage(format);
    }

    // NOTE: According to ManagerFLIC.cpp, there's trailing data:
    // - sizeof(currentTexture->second)
    // - A null/empty FlicMipHeader
    // - For Version.Five OVLs: sizeof(ExtraDataInfoV5)
    // QUESTION: Do we need to read this trailing data?

    return texture;
  }

  // See ManagerBTBL.cpp
  private static Texture[] ReadBitmapTable(string name, ReadOnlyMemory<byte> bytes) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var header = reader.ReadBitmapTable();
    if (header.Length == 0) return [];

    reader.BaseStream.Position += 8;
    var textures = new Texture[header.Length];
    for (int i = 0; i < header.Length; i++) {
      var flic = reader.ReadFlicHeader();
      Debug.Assert(flic.MipCount == 0 || flic.MipCount == 9);

      var size = flic.Width * flic.Height * flic.Format.BitsPerPixel() / 8;
      textures[i] = new Texture(name, flic.Format, flic.Width, flic.Height, flic.MipCount);
      for (int mip = 0; mip < flic.MipCount; mip++) {
        var data = reader.ReadBytes(Convert.ToInt32(size));
        textures[i].MipLevels[mip] = Image.Load<Rgba32>(data);
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

internal record struct OvlData(string OvlName, OvlFile File, ReadOnlyMemory<byte> Data);

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
  [SeenInGame(Values = [0])]
  public readonly uint Unk;
  /// <summary>
  /// Number of textures stored in the table.
  /// </summary>
  public readonly uint Length;
}

[StructLayout(LayoutKind.Sequential, Size = 12)]
internal readonly struct Flic {
  [SeenInGame(Values = [0])]
  public readonly uint DataRelocation;
  [SeenInGame(Values = [1])]
  public readonly uint Unk1;
  [SeenInGame(Values = [1])]
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
  [SeenInGame(Values = [8])]
  public readonly uint Unk10;

  [FieldOffset(40)]
  [SeenInGame(Values = [0x0D, 0x10])]
  public readonly uint Unk11;
  public readonly TextureType Type => (TextureType)Unk11;

  [FieldOffset(44)]
  [SeenInGame(Values = [1])]
  public readonly uint Unk12;

  [FieldOffset(48)]
  public uint Data;
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
    byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    var ptr = handle.AddrOfPinnedObject();
    if (ptr == nint.Zero) {
      data = default!;
      return 0;
    }
    var structure = (T?)Marshal.PtrToStructure(ptr, typeof(T));
    if (structure == null) data = default!;
    handle.Free();

    data = structure!;
    return Convert.ToUInt32(bytes.Length);
  }
}

internal static class ReadOnlySpanExtensions {
  public static Image<Rgba32> ToImage(this ReadOnlySpan<byte> span, TextureFormat format) {
    // QUESTION: Add support for TextureFormat.P8?
    // Reference's `ReadTexture2` shows P8 needs palette lookup to convert indexed colors to RGBA.
    if (format == TextureFormat.A8R8G8B8) {
      using var image = Image.Load<Argb32>(span);
      return image.CloneAs<Rgba32>();
    }

    throw new InvalidOperationException($"Unsupported texture format: {format}");
  }
}

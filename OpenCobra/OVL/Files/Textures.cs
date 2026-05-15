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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.OVL.Files;

public class Texture(string name, TextureFormat format, uint width, uint height, uint mipCount = 1) : IDisposable {
  private bool disposed;

  /// <summary>
  /// The symbol name of the texture, e.g. "GUIIcon.txs".
  /// </summary>
  public readonly string Name = name;
  /// <summary>
  /// Symbol reference to a Texture Style (TXS).
  /// </summary>
  public string? textureStyle;
  public readonly TextureFormat Format = format;
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
}

public static class Textures {
  public static bool IsDecodable(TextureFormat _) => false;

  // Extract all textures from an OVL
  public static TextureCollection Extract(Ovl ovl) {
    // Find all tex, flic, and btbl files and read their data, in parallel
    var textureTypes = new FileType[] { FileType.Texture, FileType.Flic, FileType.BitmapTable };
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
    // i.e. If an archive contains a bitmap table, we cannot collect texture references until it is read.
    var bitmapTableData = textureFilesData.Where(fileData => fileData.File.Type == FileType.BitmapTable);
    var otherTextureData = textureFilesData.Where(fileData => fileData.File.Type != FileType.BitmapTable);

    // Read bitmap tables in parallel
    var bitmapTables = new Dictionary<string, Texture[]>();
    Parallel.ForEach(bitmapTableData, fileData => {
      var name = fileData.File.ToString();
      var data = fileData.Data;

      var table = ReadBitmapTable(name, data);
      bitmapTables[fileData.OvlName] = table;
      foreach (var texture in table)
        if (texture != null) bag.Add(texture);
    });

    // Read other textures in parallel
    Parallel.ForEach(otherTextureData, fileData => {
      try {
        var name = fileData.File.ToString();
        var data = fileData.Data;
        Texture? texture = null;

        switch (fileData.File.Type) {
          case FileType.Texture:
            texture = ReadTexture(name, data);
            if (texture != null) bag.Add(texture);
            break;
          case FileType.Flic:
            texture = ReadFlic(name, data);
            if (texture != null) bag.Add(texture);
            break;
        }
      } catch {
        Debug.WriteLine($"Failed to decode {fileData.File.Name}.{fileData.File.Type.ToTagString()}");
        failures.Add(fileData.File);
      }
    });

    return [.. bag];
  }

  private static Texture ReadTexture(string name, ReadOnlyMemory<byte> bytes) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var read = reader.Read<Tex>(out var tex);
    Debug.Assert(read == Marshal.SizeOf<Tex>());
    // Not relocated if `tex.count` or `tex.Unk12` are 0.
    var flics = new Flic[Math.Max(tex.count, tex.Unk12)];
    for (int i = 0; i < flics.Length; i++) {
      read = reader.Read<Flic>(out var flic);
      Debug.Assert(read == Marshal.SizeOf<Flic>());
      flics[i] = flic;
      // TODO: Read texture style symbol reference
      // See ManagerTEX.cpp
    }
  }

  // Flics are either:
  // Indices to bitmaps in a bitmap table, or
  // Standalone flics.
  private static Texture ReadFlic(string name, ReadOnlyMemory<byte> bytes, BitmapTable? table = null) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var read = reader.Read<Flic>(out var flic);
    Debug.Assert(read == Marshal.SizeOf<Flic>());

    if (table != null && table.HasValue) {
      uint index = reader.ReadUInt32();
      reader.BaseStream.Position += Marshal.SizeOf<ExtraDataInfoV5>();
      Debug.Assert(index < table.Value.Textures.Length);
      return table.Value.Textures[index];
    }

    // TODO: Read FlicHeader
    // See ManagerFLIC.cpp
  }

  private static Texture[] ReadBitmapTable(string name, ReadOnlyMemory<byte> bytes) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var header = reader.ReadBitmapTable();
    if (header.count == 0) return [];

    reader.BaseStream.Position += 8;
    var textures = new Texture[header.count];
    for (int i = 0; i < header.count; i++) {
      var flic = reader.ReadFlicHeader();
      Debug.Assert(flic.mipCount == 0 || flic.mipCount == 9);

      var size = flic.width * flic.height * flic.format.BitsPerPixel() / 8;
      textures[i] = new Texture(name, flic.format, flic.width, flic.height, flic.mipCount);
      for (int mip = 0; mip < flic.mipCount; mip++) {
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
  internal TextureCollection AddRange(IEnumerable<Texture> textures) {
    foreach (var texture in textures)
      this.textures[texture.Name] = texture;
    return this;
  }
}

internal record struct OvlData(string OvlName, OvlFile File, ReadOnlyMemory<byte> Data);
internal record struct BitmapTable(string OvlName, Texture[] Textures);

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
internal readonly struct BmpTable {
  public readonly uint unk;   // always 0 in game files
  public readonly uint count; // Number of stored textures
}

[StructLayout(LayoutKind.Sequential, Size = 12)]
internal readonly struct Flic {
  public readonly uint flicDataPtr;   // always 0 in game files
  public readonly uint unk1;          // always 1
  public readonly float unk2;         // always 1.0
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicHeader {
  public readonly TextureFormat format;
  public readonly uint width;
  public readonly uint height;
  public readonly uint mipCount;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicMipHeader {
  public readonly uint width;
  public readonly uint height;
  public readonly uint pitch;
  public readonly uint blocks;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Tex {
  readonly uint unk1 = 0x70007;
  readonly uint unk2 = 0x70007;
  readonly uint unk3 = 0x70007;
  readonly uint unk4 = 0x70007;
  readonly uint unk5 = 0x70007;
  readonly uint unk6 = 0x70007;
  readonly uint unk7 = 0x70007;
  readonly uint unk8 = 0x70007;
  public uint count;
  public readonly uint Unk10;   // usually 8
  public readonly uint Unk11;   // usually 0x10
  public readonly uint Unk12; // usually 1

  public readonly uint Unk1 => unk1;
  public readonly uint Unk2 => unk2;
  public readonly uint Unk3 => unk3;
  public readonly uint Unk4 => unk4;
  public readonly uint Unk5 => unk5;
  public readonly uint Unk6 => unk6;
  public readonly uint Unk7 => unk7;
  public readonly uint Unk8 => unk8;

  public Tex() { }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct FlicTexture {
  readonly FlicHeader header;
  readonly FlicMipHeader mipHeader;
}

internal static class BinaryReaderExtensions {
  public static BmpTable ReadBitmapTable(this BinaryReader reader) =>
    reader.Read<BmpTable>(out var table) != 0 ? table : default;

    public static FlicHeader ReadFlicHeader(this BinaryReader reader) =>
      reader.Read<FlicHeader>(out var flic) != 0 ? flic : default;

  public static FlicTexture ReadFlic(this BinaryReader reader) =>
    reader.Read<FlicTexture>(out var flic) != 0 ? flic : default;

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

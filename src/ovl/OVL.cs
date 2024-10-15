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

public struct File {
  public uint size;
  public uint offset;
  public uint relativeOffset;
  /// This is `unsigned char*` in Importer
  public byte[] data;
  public uint unk;
}

public struct Symbol {
  public string symbol;
  public ulong[] data;
  public uint isPointer;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SymbolHashed {
  public Symbol symbol;
  public uint checksum;
}

public struct Loader {
  public uint loaderType;
  public ulong[] data;
  public uint hasExtraData;
  public Symbol? sym;
  public uint symbolsToResolve;
}

public struct SymbolRef {
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
  common,
  unique
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextString {
  public string name;
  public string value;
}

/// Used in Type 0 Files
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Resource {
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

public struct FlexiTextureData {
  public uint scale;
  public uint width, height;
  /// Combinable recolorability flags.
  public Recolorable recolorable;
  public byte[] palette;
  public byte[] texture;
  public byte[] alpha;
}

public unsafe struct FlexiTextureInfo {
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

public struct FlexiTexture {
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
  private string internalName;
  private FileStream file;
  private BinaryReader reader;
  private OvlType type;
  private File[] files = new File[9];
  private List<string> references = new();

  // QUESTION: Is char FileName[MAX_PATH]; in importer?
  public readonly string name;
  // Added to store the unique ID.
  public EffectPoint[] effectPoints = Array.Empty<EffectPoint>();
  public Mesh[] meshes = Array.Empty<Mesh>();
  public FlexiTextureInfo[] flexiTextureItems = Array.Empty<FlexiTextureInfo>();

  public Ovl(FileStream stream) {
    this.file = stream;
    this.name = this.internalName = Path.GetFileName(file.Name);
    this.reader = new BinaryReader(file, Encoding.ASCII, false);
  }

  public static Ovl Open(string fileName) {
    var invalidOvlError = $"File is not an OVL archive: {fileName}";
    //var info = new FileInfo(fileName);
    //Debug.Assert(info.Exists);

    var file = System.IO.File.Open(fileName, FileMode.Open);
    var ovl = new Ovl(file);
    Debug.Assert(file.Length >= Marshal.SizeOf<OvlHeader>(), invalidOvlError);

    var maybeHeader = file.ReadStruct<OvlHeader>();
    Debug.Assert(maybeHeader.HasValue);
    var header = maybeHeader.Value;
    // 0x4647524b
    // 0x4b524746
    // "FGRK"c.representation
    Debug.Assert(header.magic == 0x4647524b, invalidOvlError);

    // Read reference count
    if (header.version == (uint) Version.one)
      ovl.references.EnsureCapacity((int) header.references);
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

    return ovl;
  }
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

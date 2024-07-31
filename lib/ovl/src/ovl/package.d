/// License: GPL 2.0
module ovl;

import std.conv : to;
import std.exception : enforce;
static import std.stdio;
import std.typecons : BitFlags;

import ovl.enums;
import ovl.gui;

// TODO: Publish this package on dub
// QUESTION: Extract this library into it's own module?

enum VERSION = 2;

// TODO: https://vibe-serialization.dpldocs.info/v1.0.4/vibe.data.serialization.html

// FIXME: Why isn't `OvlHeader` parsing working?
struct OvlHeader {
  uint magic;
  uint reserved;
  uint version_;
  uint references;
}

struct OvlFilesHeader {
  uint unk;
  uint fileTypeCount;
}

struct File {
  ulong size;
  ulong offset;
  ulong relativeOffset;
  // This is `unsigned char*` in Importer
  ubyte[] data;
  uint unk;
}

struct Symbol {
  string symbol;
  ulong* data;
  uint isPointer;
}

struct SymbolHashed {
  Symbol symbol;
  uint checksum;
}

struct Loader {
  uint loaderType;
  ulong* data;
  uint hasExtraData;
  Symbol* sym;
  uint symbolsToResolve;
}

struct SymbolRef {
  ulong* reference;
  string symbol;
  Loader* loader;
}

struct SymbolRefHashed {
  SymbolRef ref_;
  uint checksum;
}

enum OvlType {
  common,
  unique
}

struct TextString {
  string name;
  string value;
}

/// Used in Type 0 Files
struct Resource {
  uint length;
  ulong* data;
}

struct Vector3D {
  float x, y, z;
}

struct Vertex {
  Vector3D position;
  Vector3D normal;
  uint color;
  float tu, tv;
}

struct VertexWeighted {
  Vector3D position;
  Vector3D normal;
  byte[4] bone;
  ubyte[4] boneWeight;
  uint color;
  float tu, tv;
}

struct Face {
  int[3] vertex;
  int[3] uvs;
  int smoothing;
  int materialId;
  int ab;
  int bc;
  int ca;
}

struct Mesh {
  Mesh* mesh;
  string name;
  string textureName;
  string textureStyle;
  Vector3D boudingBox1;
  Vector3D boudingBox2;
}

struct Color {
  ubyte blue;
  ubyte green;
  ubyte reg;
  ubyte alpha;
}

struct FlexiTextureData {
  uint scale;
  uint width;
  uint height;
  /// Combinable recolorability flags.
  BitFlags!Recolorable recolorable;
  ubyte[] palette;
  ubyte[] texture;
  ubyte[] alpha;
}

struct FlexiTextureInfo {
  uint scale;
  uint width;
  uint height;
  /// Animation Speed, approx. frames per second.
  uint fps;
  BitFlags!Recolorable recolorable;
  uint offsetCount;
  ulong* offset1;
  uint nextCount;
  FlexiTextureData* next;
}

struct FlexiTexture {
  string textureName;
  ubyte[] data;
  ubyte[] alphaChannel;
  FlexiTextureData flexi;
  FlexiTextureInfo flexiInfo;
  Color colors;
}

struct Matrix {
  union {
    struct {
      float _11, _12, _13, _14;
      float _21, _22, _23, _24;
      float _31, _32, _33, _34;
      float _41, _42, _43, _44;
    }
    float[4][4] m;
  }
}

struct EffectPoint {
  string name;
  Matrix transform;
}

package {
  EffectPoint[] effectPoints;
  Mesh[] meshes;
  FlexiTextureInfo[] flexiTextureItems;
}

package T read(T)(std.stdio.File file) {
  assert(!file.eof, "Unexpected EOF!");
  T value;
  file.rawRead!T((&value)[0..1]);
  return value;
}

class Ovl {
  import std.path : baseName;

  // is char FileName[MAX_PATH]; in importer
  const string path;
  const string name;
  const OvlType type;
  File[9] files;
  string[] references;
  // Added to store the unique ID.
  string internalName;

  this(string name) {
    enforce(name !is null, "You must provide a name.");
    this(null, name);
  }
  package this(string path, string name) {
    import std.algorithm : canFind, endsWith, joiner;
    import std.array : array;
    import std.path : isValidPath, pathSeparator;
    import std.uni : asLowerCase;

    path = path is null ? [".", name].joiner(pathSeparator).array.to!string : path;
    enforce(path.isValidPath, "File does not exist: " ~ path);
    this.name = path.baseName;
    const nameLowered = this.name.asLowerCase.array.to!string;
    const extIsCommonOvl = nameLowered.endsWith(".common.ovl");
    enforce(extIsCommonOvl || nameLowered.endsWith(".unique.ovl"), "File is not an OVL archive: " ~ path);
    this.path = path;
    this.type = extIsCommonOvl ? OvlType.common : OvlType.unique;
  }

  void open() {
    assert(0, "Unimplemented!");
  }

  static Ovl createScenery(string name) {
    auto ovl = new Ovl(name);
    // TODO: See https://github.com/chances/rct3-importer/blob/431fbf2b5b5038c07ed197d29d12facdf319bc68/RCT3%20Importer/src/libOVL/ovl.cpp#L108
    return ovl;
  }
  static Ovl load(string path) {
    import std.algorithm : equal, sum;
    import std.file : exists;
    import std.path : stripExtension;
    debug import std.stdio : writeln;
    import std.string : format, representation;

    const invalidOvlError = "File is not an OVL archive: " ~ path;

    auto ovl = new Ovl(path, path.baseName.stripExtension);
    enforce(path.exists, "File does not exist: " ~ path);
    auto file = std.stdio.File(path, "rb");
    enforce(file.size >= OvlHeader.sizeof, invalidOvlError);

    OvlHeader header = file.read!OvlHeader();
    debug header.writeln;
    // "FGRK"c.representation
    enforce(header.magic == 0x4b524746, invalidOvlError);

    // Read reference count
    switch (header.version_) {
      case 1:
        ovl.references = new string[header.references];
        break;
      default:
        if (header.version_ != 4 && header.version_ != 5) throw new Exception(
          format!"Unknown OVL version: %d"(header.version_)
        );

        else if (header.version_ == 5) {
          // Skip unknowns
          auto subversionFlag = file.read!ulong;
          if (subversionFlag) {
            file.seek(file.tell + 12);
            char c;
            auto padding = 0;
            do {
              file.rawRead!char((&c)[0..1]);
              padding += 1;
              if (padding == 4) padding = 0;
            } while(c != 0);
          }
        }

        // FIXME: This is likely incorrect
        ovl.references = new string[file.read!uint];
        break;
    }

    string readString() {
      assert(!file.eof, "Unexpected EOF!");
      ubyte[] str = new ubyte[file.read!ushort];
      file.rawRead!ubyte(str.ptr[0..str.length]);
      return str.to!string.idup;
    }

    // Read references
    foreach (ref reference; ovl.references) reference = readString();

    // Read file index header
    auto filesHeader = file.read!OvlFilesHeader;
    debug filesHeader.writeln;
    // Read file loader headers
    struct LoaderHeader {
      string loader;
      string name;
      ulong type;
      string tag;
      long symbolCount;
      int symbolCountOrder;
    }
    auto loaders = new LoaderHeader[filesHeader.fileTypeCount];
    foreach (ref loader; loaders) loader = LoaderHeader(
      readString(), readString(), file.read!ulong, readString()
    );

    // V5 Loader Header stuff, number of symbols for each file type / loader header by index
    // This applies to the current common/unique file
    // The order the loaders appear here is important for the symbol order, they are primarily
    // sorted by the file type in this order, secondarily they are sorted by hash
    if (header.version_ == 5) foreach (i; 0 .. loaders.length) loaders[file.read!ulong].symbolCount = file.read!ulong;

    // Read file index size
    struct FileBlock {
      ulong[] fileSizes;
      ulong relativeOffset;
      ulong size;
    }
    FileBlock[9] fileBlocks;
    foreach (i, block; fileBlocks) {
      block.fileSizes = new ulong[file.read!ulong];

      if (header.version_ == 1) continue;
      // Skip unknowns
      file.seek(file.tell + 4 + (header.version_ == 5 ? 4 : 0));
      foreach (ref size; block.fileSizes) size = file.read!ulong;
      block.size = block.fileSizes.sum;
    }

    // Skip unknowns
    if (header.version_ == 4) file.seek(file.tell + 8);
    if (header.version_ == 5) {
      auto unkBytesCount = file.read!ulong;
      file.seek(file.tell + 4 + unkBytesCount);
      foreach (x; 0 .. file.read!ulong) file.seek(file.tell + 4);
    }

    // Read file index table
    auto offset = file.tell;
    foreach (i, ref f; ovl.files) {
      fileBlocks[i].relativeOffset = file.tell - offset;
      foreach (fileSizeIndex; 0 .. fileBlocks[i].fileSizes.length) {
        enforce(!file.eof, format!"File overflow (%d, %d)"(i, fileSizeIndex));

        if (header.version_ == 1) {
          // Read size
          fileBlocks[i].fileSizes[fileSizeIndex] = file.read!ulong;
          fileBlocks[i].size += fileBlocks[i].fileSizes[fileSizeIndex];
        }
        auto size = fileBlocks[i].fileSizes[fileSizeIndex];

        if (file.eof) continue;
        f.offset = file.tell;
        f.relativeOffset = f.offset - offset;
        offset += size;
        if (size) {
          ubyte[] data = new ubyte[size];
          file.rawRead!ubyte(data.ptr[0..size]);
          f.data = data;
        }
      }
    }

    // Read relocations
    auto relocations = new ulong[file.read!ulong];
    file.rawRead!ulong(relocations.ptr[0..relocations.length]);
    // Skip relocation unknowns
    if (header.version_ > 1) file.seek(file.tell + 4);

    // Read checksum
    auto checksum = file.read!(char[2]);
    // TODO: Assert the checsum matches internal state?

    assert(file.eof, "Entire OVL archive was not ingested!");
    file.close();

    return ovl;
  }

  void save(string path) {}
}

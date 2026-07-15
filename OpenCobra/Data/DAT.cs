// DAT.cs
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using System.Text;

namespace OpenCobra.Data;

/// <summary>Type tag for a DAT struct field, matching the on-disk field-kind name table.</summary>
public enum DatFieldKind {
  Bool,
  Int8,
  Int16,
  Int32,
  UInt8,
  UInt16,
  UInt32,
  Float32,
  Vector3,
  Matrix44,
  Orientation,
  ManagedObjectPtr,
  Reference,
  String,
  Array,
  List,
  Struct,
  GraphedValue,
  WaterManager,
  GETerrain,
  SkirtTrees,
  PathTileList,
  WaypointList,
  FlexiCacheList,
  ManagedImage,
  PathNodeArray,
  ResourceSymbol,
  StringTable,
  BlockingScenery,
}

internal static class DatFieldKindParser {
  /// <summary>
  /// Maps an on-disk field-kind name to its <see cref="DatFieldKind"/>. Names are matched
  /// case-sensitively as written in DAT field definitions (e.g. <c>GE_Terrain</c>, not
  /// <c>ge_terrain</c>) - the casing is inconsistent between kinds (mostly lowercase primitives,
  /// PascalCase container kinds), but that inconsistency is what's actually on disk.
  /// </summary>
  public static DatFieldKind ToDatFieldKind(this string kindName) => kindName switch {
    "array" => DatFieldKind.Array,
    "list" => DatFieldKind.List,
    "bool" => DatFieldKind.Bool,
    "float32" => DatFieldKind.Float32,
    "int8" => DatFieldKind.Int8,
    "int16" => DatFieldKind.Int16,
    "int32" => DatFieldKind.Int32,
    "managedobjectptr" => DatFieldKind.ManagedObjectPtr,
    "matrix44" => DatFieldKind.Matrix44,
    "orientation" => DatFieldKind.Orientation,
    "reference" => DatFieldKind.Reference,
    "uint8" => DatFieldKind.UInt8,
    "uint16" => DatFieldKind.UInt16,
    "uint32" => DatFieldKind.UInt32,
    "vector3" => DatFieldKind.Vector3,
    "struct" => DatFieldKind.Struct,
    "string" => DatFieldKind.String,
    "graphedValue" => DatFieldKind.GraphedValue,
    "WaterManager" => DatFieldKind.WaterManager,
    "GE_Terrain" => DatFieldKind.GETerrain,
    "SkirtTrees" => DatFieldKind.SkirtTrees,
    "PathTileList" => DatFieldKind.PathTileList,
    "waypointlist" => DatFieldKind.WaypointList,
    "flexicachelist" => DatFieldKind.FlexiCacheList,
    "managedImage" => DatFieldKind.ManagedImage,
    "pathnodearray" => DatFieldKind.PathNodeArray,
    "resourcesymbol" => DatFieldKind.ResourceSymbol,
    "stringTable" => DatFieldKind.StringTable,
    "BlockingScenery" => DatFieldKind.BlockingScenery,
    _ => throw new DatException($"Unknown DAT field kind '{kindName}'"),
  };
}

/// <summary>A single decoded field value within a <see cref="DatStructEntry"/>.</summary>
public abstract record DatValue(DatFieldKind Kind);

public sealed record DatBoolValue(bool Value) : DatValue(DatFieldKind.Bool);
public sealed record DatInt8Value(sbyte Value) : DatValue(DatFieldKind.Int8);
public sealed record DatInt16Value(short Value) : DatValue(DatFieldKind.Int16);
public sealed record DatInt32Value(int Value) : DatValue(DatFieldKind.Int32);
public sealed record DatUInt8Value(byte Value) : DatValue(DatFieldKind.UInt8);
public sealed record DatUInt16Value(ushort Value) : DatValue(DatFieldKind.UInt16);
public sealed record DatUInt32Value(uint Value) : DatValue(DatFieldKind.UInt32);
public sealed record DatFloat32Value(float Value) : DatValue(DatFieldKind.Float32);
public sealed record DatVector3Value(Vector3 Value) : DatValue(DatFieldKind.Vector3);
public sealed record DatMatrix44Value(Matrix4x4 Value) : DatValue(DatFieldKind.Matrix44);
public sealed record DatOrientationValue(Vector3 Value) : DatValue(DatFieldKind.Orientation);
public sealed record DatManagedObjectPtrValue(ulong Value) : DatValue(DatFieldKind.ManagedObjectPtr);
public sealed record DatReferenceValue(ulong Value) : DatValue(DatFieldKind.Reference);
public sealed record DatStringValue(string Value) : DatValue(DatFieldKind.String);

/// <summary>A fixed-size (<see cref="Length"/>) collection of <see cref="DatStructValue"/> elements.</summary>
public sealed record DatArrayValue(int Size, int Length, IReadOnlyList<DatStructValue> Elements) : DatValue(DatFieldKind.Array);

/// <summary>A variable-length collection of <see cref="DatStructValue"/> elements.</summary>
public sealed record DatListValue(int Size, int Length, IReadOnlyList<DatStructValue> Elements) : DatValue(DatFieldKind.List);

/// <summary>A nested struct value - either a named field's inline body, or an array/list element.</summary>
public sealed record DatStructValue(int Size, IReadOnlyList<DatStructEntry> Entries) : DatValue(DatFieldKind.Struct) {
  public IEnumerable<DatStructEntry> ByName(string name) => Entries.Where(e => e.Name == name);
  public DatStructEntry FirstByName(string name) =>
    Entries.FirstOrDefault(e => e.Name == name) ?? throw new DatException($"No field named '{name}'");
}

/// <summary>
/// A field body whose internal layout isn't decoded here - the raw bytes are preserved so a
/// caller (or a later library revision) can decode them without re-parsing the surrounding
/// struct/entry table. Covers <see cref="DatFieldKind.GraphedValue"/>, <see cref="DatFieldKind.WaterManager"/>,
/// <see cref="DatFieldKind.GETerrain"/>, <see cref="DatFieldKind.SkirtTrees"/>,
/// <see cref="DatFieldKind.PathTileList"/>, <see cref="DatFieldKind.WaypointList"/>,
/// <see cref="DatFieldKind.FlexiCacheList"/>, <see cref="DatFieldKind.ManagedImage"/>,
/// <see cref="DatFieldKind.PathNodeArray"/>, <see cref="DatFieldKind.ResourceSymbol"/>,
/// <see cref="DatFieldKind.StringTable"/>, and <see cref="DatFieldKind.BlockingScenery"/>.
/// </summary>
public sealed record DatOpaqueValue(DatFieldKind Kind, int Size, byte[] Data) : DatValue(Kind);

/// <summary>A named field value within a <see cref="DatStructValue"/> or top-level <see cref="DatEntry"/>.</summary>
public sealed record DatStructEntry(string Name, DatValue Value) {
  public DatFieldKind Kind => Value.Kind;

  public bool AsBool() => Value is DatBoolValue v ? v.Value : throw WrongType(DatFieldKind.Bool);
  public int AsInt32() => Value is DatInt32Value v ? v.Value : throw WrongType(DatFieldKind.Int32);
  public DatStructValue AsStruct() => Value as DatStructValue ?? throw WrongType(DatFieldKind.Struct);
  public DatArrayValue AsArray() => Value as DatArrayValue ?? throw WrongType(DatFieldKind.Array);
  public string AsString() => Value is DatStringValue v ? v.Value : throw WrongType(DatFieldKind.String);
  public ulong AsPtr() => Value is DatManagedObjectPtrValue v ? v.Value : throw WrongType(DatFieldKind.ManagedObjectPtr);
  public ulong AsRef() => Value is DatReferenceValue v ? v.Value : throw WrongType(DatFieldKind.Reference);

  private DatException WrongType(DatFieldKind expected) =>
    new($"Field '{Name}' is {Value.Kind}, expected {expected}");
}

/// <summary>A named, decoded record within a loaded <see cref="Dat"/> file.</summary>
public sealed record DatEntry(ulong Id, string Name, IReadOnlyList<DatStructEntry> Values) {
  public IEnumerable<DatStructEntry> ByName(string name) => Values.Where(v => v.Name == name);
  public DatStructEntry FirstByName(string name) =>
    Values.FirstOrDefault(v => v.Name == name) ?? throw new DatException($"No field named '{name}'");
}

/// <summary>Definition of one field within a <see cref="DatStructDefinition"/> or a container field's element type.</summary>
internal sealed record DatFieldDefinition(string Name, DatFieldKind Kind, int Size, IReadOnlyList<DatFieldDefinition> Fields) {
  public static DatFieldDefinition ReadDefinition(BinaryReader reader) {
    var name = DatReader.ReadPascalString(reader);
    var kind = DatReader.ReadPascalString(reader).ToDatFieldKind();
    var size = (int)reader.ReadUInt32();
    var fieldCount = reader.ReadUInt32();
    var fields = new List<DatFieldDefinition>((int)fieldCount);
    for (var i = 0; i < fieldCount; i++) fields.Add(ReadDefinition(reader));
    return new DatFieldDefinition(name, kind, size, fields);
  }

  public DatStructEntry ReadEntry(BinaryReader reader) => new(Name, ReadValue(reader));

  private DatValue ReadValue(BinaryReader reader) => Kind switch {
    DatFieldKind.Bool => new DatBoolValue(reader.ReadBoolean()),
    DatFieldKind.Int8 => new DatInt8Value(reader.ReadSByte()),
    DatFieldKind.Int16 => new DatInt16Value(reader.ReadInt16()),
    DatFieldKind.Int32 => new DatInt32Value(reader.ReadInt32()),
    DatFieldKind.UInt8 => new DatUInt8Value(reader.ReadByte()),
    DatFieldKind.UInt16 => new DatUInt16Value(reader.ReadUInt16()),
    DatFieldKind.UInt32 => new DatUInt32Value(reader.ReadUInt32()),
    DatFieldKind.Float32 => new DatFloat32Value(reader.ReadSingle()),
    DatFieldKind.Vector3 => new DatVector3Value(DatReader.ReadVector3(reader)),
    DatFieldKind.Matrix44 => new DatMatrix44Value(DatReader.ReadMatrix44(reader)),
    DatFieldKind.Orientation => new DatOrientationValue(DatReader.ReadVector3(reader)),
    DatFieldKind.ManagedObjectPtr => new DatManagedObjectPtrValue(reader.ReadUInt64()),
    DatFieldKind.Reference => new DatReferenceValue(reader.ReadUInt64()),
    DatFieldKind.String => new DatStringValue(DatReader.ReadLengthPrefixedString(reader)),
    DatFieldKind.Array => ReadArrayOrList(reader, isArray: true),
    DatFieldKind.List => ReadArrayOrList(reader, isArray: false),
    DatFieldKind.Struct => ReadStruct(reader),
    _ => ReadOpaque(reader),
  };

  /// <summary>
  /// Array/list container bodies carry their own size/length prefix, then <see cref="Length"/>
  /// elements each shaped like <see cref="Fields"/> - unlike <see cref="ReadStruct"/>, elements
  /// have no per-element size prefix of their own.
  /// </summary>
  private DatValue ReadArrayOrList(BinaryReader reader, bool isArray) {
    var size = (int)reader.ReadUInt32();
    var length = (int)reader.ReadUInt32();
    var elements = new List<DatStructValue>(length);
    for (var i = 0; i < length; i++) {
      var entries = Fields.Select(field => field.ReadEntry(reader)).ToList();
      elements.Add(new DatStructValue(0, entries));
    }
    return isArray
      ? new DatArrayValue(size, length, elements)
      : new DatListValue(size, length, elements);
  }

  private DatValue ReadStruct(BinaryReader reader) {
    var size = Size == 0 ? (int)reader.ReadUInt32() : Size;
    var entries = Fields.Select(field => field.ReadEntry(reader)).ToList();
    return new DatStructValue(size, entries);
  }

  /// <summary>
  /// Reads (and preserves, rather than discards) the raw body of a field kind this library
  /// doesn't decode - see <see cref="DatOpaqueValue"/>.
  /// </summary>
  private DatValue ReadOpaque(BinaryReader reader) {
    var size = Size == 0 ? (int)reader.ReadUInt32() : Size;
    return new DatOpaqueValue(Kind, size, reader.ReadBytes(size));
  }
}

/// <summary>Definition of one named struct (a DAT "class") in a file's struct table.</summary>
internal sealed record DatStructDefinition(string Name, IReadOnlyList<DatFieldDefinition> Fields) {
  public static DatStructDefinition ReadDefinition(BinaryReader reader) {
    var name = DatReader.ReadPascalString(reader);
    var fieldCount = reader.ReadUInt32();
    var fields = new List<DatFieldDefinition>((int)fieldCount);
    for (var i = 0; i < fieldCount; i++) fields.Add(DatFieldDefinition.ReadDefinition(reader));
    return new DatStructDefinition(name, fields);
  }

  public DatEntry ReadEntry(BinaryReader reader, ulong id) =>
    new(id, Name, Fields.Select(field => field.ReadEntry(reader)).ToList());
}

internal static class DatReader {
  /// <summary>Reads a Pascal-style length-prefixed ASCII string: a little-endian <c>u16</c> length, then that many ASCII bytes.</summary>
  public static string ReadPascalString(BinaryReader reader) =>
    Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadUInt16()));

  /// <summary>
  /// Reads a <see cref="DatFieldKind.String"/> field value: a little-endian <c>u32</c> byte
  /// length, ASCII-encoded unless immediately followed by the <c>0xEFEFEFEF</c> marker, in which
  /// case the remaining bytes (after consuming the marker) are UTF-16 encoded instead.
  /// </summary>
  public static string ReadLengthPrefixedString(BinaryReader reader) {
    var length = (int)reader.ReadUInt32();
    var encoding = Encoding.ASCII;
    if (PeekUInt32(reader) == 0xEFEFEFEF) {
      reader.BaseStream.Seek(4, SeekOrigin.Current);
      length -= 4;
      encoding = Encoding.Unicode;
    }
    return encoding.GetString(reader.ReadBytes(length));
  }

  public static Vector3 ReadVector3(BinaryReader reader) =>
    new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

  public static Matrix4x4 ReadMatrix44(BinaryReader reader) {
    Span<float> m = stackalloc float[16];
    for (var i = 0; i < 16; i++) m[i] = reader.ReadSingle();
    return new Matrix4x4(
      m[0], m[1], m[2], m[3],
      m[4], m[5], m[6], m[7],
      m[8], m[9], m[10], m[11],
      m[12], m[13], m[14], m[15]
    );
  }

  public static uint PeekUInt32(BinaryReader reader) {
    var value = reader.ReadUInt32();
    reader.BaseStream.Seek(-4, SeekOrigin.Current);
    return value;
  }
}

/// <summary>Thrown for DAT parsing or field-access failures.</summary>
public sealed class DatException(string message) : Exception(message);

/// <summary>
/// Represents a loaded RCT3 non-OVL DAT file.
/// </summary>
/// <remarks>
/// This is the container format used by saved parks
/// (<c>Documents\RCT3\Parks\*.dat</c>), coaster track designs (<c>*.trk</c>), and firework files
/// (<c>*.fwd</c>, <c>*.frw</c>).
/// </remarks>
public sealed class Dat {
  public IReadOnlyList<DatEntry> Entries { get; }

  private Dat(IReadOnlyList<DatEntry> entries) => Entries = entries;

  /// <summary>Loads and fully decodes a DAT file's struct table and entry list.</summary>
  public static Dat Load(string path) {
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.ASCII);
    return Read(reader);
  }

  /// <summary>
  /// An extended header is present whenever the first <c>u32</c> in the file is zero; it's
  /// followed by 8 reserved bytes and a version byte selecting a fixed struct-table offset
  /// (<c>0x1A</c> → 0x40, <c>0x2A</c> → 0x50). Files without this marker have no header at all -
  /// the struct table starts at byte 0. Both cases are still unverified against a real saved-park
  /// <c>.dat</c> (see the terrain plan's Gaps and Risks); this ports the reference decoder as-is.
  /// </summary>
  internal static Dat Read(BinaryReader reader) {
    if (DatReader.PeekUInt32(reader) == 0) {
      reader.BaseStream.Seek(8, SeekOrigin.Current);
      var version = reader.ReadByte();
      reader.BaseStream.Seek(version switch {
        0x1A => 0x40,
        0x2A => 0x50,
        _ => throw new DatException($"Unsupported DAT header version 0x{version:X2}"),
      }, SeekOrigin.Begin);
    }

    var structCount = reader.ReadUInt32();
    var structs = new List<DatStructDefinition>((int)structCount);
    for (var i = 0; i < structCount; i++) structs.Add(DatStructDefinition.ReadDefinition(reader));

    var entryCount = reader.ReadUInt32();
    var entries = new List<DatEntry>((int)entryCount);
    for (var i = 0; i < entryCount; i++) {
      var structIndex = (int)reader.ReadUInt32();
      var id = reader.ReadUInt64();
      entries.Add(structs[structIndex].ReadEntry(reader, id));
    }

    return new Dat(entries);
  }

  public IEnumerable<DatEntry> ByName(string name) => Entries.Where(e => e.Name == name);
  public DatEntry FirstByName(string name) =>
    Entries.FirstOrDefault(e => e.Name == name) ?? throw new DatException($"No entry named '{name}'");
  public DatEntry ById(ulong id) =>
    Entries.FirstOrDefault(e => e.Id == id) ?? throw new DatException($"No entry with id {id}");
}

// Reads RCT3's non-OVL DAT container format (park saves, track designs, firework files, etc.).
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using System.Text;

namespace OpenCobra.Data;

/// <summary>Type tag for a DAT struct field, matching the on-disk field-kind name table.</summary>
public enum FieldKind {
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

/// <summary>Extension methods for working with <see cref="FieldKind"/>.</summary>
internal static class FieldKindExtensions {
  /// <summary>
  /// Maps an on-disk field-kind name to its <see cref="FieldKind"/>.
  /// </summary>
  /// <remarks>
  /// Names are matched case-sensitively as written in DAT field definitions (e.g. <c>GE_Terrain</c>, not
  /// <c>ge_terrain</c>) - the casing is inconsistent between kinds (mostly lowercase primitives,
  /// PascalCase container kinds), but that inconsistency is what's actually on disk.
  /// </remarks>
  public static FieldKind ToFieldKind(this string kindName) => kindName switch {
    "array" => FieldKind.Array,
    "list" => FieldKind.List,
    "bool" => FieldKind.Bool,
    "float32" => FieldKind.Float32,
    "int8" => FieldKind.Int8,
    "int16" => FieldKind.Int16,
    "int32" => FieldKind.Int32,
    "managedobjectptr" => FieldKind.ManagedObjectPtr,
    "matrix44" => FieldKind.Matrix44,
    "orientation" => FieldKind.Orientation,
    "reference" => FieldKind.Reference,
    "uint8" => FieldKind.UInt8,
    "uint16" => FieldKind.UInt16,
    "uint32" => FieldKind.UInt32,
    "vector3" => FieldKind.Vector3,
    "struct" => FieldKind.Struct,
    "string" => FieldKind.String,
    "graphedValue" => FieldKind.GraphedValue,
    "WaterManager" => FieldKind.WaterManager,
    "GE_Terrain" => FieldKind.GETerrain,
    "SkirtTrees" => FieldKind.SkirtTrees,
    "PathTileList" => FieldKind.PathTileList,
    "waypointlist" => FieldKind.WaypointList,
    "flexicachelist" => FieldKind.FlexiCacheList,
    "managedImage" => FieldKind.ManagedImage,
    "pathnodearray" => FieldKind.PathNodeArray,
    "resourcesymbol" => FieldKind.ResourceSymbol,
    "stringTable" => FieldKind.StringTable,
    "BlockingScenery" => FieldKind.BlockingScenery,
    _ => throw new DatException($"Unknown DAT field kind '{kindName}'"),
  };
}

/// <summary>A single decoded field value within a <see cref="StructEntry"/>.</summary>
public abstract record FieldValue(FieldKind Kind);

/// <summary>A <see cref="FieldKind.Bool"/> field value.</summary>
public sealed record BoolValue(bool Value) : FieldValue(FieldKind.Bool);
/// <summary>A <see cref="FieldKind.Int8"/> field value.</summary>
public sealed record Int8Value(sbyte Value) : FieldValue(FieldKind.Int8);
/// <summary>A <see cref="FieldKind.Int16"/> field value.</summary>
public sealed record Int16Value(short Value) : FieldValue(FieldKind.Int16);
/// <summary>A <see cref="FieldKind.Int32"/> field value.</summary>
public sealed record Int32Value(int Value) : FieldValue(FieldKind.Int32);
/// <summary>A <see cref="FieldKind.UInt8"/> field value.</summary>
public sealed record UInt8Value(byte Value) : FieldValue(FieldKind.UInt8);
/// <summary>A <see cref="FieldKind.UInt16"/> field value.</summary>
public sealed record UInt16Value(ushort Value) : FieldValue(FieldKind.UInt16);
/// <summary>A <see cref="FieldKind.UInt32"/> field value.</summary>
public sealed record UInt32Value(uint Value) : FieldValue(FieldKind.UInt32);
/// <summary>A <see cref="FieldKind.Float32"/> field value.</summary>
public sealed record Float32Value(float Value) : FieldValue(FieldKind.Float32);
/// <summary>A <see cref="FieldKind.Vector3"/> field value.</summary>
public sealed record Vector3Value(Vector3 Value) : FieldValue(FieldKind.Vector3);
/// <summary>A <see cref="FieldKind.Matrix44"/> field value.</summary>
public sealed record Matrix44Value(Matrix4x4 Value) : FieldValue(FieldKind.Matrix44);
/// <summary>A <see cref="FieldKind.Orientation"/> field value.</summary>
public sealed record OrientationValue(Vector3 Value) : FieldValue(FieldKind.Orientation);
/// <summary>A <see cref="FieldKind.ManagedObjectPtr"/> field value.</summary>
public sealed record ManagedObjectPtrValue(ulong Value) : FieldValue(FieldKind.ManagedObjectPtr);
/// <summary>A <see cref="FieldKind.Reference"/> field value.</summary>
public sealed record ReferenceValue(ulong Value) : FieldValue(FieldKind.Reference);
/// <summary>A <see cref="FieldKind.String"/> field value.</summary>
public sealed record StringValue(string Value) : FieldValue(FieldKind.String);

/// <summary>A fixed-size (<see cref="Length"/>) collection of <see cref="StructValue"/> elements.</summary>
public sealed record ArrayValue(int Size, int Length, IReadOnlyList<StructValue> Elements) : FieldValue(FieldKind.Array);

/// <summary>A variable-length collection of <see cref="StructValue"/> elements.</summary>
public sealed record ListValue(int Size, int Length, IReadOnlyList<StructValue> Elements) : FieldValue(FieldKind.List);

/// <summary>A nested struct value - either a named field's inline body, or an array/list element.</summary>
public sealed record StructValue(int Size, IReadOnlyList<StructEntry> Entries) : FieldValue(FieldKind.Struct) {
  /// <summary>Finds every field named <paramref name="name"/> among <see cref="Entries"/>.</summary>
  public IEnumerable<StructEntry> ByName(string name) => Entries.Where(e => e.Name == name);
  /// <summary>Finds the first field named <paramref name="name"/> among <see cref="Entries"/>.</summary>
  public StructEntry FirstByName(string name) =>
    Entries.FirstOrDefault(e => e.Name == name) ?? throw new DatException($"No field named '{name}'");
}

/// <summary>
/// A field body whose internal layout isn't decoded here - the raw bytes are preserved so a
/// caller (or a later library revision) can decode them without re-parsing the surrounding
/// struct/entry table. Covers <see cref="FieldKind.GraphedValue"/>, <see cref="FieldKind.WaterManager"/>,
/// <see cref="FieldKind.GETerrain"/>, <see cref="FieldKind.SkirtTrees"/>,
/// <see cref="FieldKind.PathTileList"/>, <see cref="FieldKind.WaypointList"/>,
/// <see cref="FieldKind.FlexiCacheList"/>, <see cref="FieldKind.ManagedImage"/>,
/// <see cref="FieldKind.PathNodeArray"/>, <see cref="FieldKind.ResourceSymbol"/>,
/// <see cref="FieldKind.StringTable"/>, and <see cref="FieldKind.BlockingScenery"/>.
/// </summary>
public sealed record OpaqueValue(FieldKind Kind, int Size, byte[] Data) : FieldValue(Kind);

/// <summary>A named field value within a <see cref="StructValue"/> or top-level <see cref="Entry"/>.</summary>
public sealed record StructEntry(string Name, FieldValue Value) {
  /// <summary>The field kind <see cref="Value"/> was decoded as.</summary>
  public FieldKind Kind => Value.Kind;

  /// <summary>Casts <see cref="Value"/> to a <see cref="bool"/>, or throws if it isn't a <see cref="BoolValue"/>.</summary>
  public bool AsBool() => Value is BoolValue v ? v.Value : throw WrongType(FieldKind.Bool);
  /// <summary>Casts <see cref="Value"/> to a <see cref="byte"/>, or throws if it isn't a <see cref="UInt8Value"/>.</summary>
  public byte AsUInt8() => Value is UInt8Value v ? v.Value : throw WrongType(FieldKind.UInt8);
  /// <summary>Casts <see cref="Value"/> to an <see cref="int"/>, or throws if it isn't an <see cref="Int32Value"/>.</summary>
  public int AsInt32() => Value is Int32Value v ? v.Value : throw WrongType(FieldKind.Int32);
  /// <summary>Casts <see cref="Value"/> to a <see cref="StructValue"/>, or throws if it isn't one.</summary>
  public StructValue AsStruct() => Value as StructValue ?? throw WrongType(FieldKind.Struct);
  /// <summary>Casts <see cref="Value"/> to an <see cref="ArrayValue"/>, or throws if it isn't one.</summary>
  public ArrayValue AsArray() => Value as ArrayValue ?? throw WrongType(FieldKind.Array);
  /// <summary>Casts <see cref="Value"/> to a <see cref="string"/>, or throws if it isn't a <see cref="StringValue"/>.</summary>
  public string AsString() => Value is StringValue v ? v.Value : throw WrongType(FieldKind.String);
  /// <summary>Casts <see cref="Value"/> to its <see cref="ManagedObjectPtrValue"/> pointer, or throws if it isn't one.</summary>
  public ulong AsPtr() => Value is ManagedObjectPtrValue v ? v.Value : throw WrongType(FieldKind.ManagedObjectPtr);
  /// <summary>Casts <see cref="Value"/> to its <see cref="ReferenceValue"/> id, or throws if it isn't one.</summary>
  public ulong AsRef() => Value is ReferenceValue v ? v.Value : throw WrongType(FieldKind.Reference);
  /// <summary>Casts <see cref="Value"/> to an <see cref="OpaqueValue"/>, or throws if it isn't one.</summary>
  public OpaqueValue AsOpaque() => Value as OpaqueValue ?? throw new DatException($"Field '{Name}' is {Value.Kind}, expected an opaque field");

  private DatException WrongType(FieldKind expected) =>
    new($"Field '{Name}' is {Value.Kind}, expected {expected}");
}

/// <summary>A named, decoded record within a loaded <see cref="Dat"/> file.</summary>
public sealed record Entry(ulong Id, string Name, IReadOnlyList<StructEntry> Values) {
  /// <summary>Finds every field named <paramref name="name"/> among <see cref="Values"/>.</summary>
  public IEnumerable<StructEntry> ByName(string name) => Values.Where(v => v.Name == name);
  /// <summary>Finds the first field named <paramref name="name"/> among <see cref="Values"/>.</summary>
  public StructEntry FirstByName(string name) =>
    Values.FirstOrDefault(v => v.Name == name) ?? throw new DatException($"No field named '{name}'");
}

/// <summary>Definition of one field within a <see cref="StructDefinition"/> or a container field's element type.</summary>
internal sealed record FieldDefinition(string Name, FieldKind Kind, int Size, IReadOnlyList<FieldDefinition> Fields) {
  /// <summary>Reads one field definition (name, kind, size, nested field definitions) from the struct table.</summary>
  public static FieldDefinition ReadDefinition(BinaryReader reader) {
    var name = DatReader.ReadPascalString(reader);
    var kind = DatReader.ReadPascalString(reader).ToFieldKind();
    var size = (int)reader.ReadUInt32();
    var fieldCount = reader.ReadUInt32();
    var fields = new List<FieldDefinition>((int)fieldCount);
    for (var i = 0; i < fieldCount; i++) fields.Add(ReadDefinition(reader));
    return new FieldDefinition(name, kind, size, fields);
  }

  /// <summary>Reads this field's value from an entry, per this definition's <see cref="Kind"/>.</summary>
  public StructEntry ReadEntry(BinaryReader reader) => new(Name, ReadValue(reader));

  private FieldValue ReadValue(BinaryReader reader) => Kind switch {
    FieldKind.Bool => new BoolValue(reader.ReadBoolean()),
    FieldKind.Int8 => new Int8Value(reader.ReadSByte()),
    FieldKind.Int16 => new Int16Value(reader.ReadInt16()),
    FieldKind.Int32 => new Int32Value(reader.ReadInt32()),
    FieldKind.UInt8 => new UInt8Value(reader.ReadByte()),
    FieldKind.UInt16 => new UInt16Value(reader.ReadUInt16()),
    FieldKind.UInt32 => new UInt32Value(reader.ReadUInt32()),
    FieldKind.Float32 => new Float32Value(reader.ReadSingle()),
    FieldKind.Vector3 => new Vector3Value(DatReader.ReadVector3(reader)),
    FieldKind.Matrix44 => new Matrix44Value(DatReader.ReadMatrix44(reader)),
    FieldKind.Orientation => new OrientationValue(DatReader.ReadVector3(reader)),
    FieldKind.ManagedObjectPtr => new ManagedObjectPtrValue(reader.ReadUInt64()),
    FieldKind.Reference => new ReferenceValue(reader.ReadUInt64()),
    FieldKind.String => new StringValue(DatReader.ReadLengthPrefixedString(reader)),
    FieldKind.Array => ReadArrayOrList(reader, isArray: true),
    FieldKind.List => ReadArrayOrList(reader, isArray: false),
    FieldKind.Struct => ReadStruct(reader),
    _ => ReadOpaque(reader),
  };

  /// <summary>Reads an array or list container body: a size/length prefix, then that many elements.</summary>
  /// <remarks>
  /// Each element is shaped like <see cref="Fields"/> - unlike <see cref="ReadStruct"/>, elements
  /// have no per-element size prefix of their own.
  /// </remarks>
  private FieldValue ReadArrayOrList(BinaryReader reader, bool isArray) {
    var size = (int)reader.ReadUInt32();
    var length = (int)reader.ReadUInt32();
    var elements = new List<StructValue>(length);
    for (var i = 0; i < length; i++) {
      var entries = Fields.Select(field => field.ReadEntry(reader)).ToList();
      elements.Add(new StructValue(0, entries));
    }
    return isArray
      ? new ArrayValue(size, length, elements)
      : new ListValue(size, length, elements);
  }

  private FieldValue ReadStruct(BinaryReader reader) {
    var size = Size == 0 ? (int)reader.ReadUInt32() : Size;
    var entries = Fields.Select(field => field.ReadEntry(reader)).ToList();
    return new StructValue(size, entries);
  }

  /// <summary>
  /// Reads (and preserves, rather than discards) the raw body of a field kind this library
  /// doesn't decode - see <see cref="OpaqueValue"/>.
  /// </summary>
  private FieldValue ReadOpaque(BinaryReader reader) {
    var size = Size == 0 ? (int)reader.ReadUInt32() : Size;
    return new OpaqueValue(Kind, size, reader.ReadBytes(size));
  }
}

/// <summary>Definition of one named struct (a DAT "class") in a file's struct table.</summary>
internal sealed record StructDefinition(string Name, IReadOnlyList<FieldDefinition> Fields) {
  /// <summary>Reads one struct definition (a DAT "class": name plus field definitions) from the struct table.</summary>
  public static StructDefinition ReadDefinition(BinaryReader reader) {
    var name = DatReader.ReadPascalString(reader);
    var fieldCount = reader.ReadUInt32();
    var fields = new List<FieldDefinition>((int)fieldCount);
    for (var i = 0; i < fieldCount; i++) fields.Add(FieldDefinition.ReadDefinition(reader));
    return new StructDefinition(name, fields);
  }

  /// <summary>Reads one top-level entry's field values, per this struct's <see cref="Fields"/>.</summary>
  public Entry ReadEntry(BinaryReader reader, ulong id) =>
    new(id, Name, Fields.Select(field => field.ReadEntry(reader)).ToList());
}

/// <summary>Low-level binary readers shared by the DAT struct-table and entry-value decoders.</summary>
internal static class DatReader {
  /// <summary>Reads a Pascal-style length-prefixed ASCII string: a little-endian <c>u16</c> length, then that many ASCII bytes.</summary>
  public static string ReadPascalString(BinaryReader reader) =>
    Encoding.ASCII.GetString(reader.ReadBytes(reader.ReadUInt16()));

  /// <summary>
  /// Reads a <see cref="FieldKind.String"/> field value: a little-endian <c>u32</c> byte
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

  /// <summary>Reads three consecutive <see cref="float"/>s as a <see cref="Vector3"/>.</summary>
  public static Vector3 ReadVector3(BinaryReader reader) =>
    new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

  /// <summary>Reads sixteen consecutive <see cref="float"/>s as a row-major <see cref="Matrix4x4"/>.</summary>
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

  /// <summary>Reads a <see cref="uint"/> without advancing the stream position.</summary>
  public static uint PeekUInt32(BinaryReader reader) {
    var value = reader.ReadUInt32();
    reader.BaseStream.Seek(-4, SeekOrigin.Current);
    return value;
  }
}

/// <summary>Thrown for DAT parsing or field-access failures.</summary>
public sealed class DatException(string message) : Exception(message);

/// <summary>
/// Represents a loaded RCT3 non-OVL DAT file - the container format used by saved parks
/// (<c>Documents\RCT3\Parks\*.dat</c>), coaster track designs (<c>*.trk</c>), and firework files
/// (<c>*.fwd</c>, <c>*.frw</c>). Distinct from the OVL archive format read by
/// <see cref="OpenCobra.OVL.Ovl"/>. See <c>OpenCobra/Data/README.md</c> for a format reference.
/// </summary>
public sealed class Dat {
  /// <summary>Every top-level entry decoded from the file, in on-disk order.</summary>
  public IReadOnlyList<Entry> Entries { get; }

  private Dat(IReadOnlyList<Entry> entries) => Entries = entries;

  /// <summary>Loads and fully decodes a DAT file's struct table and entry list.</summary>
  public static Dat Load(string path) {
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    using var reader = new BinaryReader(stream, Encoding.ASCII);
    return Read(reader);
  }

  /// <summary>Reads a DAT file's header, struct table, and entry list from an open stream.</summary>
  /// <remarks>
  /// An extended header is present whenever the first <c>u32</c> in the file is zero; it's
  /// followed by 8 reserved bytes and a version byte selecting a fixed struct-table offset
  /// (<c>0x1A</c> → 0x40, <c>0x2A</c> → 0x50). Files without this marker have no header at all -
  /// the struct table starts at byte 0.
  /// </remarks>
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
    var structs = new List<StructDefinition>((int)structCount);
    for (var i = 0; i < structCount; i++) structs.Add(StructDefinition.ReadDefinition(reader));

    var entryCount = reader.ReadUInt32();
    var entries = new List<Entry>((int)entryCount);
    for (var i = 0; i < entryCount; i++) {
      var structIndex = (int)reader.ReadUInt32();
      var id = reader.ReadUInt64();
      entries.Add(structs[structIndex].ReadEntry(reader, id));
    }

    return new Dat(entries);
  }

  /// <summary>Finds every top-level entry named <paramref name="name"/>.</summary>
  public IEnumerable<Entry> ByName(string name) => Entries.Where(e => e.Name == name);
  /// <summary>Finds the first top-level entry named <paramref name="name"/>.</summary>
  public Entry FirstByName(string name) =>
    Entries.FirstOrDefault(e => e.Name == name) ?? throw new DatException($"No entry named '{name}'");
  /// <summary>Finds the top-level entry with the given <paramref name="id"/>.</summary>
  public Entry ById(ulong id) =>
    Entries.FirstOrDefault(e => e.Id == id) ?? throw new DatException($"No entry with id {id}");
}

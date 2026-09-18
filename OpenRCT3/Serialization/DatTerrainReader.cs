// DatTerrainReader
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace OpenRCT3.Serialization;

/// <summary>
/// Reads the first EngineTerrain/GE_Terrain field and first WaterManager payload from an RCT3 DAT
/// file while consuming every declared value.
/// </summary>
internal static class DatTerrainReader {
  private const string TargetFieldName = "EngineTerrain";
  private const int ExtendedHeaderVersion1Offset = 0x40;
  private const int ExtendedHeaderVersion2Offset = 0x50;
  private const int MaxStructureCount = 4_096;
  private const int MaxSchemaFieldCount = 65_536;
  private const int MaxSchemaDepth = 32;
  private const int MaxDefinitionStringBytes = 4_096;
  private const int MaxEntryCount = 100_000;
  private const int MaxCollectionLength = 100_000;
  private const int MaxTotalCollectionElements = 1_000_000;
  private const int MaxValueReadCount = 8_000_000;
  private const int MaxStringBytes = 16 * 1024 * 1024;
  private const int MaxPayloadBytes = 64 * 1024 * 1024;
  private const int TerrainHeaderBytes = 18;
  private const int TerrainTailBytes = 8;
  private const int WaterManagerHeaderBytes = 6;
  private const int WaterPoolHeaderBytes = 8;
  private const int WaterRecordBytes = 4;
  private const int MaxWaterRecordCount = 2 * byte.MaxValue * byte.MaxValue;

  public static DatTerrainData Read(string path) {
    if (string.IsNullOrWhiteSpace(path))
      throw new ArgumentException("A DAT file path is required.", nameof(path));

    using var stream = File.OpenRead(path);
    return Read(stream);
  }

  public static DatTerrainData Read(Stream stream) {
    ArgumentNullException.ThrowIfNull(stream);
    if (!stream.CanRead) throw new ArgumentException("The DAT stream must be readable.", nameof(stream));

    var reader = new DatBinaryReader(stream);
    var structureCount = ReadStructureCount(reader);
    var structures = ReadStructureDefinitions(reader, structureCount);
    var entryCount = ReadBoundedCount(reader.ReadUInt32(), MaxEntryCount, "entry count");
    var state = new ValueReadState();

    for (var entryIndex = 0; entryIndex < entryCount; entryIndex++) {
      var structureIndex = reader.ReadUInt32();
      if (structureIndex >= Convert.ToUInt32(structures.Length))
        throw new InvalidDataException($"DAT entry {entryIndex} has an invalid structure index.");

      reader.ReadUInt64();
      var structure = structures[Convert.ToInt32(structureIndex)];
      foreach (var field in structure.Fields)
        ReadFieldValue(reader, field, state);
    }

    var terrain = state.Terrain
      ?? throw new InvalidDataException(
        "The DAT file does not contain an EngineTerrain/GE_Terrain field.");
    return AttachWaterManager(terrain, state.WaterManager);
  }

  private static int ReadStructureCount(DatBinaryReader reader) {
    var firstValue = reader.ReadUInt32();
    if (firstValue != 0)
      return ReadBoundedCount(firstValue, MaxStructureCount, "structure count");

    reader.ReadUInt32();
    var version = reader.ReadByte();
    var definitionOffset = version switch {
      0x1A => ExtendedHeaderVersion1Offset,
      0x2A => ExtendedHeaderVersion2Offset,
      _ => throw new InvalidDataException($"Unsupported DAT extended-header version 0x{version:X2}."),
    };
    if (reader.Position > definitionOffset)
      throw new InvalidDataException("The DAT extended header exceeds its definition offset.");

    reader.Skip(Convert.ToInt32(definitionOffset - reader.Position));
    return ReadBoundedCount(reader.ReadUInt32(), MaxStructureCount, "structure count");
  }

  private static DataStructure[] ReadStructureDefinitions(DatBinaryReader reader, int structureCount) {
    var structures = new DataStructure[structureCount];
    var state = new SchemaReadState();
    for (var index = 0; index < structureCount; index++)
      structures[index] = ReadStructureDefinition(reader, state);
    return structures;
  }

  private static DataStructure ReadStructureDefinition(
    DatBinaryReader reader,
    SchemaReadState state) {
    var name = reader.ReadAscii16("structure name");
    var fieldCount = ReadSchemaChildCount(reader.ReadUInt32(), state);
    var fields = new FieldDefinition[fieldCount];
    for (var index = 0; index < fieldCount; index++)
      fields[index] = ReadFieldDefinition(reader, state, depth: 1);
    return new DataStructure(name, fields);
  }

  private static FieldDefinition ReadFieldDefinition(
    DatBinaryReader reader,
    SchemaReadState state,
    int depth) {
    if (depth > MaxSchemaDepth)
      throw new InvalidDataException("DAT schema nesting exceeds the supported depth.");

    state.AddField();
    var name = reader.ReadAscii16("field name");
    var kindName = reader.ReadAscii16("field kind");
    var kind = ParseFieldKind(kindName);
    var fixedSize = reader.ReadUInt32();
    var childCount = ReadSchemaChildCount(reader.ReadUInt32(), state);
    var children = new FieldDefinition[childCount];
    for (var index = 0; index < childCount; index++)
      children[index] = ReadFieldDefinition(reader, state, depth + 1);
    return new FieldDefinition(name, kind, fixedSize, children);
  }

  private static int ReadSchemaChildCount(uint value, SchemaReadState state) {
    var remaining = MaxSchemaFieldCount - state.FieldCount;
    return ReadBoundedCount(value, remaining, "schema field count");
  }

  private static FieldKind ParseFieldKind(string kind) => kind switch {
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
    _ => throw new InvalidDataException($"Unsupported DAT field kind '{kind}'."),
  };

  private static void ReadFieldValue(
    DatBinaryReader reader,
    FieldDefinition field,
    ValueReadState state) {
    state.AddValue();

    switch (field.Kind) {
      case FieldKind.Bool:
      case FieldKind.Int8:
      case FieldKind.UInt8:
        reader.Skip(1);
        return;
      case FieldKind.Int16:
      case FieldKind.UInt16:
        reader.Skip(2);
        return;
      case FieldKind.Int32:
      case FieldKind.UInt32:
      case FieldKind.Float32:
        reader.Skip(4);
        return;
      case FieldKind.ManagedObjectPtr:
      case FieldKind.Reference:
        reader.Skip(8);
        return;
      case FieldKind.Vector3:
      case FieldKind.Orientation:
        reader.Skip(12);
        return;
      case FieldKind.Matrix44:
        reader.Skip(64);
        return;
      case FieldKind.String:
        reader.Skip(ReadBoundedSize(reader.ReadUInt32(), MaxStringBytes, "string length"));
        return;
      case FieldKind.Array:
      case FieldKind.List:
        ReadCollection(reader, field, state);
        return;
      case FieldKind.Struct:
        // dat.rs records this size but walks the child definitions directly. Treat it as bounded
        // metadata rather than inventing an unproven container boundary.
        ReadSizedValueLength(reader, field.FixedSize, "structure payload");
        ReadChildValues(reader, field, state);
        return;
      case FieldKind.GraphedValue:
      case FieldKind.SkirtTrees:
      case FieldKind.PathTileList:
      case FieldKind.WaypointList:
      case FieldKind.FlexiCacheList:
      case FieldKind.ManagedImage:
      case FieldKind.PathNodeArray:
      case FieldKind.ResourceSymbol:
      case FieldKind.StringTable:
      case FieldKind.BlockingScenery:
        reader.Skip(ReadSizedValueLength(reader, field.FixedSize, "custom payload"));
        return;
      case FieldKind.WaterManager:
        var waterPayloadSize = ReadSizedValueLength(
          reader,
          field.FixedSize,
          "WaterManager payload");
        var waterManager = ReadWaterManager(reader, waterPayloadSize, state);
        state.CaptureWaterManager(waterManager);
        return;
      case FieldKind.GETerrain:
        var payloadSize = ReadSizedValueLength(reader, field.FixedSize, "GE_Terrain payload");
        if (field.Name == TargetFieldName) {
          var terrain = ReadTerrain(reader, payloadSize);
          state.CaptureTerrain(terrain);
          return;
        }
        reader.Skip(payloadSize);
        return;
      default:
        throw new InvalidDataException($"Unsupported DAT field kind '{field.Kind}'.");
    }
  }

  private static void ReadCollection(
    DatBinaryReader reader,
    FieldDefinition field,
    ValueReadState state) {
    // Like struct sizes, array/list sizes are metadata in dat.rs; element count and child schema
    // determine how many bytes follow.
    ReadBoundedSize(reader.ReadUInt32(), MaxPayloadBytes, "collection payload");
    var length = ReadBoundedCount(reader.ReadUInt32(), MaxCollectionLength, "collection length");
    state.AddCollectionElements(length);

    for (var elementIndex = 0; elementIndex < length; elementIndex++)
      ReadChildValues(reader, field, state);
  }

  private static void ReadChildValues(
    DatBinaryReader reader,
    FieldDefinition field,
    ValueReadState state) {
    foreach (var child in field.Children)
      ReadFieldValue(reader, child, state);
  }

  private static int ReadSizedValueLength(
    DatBinaryReader reader,
    uint fixedSize,
    string description) {
    var size = fixedSize == 0 ? reader.ReadUInt32() : fixedSize;
    return ReadBoundedSize(size, MaxPayloadBytes, description);
  }

  private static DatWaterManagerData ReadWaterManager(
    DatBinaryReader reader,
    int payloadSize,
    ValueReadState state) {
    var payloadStart = reader.Position;
    EnsurePayloadBytesRemaining(
      reader,
      payloadStart,
      payloadSize,
      WaterManagerHeaderBytes,
      "WaterManager header");

    var width = Convert.ToInt32(reader.ReadByte());
    var height = Convert.ToInt32(reader.ReadByte());
    if (width == 0 || height == 0)
      throw new InvalidDataException("WaterManager dimensions must be positive.");

    var poolCount = ReadBoundedCount(
      reader.ReadUInt32(),
      MaxCollectionLength,
      "WaterManager pool count");
    state.AddCollectionElements(poolCount);

    var minimumPayloadSize = Convert.ToInt64(WaterManagerHeaderBytes)
      + (Convert.ToInt64(poolCount) * WaterPoolHeaderBytes);
    if (minimumPayloadSize > payloadSize)
      throw new InvalidDataException("WaterManager pool count exceeds its payload size.");

    var pools = new DatWaterPoolData[poolCount];
    var maximumRecordCount = checked(width * height * 2);
    for (var poolIndex = 0; poolIndex < poolCount; poolIndex++) {
      EnsurePayloadBytesRemaining(
        reader,
        payloadStart,
        payloadSize,
        WaterPoolHeaderBytes,
        $"WaterManager pool {poolIndex} header");

      var poolHeight = reader.ReadSingle();
      if (!float.IsFinite(poolHeight))
        throw new InvalidDataException(
          $"WaterManager pool {poolIndex} contains a non-finite height.");

      var recordCount = ReadBoundedCount(
        reader.ReadUInt32(),
        MaxWaterRecordCount,
        $"WaterManager pool {poolIndex} record count");
      if (recordCount > maximumRecordCount)
        throw new InvalidDataException(
          $"WaterManager pool {poolIndex} record count exceeds its grid bounds.");
      state.AddCollectionElements(recordCount);

      var recordPayloadSize = checked(recordCount * WaterRecordBytes);
      EnsurePayloadBytesRemaining(
        reader,
        payloadStart,
        payloadSize,
        recordPayloadSize,
        $"WaterManager pool {poolIndex} records");

      var records = new DatWaterRecord[recordCount];
      HashSet<int>? occupiedTriangles = null;
      for (var recordIndex = 0; recordIndex < recordCount; recordIndex++) {
        var x = reader.ReadByte();
        var y = reader.ReadByte();
        var triangle = reader.ReadByte();
        var vertexMask = reader.ReadByte();
        if (x >= width || y >= height)
          throw new InvalidDataException(
            $"WaterManager pool {poolIndex} record {recordIndex} is outside the manager bounds.");
        if (triangle > 1)
          throw new InvalidDataException(
            $"WaterManager pool {poolIndex} record {recordIndex} has an invalid triangle.");
        if (vertexMask is < 1 or > 7)
          throw new InvalidDataException(
            $"WaterManager pool {poolIndex} record {recordIndex} has an invalid vertex mask.");

        var triangleAddress = ((((Convert.ToInt32(y) * width) + x) * 2) + triangle);
        occupiedTriangles ??= new HashSet<int>();
        if (!occupiedTriangles.Add(triangleAddress))
          throw new InvalidDataException(
            $"WaterManager pool {poolIndex} contains a duplicate terrain triangle.");
        records[recordIndex] = new DatWaterRecord(x, y, triangle, vertexMask);
      }

      pools[poolIndex] = new DatWaterPoolData(poolHeight, records);
    }

    var consumed = reader.Position - payloadStart;
    if (consumed != payloadSize)
      throw new InvalidDataException(
        "WaterManager payload size does not exactly match its decoded contents.");
    return new DatWaterManagerData(width, height, pools);
  }

  private static void EnsurePayloadBytesRemaining(
    DatBinaryReader reader,
    long payloadStart,
    int payloadSize,
    int requiredBytes,
    string description) {
    var consumed = reader.Position - payloadStart;
    if (consumed < 0 || requiredBytes < 0 || consumed + requiredBytes > payloadSize)
      throw new InvalidDataException($"The {description} exceeds its declared payload size.");
  }

  private static DatTerrainData AttachWaterManager(
    DatTerrainData terrain,
    DatWaterManagerData? waterManager) {
    if (waterManager == null) return terrain;
    if (waterManager.Width != terrain.Width || waterManager.Height != terrain.Height)
      throw new InvalidDataException(
        "WaterManager dimensions do not match the decoded GE_Terrain dimensions.");

    var cells = new DatTerrainCell[terrain.Cells.Count];
    for (var index = 0; index < cells.Length; index++)
      cells[index] = terrain.Cells[index];
    return new DatTerrainData(
      terrain.Width,
      terrain.Height,
      terrain.OriginX,
      terrain.OriginY,
      terrain.TileSizeX,
      terrain.TileSizeY,
      cells,
      waterManager);
  }

  private static DatTerrainData ReadTerrain(DatBinaryReader reader, int payloadSize) {
    if (payloadSize < TerrainHeaderBytes)
      throw new InvalidDataException("The GE_Terrain payload is too small for its header.");

    var width = Convert.ToInt32(reader.ReadByte());
    var height = Convert.ToInt32(reader.ReadByte());
    if (width == 0 || height == 0)
      throw new InvalidDataException("GE_Terrain dimensions must be positive.");

    var originX = reader.ReadSingle();
    var originY = reader.ReadSingle();
    var tileSizeX = reader.ReadSingle();
    var tileSizeY = reader.ReadSingle();
    if (!float.IsFinite(originX) || !float.IsFinite(originY)
      || !float.IsFinite(tileSizeX) || !float.IsFinite(tileSizeY))
      throw new InvalidDataException("GE_Terrain metadata contains a non-finite value.");
    if (tileSizeX <= 0 || tileSizeY <= 0)
      throw new InvalidDataException("GE_Terrain tile sizes must be positive.");

    var cellCount = checked(width * height);
    var layout = DetermineTerrainLayout(payloadSize, cellCount);
    var cells = new DatTerrainCell[cellCount];
    for (var index = 0; index < cellCount; index++) {
      // The saved-field compass labels are camera-relative. Preserve the positional order here: the
      // real-map shared-edge continuity check establishes it as the simulation's world-space order.
      var southWest = reader.ReadSingle();
      var southEast = reader.ReadSingle();
      var northWest = reader.ReadSingle();
      var northEast = reader.ReadSingle();
      if (!float.IsFinite(southWest) || !float.IsFinite(southEast)
        || !float.IsFinite(northWest) || !float.IsFinite(northEast))
        throw new InvalidDataException($"GE_Terrain cell {index} contains a non-finite height.");

      var surfaceIndex = reader.ReadByte();
      var cliffIndex = reader.ReadByte();
      reader.Skip(layout.RecordSize - 18);
      cells[index] = new DatTerrainCell(
        southWest,
        southEast,
        northWest,
        northEast,
        surfaceIndex,
        cliffIndex);
    }

    reader.Skip(layout.TailSize);
    return new DatTerrainData(
      width,
      height,
      originX,
      originY,
      tileSizeX,
      tileSizeY,
      cells);
  }

  private static TerrainLayout DetermineTerrainLayout(int payloadSize, int cellCount) {
    var matches24 = MatchesTerrainLayout(payloadSize, cellCount, recordSize: 24, tailSize: 0);
    var matches20 = MatchesTerrainLayout(payloadSize, cellCount, recordSize: 20, tailSize: 0);
    var matches24WithTail = MatchesTerrainLayout(
      payloadSize,
      cellCount,
      recordSize: 24,
      tailSize: TerrainTailBytes);
    var matches20WithTail = MatchesTerrainLayout(
      payloadSize,
      cellCount,
      recordSize: 20,
      tailSize: TerrainTailBytes);
    var matchCount = 0;
    if (matches24) matchCount++;
    if (matches20) matchCount++;
    if (matches24WithTail) matchCount++;
    if (matches20WithTail) matchCount++;
    if (matchCount > 1)
      throw new InvalidDataException("GE_Terrain payload has an ambiguous cell layout.");

    if (matches24)
      return new TerrainLayout(recordSize: 24, tailSize: 0);
    if (matches20)
      return new TerrainLayout(recordSize: 20, tailSize: 0);
    if (matches24WithTail)
      return new TerrainLayout(recordSize: 24, tailSize: TerrainTailBytes);
    if (matches20WithTail)
      return new TerrainLayout(recordSize: 20, tailSize: TerrainTailBytes);

    throw new InvalidDataException(
      "GE_Terrain payload size does not match a supported 20-byte or 24-byte cell layout.");
  }

  private static bool MatchesTerrainLayout(
    int payloadSize,
    int cellCount,
    int recordSize,
    int tailSize) {
    var expected = Convert.ToInt64(TerrainHeaderBytes)
      + (Convert.ToInt64(cellCount) * recordSize)
      + tailSize;
    return expected == payloadSize;
  }

  private static int ReadBoundedCount(uint value, int maximum, string description) {
    if (maximum < 0 || value > Convert.ToUInt32(maximum))
      throw new InvalidDataException($"DAT {description} exceeds the supported limit.");
    return Convert.ToInt32(value);
  }

  private static int ReadBoundedSize(uint value, int maximum, string description) {
    if (value > Convert.ToUInt32(maximum))
      throw new InvalidDataException($"DAT {description} exceeds the supported limit.");
    return Convert.ToInt32(value);
  }

  private enum FieldKind {
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

  private sealed class DataStructure {
    public string Name { get; }
    public FieldDefinition[] Fields { get; }

    public DataStructure(string name, FieldDefinition[] fields) {
      Name = name;
      Fields = fields;
    }
  }

  private sealed class FieldDefinition {
    public string Name { get; }
    public FieldKind Kind { get; }
    public uint FixedSize { get; }
    public FieldDefinition[] Children { get; }

    public FieldDefinition(
      string name,
      FieldKind kind,
      uint fixedSize,
      FieldDefinition[] children) {
      Name = name;
      Kind = kind;
      FixedSize = fixedSize;
      Children = children;
    }
  }

  private sealed class SchemaReadState {
    public int FieldCount { get; private set; }

    public void AddField() {
      if (FieldCount >= MaxSchemaFieldCount)
        throw new InvalidDataException("DAT schema field count exceeds the supported limit.");
      FieldCount++;
    }
  }

  private sealed class ValueReadState {
    private int _collectionElementCount;
    private int _valueReadCount;

    public DatTerrainData? Terrain { get; private set; }
    public DatWaterManagerData? WaterManager { get; private set; }

    public void AddCollectionElements(int count) {
      if (count > MaxTotalCollectionElements - _collectionElementCount)
        throw new InvalidDataException("DAT collection element count exceeds the supported limit.");
      _collectionElementCount += count;
    }

    public void AddValue() {
      if (_valueReadCount >= MaxValueReadCount)
        throw new InvalidDataException("DAT value count exceeds the supported limit.");
      _valueReadCount++;
    }

    public void CaptureTerrain(DatTerrainData terrain) {
      Terrain ??= terrain;
    }

    public void CaptureWaterManager(DatWaterManagerData waterManager) {
      WaterManager ??= waterManager;
    }
  }

  private readonly struct TerrainLayout {
    public int RecordSize { get; }
    public int TailSize { get; }

    public TerrainLayout(int recordSize, int tailSize) {
      RecordSize = recordSize;
      TailSize = tailSize;
    }
  }

  private sealed class DatBinaryReader {
    private readonly Stream _stream;

    public long Position { get; private set; }

    public DatBinaryReader(Stream stream) {
      _stream = stream;
    }

    public byte ReadByte() {
      Span<byte> bytes = stackalloc byte[1];
      ReadExact(bytes);
      return bytes[0];
    }

    public ushort ReadUInt16() {
      Span<byte> bytes = stackalloc byte[2];
      ReadExact(bytes);
      return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    }

    public uint ReadUInt32() {
      Span<byte> bytes = stackalloc byte[4];
      ReadExact(bytes);
      return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    public ulong ReadUInt64() {
      Span<byte> bytes = stackalloc byte[8];
      ReadExact(bytes);
      return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    public float ReadSingle() {
      Span<byte> bytes = stackalloc byte[4];
      ReadExact(bytes);
      return BinaryPrimitives.ReadSingleLittleEndian(bytes);
    }

    public string ReadAscii16(string description) {
      var length = ReadBoundedCount(
        ReadUInt16(),
        MaxDefinitionStringBytes,
        description);
      var bytes = new byte[length];
      ReadExact(bytes);
      foreach (var value in bytes) {
        if (value > 0x7F)
          throw new InvalidDataException($"DAT {description} is not ASCII.");
      }
      return Encoding.ASCII.GetString(bytes);
    }

    public void Skip(int count) {
      if (count < 0) throw new InvalidDataException("DAT skip length is invalid.");

      Span<byte> buffer = stackalloc byte[4_096];
      var remaining = count;
      while (remaining > 0) {
        var chunkLength = Math.Min(remaining, buffer.Length);
        ReadExact(buffer[..chunkLength]);
        remaining -= chunkLength;
      }
    }

    private void ReadExact(Span<byte> buffer) {
      var offset = 0;
      while (offset < buffer.Length) {
        var read = _stream.Read(buffer[offset..]);
        if (read == 0) throw new InvalidDataException("Unexpected end of DAT stream.");
        offset += read;
      }

      if (Position > long.MaxValue - buffer.Length)
        throw new InvalidDataException("DAT stream position overflowed.");
      Position += buffer.Length;
    }
  }
}

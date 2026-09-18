// DatTerrainReaderTests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Serialization;
using System.Text;

namespace OpenRCT3.Tests.Serialization;

[TestFixture]
public class DatTerrainReaderTests {
  private const int TerrainRecord20Bytes = 20;
  private const int TerrainRecord24Bytes = 24;

  [Test]
  public void Read_24ByteTerrain_DecodesMetadataAndRowMajorCells() {
    var cells = new[] {
      new CellSpec(-4.25f, 2.5f, 3.75f, 4.5f, 1, 2),
      new CellSpec(5.25f, 6.5f, 7.75f, 8.5f, 3, 4),
      new CellSpec(9.25f, 10.5f, 11.75f, 12.5f, 5, 6),
      new CellSpec(13.25f, 14.5f, 15.75f, 16.5f, 7, 8),
    };
    var payload = BuildTerrainPayload(
      width: 2,
      height: 2,
      TerrainRecord24Bytes,
      cells,
      originX: -128f,
      originY: 64f,
      tileSizeX: 4f,
      tileSizeY: 5f);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload));

    var terrain = DatTerrainReader.Read(stream);

    Assert.Multiple(new Action(() => {
      Assert.That(terrain.Width, Is.EqualTo(2));
      Assert.That(terrain.Height, Is.EqualTo(2));
      Assert.That(terrain.OriginX, Is.EqualTo(-128f));
      Assert.That(terrain.OriginY, Is.EqualTo(64f));
      Assert.That(terrain.TileSizeX, Is.EqualTo(4f));
      Assert.That(terrain.TileSizeY, Is.EqualTo(5f));
      Assert.That(terrain.Cells, Has.Count.EqualTo(4));
      Assert.That(terrain.Cells[0].SouthWestHeight, Is.EqualTo(-4.25f));
      Assert.That(terrain.Cells[0].SouthEastHeight, Is.EqualTo(2.5f));
      Assert.That(terrain.Cells[0].NorthWestHeight, Is.EqualTo(3.75f));
      Assert.That(terrain.Cells[0].NorthEastHeight, Is.EqualTo(4.5f));
      Assert.That(terrain.Cells[1].SouthWestHeight, Is.EqualTo(5.25f));
      Assert.That(terrain.Cells[2].SouthWestHeight, Is.EqualTo(9.25f));
      Assert.That(terrain.Cells[3].NorthEastHeight, Is.EqualTo(16.5f));
      Assert.That(terrain.Cells[3].SurfaceIndex, Is.EqualTo(7));
      Assert.That(terrain.Cells[3].CliffIndex, Is.EqualTo(8));
    }));
  }

  [Test]
  public void Read_20ByteTerrainWithTail_DecodesCell() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord20Bytes,
      [new CellSpec(-9f, -8f, -7f, -6f, 11, 12)],
      includeTail: true);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload));

    var terrain = DatTerrainReader.Read(stream);

    Assert.Multiple(new Action(() => {
      Assert.That(terrain.Cells[0].SouthWestHeight, Is.EqualTo(-9f));
      Assert.That(terrain.Cells[0].NorthEastHeight, Is.EqualTo(-6f));
      Assert.That(terrain.Cells[0].SurfaceIndex, Is.EqualTo(11));
      Assert.That(terrain.Cells[0].CliffIndex, Is.EqualTo(12));
    }));
  }

  [Test]
  public void Read_TwoCellAmbiguousLayout_ThrowsInvalidDataException() {
    var payload = BuildTerrainPayload(
      width: 2,
      height: 1,
      TerrainRecord20Bytes,
      [
        new CellSpec(1f, 2f, 3f, 4f, 5, 6),
        new CellSpec(7f, 8f, 9f, 10f, 11, 12),
      ],
      includeTail: true);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload));

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_FixedSizeTerrain_DoesNotReadSizePrefix() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    using var stream = BuildDat(
      [new FieldSpec("EngineTerrain", "GE_Terrain", Convert.ToUInt32(payload.Length))],
      writer => writer.Write(payload));

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.Cells[0].SouthEastHeight, Is.EqualTo(2f));
  }

  [Test]
  public void Read_NonTargetTerrainField_SkipsToEngineTerrain() {
    var ignoredPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(100f, 200f, 300f, 400f, 1, 2)]);
    var targetPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(10f, 20f, 30f, 40f, 3, 4)]);
    using var stream = BuildDat(
      [new FieldSpec("PreviewTerrain", "GE_Terrain"), TargetTerrainField()],
      writer => {
        WriteDynamicPayload(writer, ignoredPayload);
        WriteDynamicPayload(writer, targetPayload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.Cells[0].SouthWestHeight, Is.EqualTo(10f));
  }

  [TestCase(0x1A)]
  [TestCase(0x2A)]
  public void Read_ExtendedHeader_UsesVersionDefinitionOffset(int version) {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload),
      extendedVersion: Convert.ToByte(version));

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.Cells[0].NorthEastHeight, Is.EqualTo(4f));
  }

  [Test]
  public void Read_CollectionSizeMetadata_DoesNotOverrideElementSchema() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(10f, 20f, 30f, 40f, 2, 3)]);
    var arrayField = new FieldSpec(
      "Items",
      "array",
      children: [new FieldSpec("Value", "uint16")]);
    using var stream = BuildDat(
      [new FieldSpec("Enabled", "bool"), arrayField, TargetTerrainField()],
      writer => {
        writer.Write(true);
        writer.Write(1_234u);
        writer.Write(2u);
        writer.Write(Convert.ToUInt16(100));
        writer.Write(Convert.ToUInt16(200));
        WriteDynamicPayload(writer, payload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.Cells[0].NorthWestHeight, Is.EqualTo(30f));
  }

  [Test]
  public void Read_TerrainThenWaterManager_DecodesPoolsAndRecords() {
    var terrainPayload = BuildTerrainPayload(
      width: 3,
      height: 2,
      TerrainRecord24Bytes,
      [
        new CellSpec(1f, 2f, 3f, 4f, 5, 6),
        new CellSpec(2f, 3f, 4f, 5f, 5, 6),
        new CellSpec(3f, 4f, 5f, 6f, 5, 6),
        new CellSpec(4f, 5f, 6f, 7f, 5, 6),
        new CellSpec(5f, 6f, 7f, 8f, 5, 6),
        new CellSpec(6f, 7f, 8f, 9f, 5, 6),
      ]);
    var waterPayload = BuildWaterManagerPayload(
      width: 3,
      height: 2,
      [
        new WaterPoolSpec(
          12.5f,
          [
            new WaterRecordSpec(0, 0, 0, 7),
            new WaterRecordSpec(0, 0, 1, 3),
            new WaterRecordSpec(2, 1, 0, 5),
          ]),
        new WaterPoolSpec(-2.25f, []),
      ]);
    using var stream = BuildDat(
      [TargetTerrainField(), WaterManagerField()],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        WriteDynamicPayload(writer, waterPayload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.Multiple(new Action(() => {
      Assert.That(terrain.WaterManager, Is.Not.Null);
      Assert.That(terrain.WaterManager!.Width, Is.EqualTo(3));
      Assert.That(terrain.WaterManager.Height, Is.EqualTo(2));
      Assert.That(terrain.WaterManager.Pools, Has.Count.EqualTo(2));
      Assert.That(terrain.WaterManager.Pools[0].Height, Is.EqualTo(12.5f));
      Assert.That(terrain.WaterManager.Pools[0].Records, Has.Count.EqualTo(3));
      Assert.That(terrain.WaterManager.Pools[0].Records[1].X, Is.EqualTo(0));
      Assert.That(terrain.WaterManager.Pools[0].Records[1].Y, Is.EqualTo(0));
      Assert.That(terrain.WaterManager.Pools[0].Records[1].Triangle, Is.EqualTo(1));
      Assert.That(terrain.WaterManager.Pools[0].Records[1].VertexMask, Is.EqualTo(3));
      Assert.That(terrain.WaterManager.Pools[1].Height, Is.EqualTo(-2.25f));
      Assert.That(terrain.WaterManager.Pools[1].Records, Is.Empty);
    }));
  }

  [Test]
  public void Read_WaterRecordCountAtGridCapacity_IsAccepted() {
    var terrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    var waterPayload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [
        new WaterPoolSpec(
          4f,
          [new WaterRecordSpec(0, 0, 0, 7), new WaterRecordSpec(0, 0, 1, 7)]),
      ]);
    using var stream = BuildDat(
      [TargetTerrainField(), WaterManagerField()],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        WriteDynamicPayload(writer, waterPayload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.WaterManager!.Pools[0].Records, Has.Count.EqualTo(2));
  }

  [Test]
  public void Read_WaterRecordCountAboveGridCapacity_ThrowsInvalidDataException() {
    var payload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [
        new WaterPoolSpec(
          4f,
          [
            new WaterRecordSpec(0, 0, 0, 7),
            new WaterRecordSpec(0, 0, 1, 7),
            new WaterRecordSpec(0, 0, 1, 7),
          ]),
      ]);

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_DuplicateWaterManagers_KeepsFirstAndConsumesFollowingFields() {
    var terrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    var firstWater = BuildWaterManagerPayload(width: 1, height: 1, []);
    var secondWater = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [new WaterPoolSpec(4f, [new WaterRecordSpec(0, 0, 0, 7)])]);
    using var stream = BuildDat(
      [
        TargetTerrainField(),
        WaterManagerField(),
        WaterManagerField(),
        new FieldSpec("Sentinel", "uint32"),
      ],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        WriteDynamicPayload(writer, firstWater);
        WriteDynamicPayload(writer, secondWater);
        writer.Write(0x01020304u);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.WaterManager!.Pools, Is.Empty);
    Assert.That(stream.Position, Is.EqualTo(stream.Length));
  }

  [Test]
  public void Read_WaterManagerDimensionsDifferFromTerrain_ThrowsInvalidDataException() {
    var terrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    var waterPayload = BuildWaterManagerPayload(width: 2, height: 1, []);
    using var stream = BuildDat(
      [TargetTerrainField(), WaterManagerField()],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        WriteDynamicPayload(writer, waterPayload);
      });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_FixedSizeWaterManagerBeforeTerrain_DecodesWithoutSizePrefix() {
    var terrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    var waterPayload = BuildWaterManagerPayload(width: 1, height: 1, []);
    using var stream = BuildDat(
      [
        new FieldSpec("Water", "WaterManager", Convert.ToUInt32(waterPayload.Length)),
        TargetTerrainField(),
      ],
      writer => {
        writer.Write(waterPayload);
        WriteDynamicPayload(writer, terrainPayload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.Multiple(new Action(() => {
      Assert.That(terrain.WaterManager, Is.Not.Null);
      Assert.That(terrain.WaterManager!.Width, Is.EqualTo(1));
      Assert.That(terrain.Cells[0].NorthEastHeight, Is.EqualTo(4f));
    }));
  }

  [Test]
  public void Read_TerrainInsideCollection_ConsumesRemainingValuesBeforeWaterManager() {
    var firstTerrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    var secondTerrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(10f, 20f, 30f, 40f, 7, 8)]);
    var waterPayload = BuildWaterManagerPayload(width: 1, height: 1, []);
    var terrainCollection = new FieldSpec(
      "Terrains",
      "array",
      children: [TargetTerrainField()]);
    using var stream = BuildDat(
      [terrainCollection, WaterManagerField()],
      writer => {
        writer.Write(0u);
        writer.Write(2u);
        WriteDynamicPayload(writer, firstTerrainPayload);
        WriteDynamicPayload(writer, secondTerrainPayload);
        WriteDynamicPayload(writer, waterPayload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.Multiple(new Action(() => {
      Assert.That(terrain.Cells[0].SouthWestHeight, Is.EqualTo(1f));
      Assert.That(terrain.WaterManager, Is.Not.Null);
      Assert.That(terrain.WaterManager!.Pools, Is.Empty);
    }));
  }

  [Test]
  public void Read_WaterManagerWithExtraPayloadByte_ThrowsInvalidDataException() {
    var payload = BuildWaterManagerPayload(width: 1, height: 1, []);
    Array.Resize(ref payload, payload.Length + 1);

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_TruncatedWaterManager_ThrowsInvalidDataException() {
    var completePayload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [new WaterPoolSpec(4f, [new WaterRecordSpec(0, 0, 0, 7)])]);
    var truncatedPayload = (byte[])completePayload.Clone();
    Array.Resize(ref truncatedPayload, truncatedPayload.Length - 1);

    AssertInvalidWater(truncatedPayload, Convert.ToUInt32(completePayload.Length));
  }

  [Test]
  public void Read_ExcessiveWaterPoolCount_ThrowsInvalidDataException() {
    var payload = BuildRawWaterPayload(writer => {
      writer.Write(Convert.ToByte(1));
      writer.Write(Convert.ToByte(1));
      writer.Write(uint.MaxValue);
    });

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_ExcessiveWaterRecordCount_ThrowsInvalidDataException() {
    var payload = BuildRawWaterPayload(writer => {
      writer.Write(Convert.ToByte(1));
      writer.Write(Convert.ToByte(1));
      writer.Write(1u);
      writer.Write(4f);
      writer.Write(uint.MaxValue);
    });

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_WaterRecordsOutsideRowMajorOrder_DecodesInFileOrder() {
    var payload = BuildWaterManagerPayload(
      width: 2,
      height: 1,
      [
        new WaterPoolSpec(
          4f,
          [new WaterRecordSpec(1, 0, 0, 7), new WaterRecordSpec(0, 0, 1, 7)]),
      ]);

    var terrainPayload = BuildTerrainPayload(
      width: 2,
      height: 1,
      TerrainRecord24Bytes,
      [
        new CellSpec(1f, 2f, 3f, 4f, 5, 6),
        new CellSpec(7f, 8f, 9f, 10f, 11, 12),
      ],
      includeTail: true);
    using var stream = BuildDat(
      [TargetTerrainField(), WaterManagerField()],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        WriteDynamicPayload(writer, payload);
      });

    var terrain = DatTerrainReader.Read(stream);

    Assert.That(terrain.WaterManager!.Pools[0].Records, Is.EqualTo(new[] {
      new DatWaterRecord(1, 0, 0, 7),
      new DatWaterRecord(0, 0, 1, 7),
    }));
  }

  [Test]
  public void Read_DuplicateWaterTriangle_ThrowsInvalidDataException() {
    var payload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [
        new WaterPoolSpec(
          4f,
          [new WaterRecordSpec(0, 0, 1, 3), new WaterRecordSpec(0, 0, 1, 7)]),
      ]);

    AssertInvalidWater(payload);
  }

  [TestCase(2, 0)]
  [TestCase(0, 2)]
  public void Read_WaterRecordOutsideManagerBounds_ThrowsInvalidDataException(int x, int y) {
    var payload = BuildWaterManagerPayload(
      width: 2,
      height: 2,
      [
        new WaterPoolSpec(
          4f,
          [new WaterRecordSpec(Convert.ToByte(x), Convert.ToByte(y), 0, 7)]),
      ]);

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_InvalidWaterTriangle_ThrowsInvalidDataException() {
    var payload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [new WaterPoolSpec(4f, [new WaterRecordSpec(0, 0, 2, 7)])]);

    AssertInvalidWater(payload);
  }

  [TestCase(0)]
  [TestCase(8)]
  public void Read_InvalidWaterVertexMask_ThrowsInvalidDataException(int vertexMask) {
    var payload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [
        new WaterPoolSpec(
          4f,
          [new WaterRecordSpec(0, 0, 0, Convert.ToByte(vertexMask))]),
      ]);

    AssertInvalidWater(payload);
  }

  [TestCase(0, 1)]
  [TestCase(1, 0)]
  public void Read_EmptyWaterManagerDimension_ThrowsInvalidDataException(int width, int height) {
    var payload = BuildWaterManagerPayload(width, height, []);

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_NonFiniteWaterHeight_ThrowsInvalidDataException() {
    var payload = BuildWaterManagerPayload(
      width: 1,
      height: 1,
      [new WaterPoolSpec(float.NaN, [])]);

    AssertInvalidWater(payload);
  }

  [Test]
  public void Read_TruncatedTerrain_ThrowsInvalidDataException() {
    var bytes = BuildValidDatBytes();
    Array.Resize(ref bytes, bytes.Length - 1);

    AssertInvalid(bytes);
  }

  [Test]
  public void Read_InvalidStructureIndex_ThrowsInvalidDataException() {
    using var stream = BuildDat(
      [TargetTerrainField()],
      _ => { },
      structureIndex: 1);

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_ExcessiveStructureCount_ThrowsInvalidDataException() {
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
      writer.Write(uint.MaxValue);
    stream.Position = 0;

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_ExcessiveEntryCount_ThrowsInvalidDataException() {
    using var stream = BuildDat(
      [TargetTerrainField()],
      _ => { },
      entryCount: uint.MaxValue);

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_InvalidTerrainSize_ThrowsInvalidDataException() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => {
        writer.Write(Convert.ToUInt32(payload.Length - 1));
        writer.Write(payload);
      });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_ExcessiveCustomPayloadSize_ThrowsInvalidDataException() {
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => writer.Write(uint.MaxValue));

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_TruncatedCustomPayload_ThrowsInvalidDataException() {
    using var stream = BuildDat(
      [new FieldSpec("Trees", "SkirtTrees"), TargetTerrainField()],
      writer => {
        writer.Write(4u);
        writer.Write(Convert.ToUInt16(0));
      });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_NonFiniteHeight_ThrowsInvalidDataException() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(float.NaN, 2f, 3f, 4f, 5, 6)]);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload));

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_NonFiniteMetadata_ThrowsInvalidDataException() {
    var payload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)],
      tileSizeX: float.PositiveInfinity);
    using var stream = BuildDat(
      [TargetTerrainField()],
      writer => WriteDynamicPayload(writer, payload));

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_ExcessiveCollectionLength_ThrowsInvalidDataException() {
    var arrayField = new FieldSpec(
      "Items",
      "array",
      children: [new FieldSpec("Value", "uint8")]);
    using var stream = BuildDat(
      [arrayField, TargetTerrainField()],
      writer => {
        writer.Write(0u);
        writer.Write(uint.MaxValue);
      });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_UnsupportedKind_ThrowsInvalidDataException() {
    using var stream = BuildDat(
      [new FieldSpec("Mystery", "unsupported")],
      _ => { });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  [Test]
  public void Read_UnsupportedExtendedHeaderVersion_ThrowsInvalidDataException() {
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(Convert.ToByte(0x3A));
    }
    stream.Position = 0;

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  private static byte[] BuildValidDatBytes() {
    return DatTerrainFixture.BuildMinimalTerrainBytes();
  }

  private static FieldSpec TargetTerrainField()
    => new("EngineTerrain", "GE_Terrain");

  private static FieldSpec WaterManagerField()
    => new("Water", "WaterManager");

  private static MemoryStream BuildDat(
    IReadOnlyList<FieldSpec> fields,
    Action<BinaryWriter> writeValues,
    uint structureIndex = 0,
    uint entryCount = 1,
    byte? extendedVersion = null) {
    var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      if (extendedVersion.HasValue) {
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(extendedVersion.Value);
        var definitionOffset = extendedVersion.Value == 0x1A ? 0x40 : 0x50;
        while (stream.Position < definitionOffset) writer.Write(Convert.ToByte(0));
      }

      writer.Write(1u);
      WriteAscii16(writer, "Landscape");
      writer.Write(Convert.ToUInt32(fields.Count));
      foreach (var field in fields) WriteFieldDefinition(writer, field);
      writer.Write(entryCount);
      if (entryCount == 1) {
        writer.Write(structureIndex);
        writer.Write(1ul);
        writeValues(writer);
      }
    }
    stream.Position = 0;
    return stream;
  }

  private static void WriteFieldDefinition(BinaryWriter writer, FieldSpec field) {
    WriteAscii16(writer, field.Name);
    WriteAscii16(writer, field.Kind);
    writer.Write(field.FixedSize);
    writer.Write(Convert.ToUInt32(field.Children.Count));
    foreach (var child in field.Children) WriteFieldDefinition(writer, child);
  }

  private static void WriteAscii16(BinaryWriter writer, string value) {
    var bytes = Encoding.ASCII.GetBytes(value);
    writer.Write(Convert.ToUInt16(bytes.Length));
    writer.Write(bytes);
  }

  private static void WriteDynamicPayload(BinaryWriter writer, byte[] payload) {
    writer.Write(Convert.ToUInt32(payload.Length));
    writer.Write(payload);
  }

  private static byte[] BuildTerrainPayload(
    int width,
    int height,
    int recordSize,
    IReadOnlyList<CellSpec> cells,
    bool includeTail = false,
    float originX = -16f,
    float originY = -20f,
    float tileSizeX = 4f,
    float tileSizeY = 4f) {
    if (cells.Count != checked(width * height))
      throw new ArgumentException("Test cells must match the requested dimensions.", nameof(cells));

    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      writer.Write(Convert.ToByte(width));
      writer.Write(Convert.ToByte(height));
      writer.Write(originX);
      writer.Write(originY);
      writer.Write(tileSizeX);
      writer.Write(tileSizeY);
      foreach (var cell in cells) {
        writer.Write(cell.SouthWest);
        writer.Write(cell.SouthEast);
        writer.Write(cell.NorthWest);
        writer.Write(cell.NorthEast);
        writer.Write(cell.SurfaceIndex);
        writer.Write(cell.CliffIndex);
        for (var padding = 18; padding < recordSize; padding++)
          writer.Write(Convert.ToByte(padding));
      }
      if (includeTail) writer.Write(0x0102030405060708ul);
    }
    return stream.ToArray();
  }

  private static byte[] BuildWaterManagerPayload(
    int width,
    int height,
    IReadOnlyList<WaterPoolSpec> pools) {
    return BuildRawWaterPayload(writer => {
      writer.Write(Convert.ToByte(width));
      writer.Write(Convert.ToByte(height));
      writer.Write(Convert.ToUInt32(pools.Count));
      foreach (var pool in pools) {
        writer.Write(pool.Height);
        writer.Write(Convert.ToUInt32(pool.Records.Count));
        foreach (var record in pool.Records) {
          writer.Write(record.X);
          writer.Write(record.Y);
          writer.Write(record.Triangle);
          writer.Write(record.VertexMask);
        }
      }
    });
  }

  private static byte[] BuildRawWaterPayload(Action<BinaryWriter> writePayload) {
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
      writePayload(writer);
    return stream.ToArray();
  }

  private static void AssertInvalidWater(byte[] payload, uint? declaredSize = null) {
    var terrainPayload = BuildTerrainPayload(
      width: 1,
      height: 1,
      TerrainRecord24Bytes,
      [new CellSpec(1f, 2f, 3f, 4f, 5, 6)]);
    using var stream = BuildDat(
      [TargetTerrainField(), WaterManagerField()],
      writer => {
        WriteDynamicPayload(writer, terrainPayload);
        writer.Write(declaredSize ?? Convert.ToUInt32(payload.Length));
        writer.Write(payload);
      });

    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  private static void AssertInvalid(byte[] bytes) {
    using var stream = new MemoryStream(bytes);
    Assert.Throws<InvalidDataException>(new Action(() => DatTerrainReader.Read(stream)));
  }

  private sealed class FieldSpec {
    public string Name { get; }
    public string Kind { get; }
    public uint FixedSize { get; }
    public IReadOnlyList<FieldSpec> Children { get; }

    public FieldSpec(
      string name,
      string kind,
      uint fixedSize = 0,
      IReadOnlyList<FieldSpec>? children = null) {
      Name = name;
      Kind = kind;
      FixedSize = fixedSize;
      Children = children ?? Array.Empty<FieldSpec>();
    }
  }

  private readonly struct CellSpec {
    public float SouthWest { get; }
    public float SouthEast { get; }
    public float NorthWest { get; }
    public float NorthEast { get; }
    public byte SurfaceIndex { get; }
    public byte CliffIndex { get; }

    public CellSpec(
      float southWest,
      float southEast,
      float northWest,
      float northEast,
      byte surfaceIndex,
      byte cliffIndex) {
      SouthWest = southWest;
      SouthEast = southEast;
      NorthWest = northWest;
      NorthEast = northEast;
      SurfaceIndex = surfaceIndex;
      CliffIndex = cliffIndex;
    }
  }

  private readonly struct WaterPoolSpec {
    public float Height { get; }
    public IReadOnlyList<WaterRecordSpec> Records { get; }

    public WaterPoolSpec(float height, IReadOnlyList<WaterRecordSpec> records) {
      Height = height;
      Records = records;
    }
  }

  private readonly struct WaterRecordSpec {
    public byte X { get; }
    public byte Y { get; }
    public byte Triangle { get; }
    public byte VertexMask { get; }

    public WaterRecordSpec(byte x, byte y, byte triangle, byte vertexMask) {
      X = x;
      Y = y;
      Triangle = triangle;
      VertexMask = vertexMask;
    }
  }
}

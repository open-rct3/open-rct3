using System.Buffers.Binary;
using System.Text;
using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OvlVersion = OpenCobra.OVL.Version;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TerrainTypesTests {
  [Test]
  public void Decode_PreservesFieldsAndTypedReferences() {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 4, 7);
    WriteUInt32(bytes, 12, 42);
    WriteUInt32(bytes, 32, 0x11223344);
    WriteUInt32(bytes, 36, 0x55667788);
    WriteSingle(bytes, 40, 0.25f);
    WriteSingle(bytes, 44, 0.5f);
    WriteSingle(bytes, 48, 0.3f);
    WriteSingle(bytes, 52, -0.125f);
    WriteSingle(bytes, 56, 0.75f);

    var terrain = TerrainTypes.Decode("TerrainGrass", bytes, ResolveReference);

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.Name, Is.EqualTo("TerrainGrass"));
      Assert.That(terrain.DescriptionName, Is.EqualTo("TerrainGrassDescription"));
      Assert.That(terrain.Description.Type, Is.EqualTo(FileType.Text));
      Assert.That(terrain.IconName, Is.EqualTo("TerrainGrassIcon"));
      Assert.That(terrain.Icon.Type, Is.EqualTo(FileType.GuiSkinItem));
      Assert.That(terrain.TextureRef, Is.EqualTo("Terrain_00"));
      Assert.That(terrain.Texture.QualifiedName, Is.EqualTo("Terrain_00:tex"));
      Assert.That(terrain.Version, Is.EqualTo(1));
      Assert.That(terrain.Addon, Is.EqualTo(Addon.Soaked));
      Assert.That(terrain.Number, Is.EqualTo(42));
      Assert.That(terrain.Type, Is.EqualTo(TerrainTypeKind.GroundBlended));
      Assert.That(terrain.Parameters.Color01, Is.EqualTo(0x11223344));
      Assert.That(terrain.Parameters.Color02, Is.EqualTo(0x55667788));
      Assert.That(terrain.Parameters.InvWidth, Is.EqualTo(0.25f));
      Assert.That(terrain.Parameters.InvHeight, Is.EqualTo(0.5f));
      Assert.That(terrain.Unknowns.Unk02, Is.EqualTo(7));
      Assert.That(terrain.Unknowns.Unk13, Is.EqualTo(0.3f));
      Assert.That(terrain.Unknowns.Unk14, Is.EqualTo(-0.125f));
      Assert.That(terrain.Unknowns.Unk15, Is.EqualTo(0.75f));
    }
  }

  [TestCase(0)]
  [TestCase(TerrainTypes.RecordSize - 1)]
  [TestCase(TerrainTypes.RecordSize + 1)]
  public void Decode_RequiresExactRecordSize(int size) {
    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("bad-size", new byte[size], ResolveReference)));
  }

  [TestCase(0u)]
  [TestCase(2u)]
  [TestCase(uint.MaxValue)]
  public void Decode_RejectsUnsupportedVersion(uint version) {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 0, version);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("bad-version", bytes, ResolveReference)));
  }

  [TestCase(3u)]
  [TestCase(uint.MaxValue)]
  public void Decode_RejectsInvalidAddon(uint addon) {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 8, addon);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("bad-addon", bytes, ResolveReference)));
  }

  [TestCase(3u)]
  [TestCase(uint.MaxValue)]
  public void Decode_RejectsInvalidTerrainType(uint type) {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 16, type);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("bad-type", bytes, ResolveReference)));
  }

  [TestCaseSource(nameof(KnownAddons))]
  public void Decode_AcceptsKnownAddon(Addon addon) {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 8, Convert.ToUInt32(addon));

    var terrain = TerrainTypes.Decode("known-addon", bytes, ResolveReference);

    Assert.That(terrain.Addon, Is.EqualTo(addon));
  }

  [TestCase(TerrainTypeKind.GroundUnblended)]
  [TestCase(TerrainTypeKind.Cliff)]
  [TestCase(TerrainTypeKind.GroundBlended)]
  public void Decode_AcceptsKnownTerrainType(TerrainTypeKind type) {
    var bytes = ValidRecord();
    WriteUInt32(bytes, 16, Convert.ToUInt32(type));

    var terrain = TerrainTypes.Decode("known-type", bytes, ResolveReference);

    Assert.That(terrain.Type, Is.EqualTo(type));
  }

  [TestCase(OvlVersion.One)]
  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void ReadStringTableBlock_RejectsMissingBlock(OvlVersion version) {
    using var stream = OvlArchive(version, typeZeroBlockSize: null, []);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    Assert.Throws<InvalidDataException>(new Action(() => TerrainTypes.ReadStringTableBlock(reader)));
  }

  [TestCase(OvlVersion.One)]
  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void ReadStringTableBlock_RejectsZeroSizedBlock(OvlVersion version) {
    using var stream = OvlArchive(version, typeZeroBlockSize: 0, []);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    Assert.Throws<InvalidDataException>(new Action(() => TerrainTypes.ReadStringTableBlock(reader)));
  }

  [TestCase(OvlVersion.One)]
  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void ReadStringTableBlock_PreservesFirstBlockSize(OvlVersion version) {
    using var stream = OvlArchive(version, typeZeroBlockSize: 1, [0]);
    var expectedOffset = stream.Length - 1;
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var block = TerrainTypes.ReadStringTableBlock(reader);

    using (Assert.EnterMultipleScope()) {
      Assert.That(block.Address, Is.EqualTo(0));
      Assert.That(block.Offset, Is.EqualTo(expectedOffset));
      Assert.That(block.Size, Is.EqualTo(1));
    }
  }

  [TestCase((uint)int.MaxValue)]
  [TestCase(uint.MaxValue)]
  public void ReadStringTableBlock_RejectsOversizedReferenceCount(uint count) {
    using var stream = OvlArchive(
      OvlVersion.Four, typeZeroBlockSize: 1, [0], referenceCount: count);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    Assert.Throws<InvalidDataException>(new Action(() => TerrainTypes.ReadStringTableBlock(reader)));
  }

  [Test]
  public void ReadStringTableBlock_RejectsCountBeyondRemainingBytes() {
    using var stream = OvlArchive(
      OvlVersion.Four, typeZeroBlockSize: 1, [0], referenceCount: 100);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var exception = Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.ReadStringTableBlock(reader)));

    Assert.That(exception!.Message, Does.Contain("remaining file data"));
  }

  [TestCase((uint)int.MaxValue)]
  [TestCase(uint.MaxValue)]
  public void ReadStringTableBlock_RejectsOversizedLoaderCount(uint count) {
    using var stream = OvlArchive(
      OvlVersion.Four, typeZeroBlockSize: 1, [0], loaderCount: count);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    Assert.Throws<InvalidDataException>(new Action(() => TerrainTypes.ReadStringTableBlock(reader)));
  }

  [TestCase((uint)int.MaxValue)]
  [TestCase(uint.MaxValue)]
  public void ReadStringTableBlock_RejectsOversizedBlockCount(uint count) {
    using var stream = OvlArchive(
      OvlVersion.Four,
      typeZeroBlockSize: 1,
      [0],
      typeZeroBlockCountOverride: count);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    Assert.Throws<InvalidDataException>(new Action(() => TerrainTypes.ReadStringTableBlock(reader)));
  }

  [Test]
  public void ReadStringTableBlock_RejectsOversizedAggregateBlockCount() {
    using var stream = OvlArchiveWithBlockCounts(typeZeroBlockCount: 32_769, typeOneBlockCount: 32_768);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var exception = Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.ReadStringTableBlock(reader)));

    Assert.That(exception!.Message, Does.Contain("aggregate block count"));
  }

  [Test]
  public void ReadStringTableBlock_AcceptsEvenNonzeroV5SubVersionAlignment() {
    using var stream = OvlArchive(
      OvlVersion.Five,
      typeZeroBlockSize: 4,
      [(byte)'t', (byte)'e', (byte)'r', 0],
      subVersionFlag: 2);
    var expectedOffset = stream.Length - 4;
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var block = TerrainTypes.ReadStringTableBlock(reader);

    using (Assert.EnterMultipleScope()) {
      Assert.That(block.Offset, Is.EqualTo(expectedOffset));
      Assert.That(block.Size, Is.EqualTo(4));
    }
  }

  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void TryReadStringTableStart_RejectsTerminatorInFollowingBlock(OvlVersion version) {
    using var stream = OvlArchive(
      version,
      typeZeroBlockSize: 3,
      [(byte)'t', (byte)'e', (byte)'r', 0],
      typeOneBlockSize: 1);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var resolved = TerrainTypes.TryReadStringTableStart(reader, out var value);

    using (Assert.EnterMultipleScope()) {
      Assert.That(resolved, Is.False);
      Assert.That(value, Is.Empty);
    }
  }

  [TestCase(OvlVersion.One)]
  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void TryReadStringTableStart_AcceptsTerminatorInsideBlock(OvlVersion version) {
    using var stream = OvlArchive(
      version,
      typeZeroBlockSize: 4,
      [(byte)'t', (byte)'e', (byte)'r', 0]);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var resolved = TerrainTypes.TryReadString(reader, address: 0, out var value);

    using (Assert.EnterMultipleScope()) {
      Assert.That(resolved, Is.True);
      Assert.That(value, Is.EqualTo("ter"));
    }
  }

  [TestCase(OvlVersion.Four)]
  [TestCase(OvlVersion.Five)]
  public void TryReadString_RejectsAddressInNonStringBlock(OvlVersion version) {
    using var stream = OvlArchive(
      version,
      typeZeroBlockSize: 4,
      [(byte)'t', (byte)'e', (byte)'r', 0, (byte)'b', (byte)'a', (byte)'d', 0],
      typeOneBlockSize: 4);
    using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

    var resolved = TerrainTypes.TryReadString(reader, address: 4, out var value);

    using (Assert.EnterMultipleScope()) {
      Assert.That(resolved, Is.False);
      Assert.That(value, Is.Empty);
    }
  }

  [TestCase(40)]
  [TestCase(44)]
  [TestCase(48)]
  [TestCase(52)]
  [TestCase(56)]
  public void Decode_RejectsNonFiniteFloats(int offset) {
    var bytes = ValidRecord();
    WriteSingle(bytes, offset, float.NaN);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("non-finite", bytes, ResolveReference)));
  }

  [TestCase(40, 0f)]
  [TestCase(40, -0.1f)]
  [TestCase(44, 0f)]
  [TestCase(44, -0.1f)]
  public void Decode_RejectsNonPositiveInverseDimensions(int offset, float value) {
    var bytes = ValidRecord();
    WriteSingle(bytes, offset, value);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode("bad-scale", bytes, ResolveReference)));
  }

  [Test]
  public void Decode_RejectsWrongReferenceType() {
    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode(
        "wrong-reference",
        ValidRecord(),
        (_, _) => new TerrainResourceReference("wrong", FileType.Texture)
      )));
  }

  [Test]
  public void Decode_RejectsEmptyReferenceName() {
    Assert.Throws<InvalidDataException>(new Action(() =>
      TerrainTypes.Decode(
        "empty-reference",
        ValidRecord(),
        (_, type) => new TerrainResourceReference("", type)
      )));
  }

  private static TerrainResourceReference ResolveReference(int offset, FileType expectedType) =>
    offset switch {
      20 => new TerrainResourceReference("Terrain_00", FileType.Texture),
      24 => new TerrainResourceReference("TerrainGrassDescription", FileType.Text),
      28 => new TerrainResourceReference("TerrainGrassIcon", FileType.GuiSkinItem),
      _ => throw new AssertionException($"Unexpected reference offset {offset} for {expectedType}.")
    };

  private static byte[] ValidRecord() {
    var bytes = new byte[TerrainTypes.RecordSize];
    WriteUInt32(bytes, 0, 1);
    WriteUInt32(bytes, 8, Convert.ToUInt32(Addon.Soaked));
    WriteUInt32(bytes, 16, Convert.ToUInt32(TerrainTypeKind.GroundBlended));
    WriteSingle(bytes, 40, 0.1f);
    WriteSingle(bytes, 44, 0.1f);
    return bytes;
  }

  private static IEnumerable<Addon> KnownAddons => Enum.GetValues<Addon>();

  private static MemoryStream OvlArchive(
    OvlVersion version,
    uint? typeZeroBlockSize,
    byte[] rawData,
    uint typeOneBlockSize = 0,
    uint subVersionFlag = 0,
    uint referenceCount = 0,
    uint loaderCount = 0,
    uint? typeZeroBlockCountOverride = null
  ) {
    if (version == OvlVersion.One && typeOneBlockSize != 0)
      throw new ArgumentException("The synthetic v1 fixture supports only its first type-0 block.");
    var expectedRawSize = checked((typeZeroBlockSize ?? 0) + typeOneBlockSize);
    if (rawData.Length != expectedRawSize)
      throw new ArgumentException("Raw data length must match the synthetic block definitions.");

    var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      writer.Write(0x4b524746u);
      writer.Write(0u);
      writer.Write(Convert.ToUInt32(version));
      writer.Write(version == OvlVersion.One ? referenceCount : 0u);
      if (version == OvlVersion.Four) {
        writer.Write(referenceCount);
      } else if (version == OvlVersion.Five) {
        writer.Write(subVersionFlag);
        if (subVersionFlag != 0) {
          writer.Write(0u);
          writer.Write(0u);
          writer.Write(0u);
          writer.Write((byte)0);
          while (stream.Position % 4 != 0) writer.Write((byte)0);
        }
        writer.Write(referenceCount);
      }

      writer.Write(0u);
      writer.Write(loaderCount);
      foreach (var typeIndex in Enumerable.Range(0, 9)) {
        var blockSize = typeIndex switch {
          0 => typeZeroBlockSize,
          1 when typeOneBlockSize != 0 => typeOneBlockSize,
          _ => null
        };
        var blockCount = typeIndex == 0 && typeZeroBlockCountOverride.HasValue
          ? typeZeroBlockCountOverride.Value
          : blockSize.HasValue ? 1u : 0u;
        writer.Write(blockCount);
        if (version != OvlVersion.One) {
          writer.Write(0u);
          if (version == OvlVersion.Five && subVersionFlag != 0) writer.Write(0u);
          if (!typeZeroBlockCountOverride.HasValue || typeIndex != 0) {
            if (blockSize.HasValue) writer.Write(blockSize.Value);
          }
        }
      }

      if (version == OvlVersion.Four || version == OvlVersion.Five) {
        writer.Write(0u);
        writer.Write(0u);
      }
      if (version == OvlVersion.One && typeZeroBlockSize.HasValue)
        writer.Write(typeZeroBlockSize.Value);
      writer.Write(rawData);
    }
    stream.Position = 0;
    return stream;
  }

  private static MemoryStream OvlArchiveWithBlockCounts(
    int typeZeroBlockCount,
    int typeOneBlockCount
  ) {
    var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      writer.Write(0x4b524746u);
      writer.Write(0u);
      writer.Write(Convert.ToUInt32(OvlVersion.Four));
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      foreach (var typeIndex in Enumerable.Range(0, 9)) {
        var count = typeIndex switch {
          0 => typeZeroBlockCount,
          1 => typeOneBlockCount,
          _ => 0
        };
        writer.Write(Convert.ToUInt32(count));
        writer.Write(0u);
        foreach (var _ in Enumerable.Range(0, count)) writer.Write(0u);
      }
      writer.Write(0u);
      writer.Write(0u);
    }
    stream.Position = 0;
    return stream;
  }

  private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);

  private static void WriteSingle(byte[] bytes, int offset, float value) =>
    WriteUInt32(bytes, offset, BitConverter.SingleToUInt32Bits(value));
}

using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TextureDecodingTests {
  [Test]
  public void ReadBitmapTable_RequiresExactStructSize() {
    var bytes = WriteUInt32s(0, 17);
    AssertExactRead<BitmapTable>(bytes, table => Assert.That(table.Length, Is.EqualTo(17)));
  }

  [Test]
  public void ReadFlicHeader_RequiresExactStructSize() {
    var bytes = WriteUInt32s(Convert.ToUInt32(TextureFormat.Dxt5), 17, 9, 3);
    AssertExactRead<FlicHeader>(bytes, header => {
      Assert.That(header.Format, Is.EqualTo(TextureFormat.Dxt5));
      Assert.That(header.Width, Is.EqualTo(17));
      Assert.That(header.Height, Is.EqualTo(9));
      Assert.That(header.MipCount, Is.EqualTo(3));
    });
  }

  [Test]
  public void ReadFlicMipHeader_RequiresExactStructSize() {
    var bytes = WriteUInt32s(17, 9, 48, 4);
    AssertExactRead<FlicMipHeader>(bytes, header => {
      Assert.That(header.Width, Is.EqualTo(17));
      Assert.That(header.Height, Is.EqualTo(9));
      Assert.That(header.Pitch, Is.EqualTo(48));
      Assert.That(header.Blocks, Is.EqualTo(4));
    });
  }

  [Test]
  public void ReadTex_RequiresExactStructSize() {
    var bytes = new byte[Marshal.SizeOf<Tex>()];
    WriteUInt32(bytes, 40, Convert.ToUInt32(TextureType.Icon));
    WriteUInt32(bytes, 52, 1234);
    AssertExactRead<Tex>(bytes, tex => {
      Assert.That(tex.Type, Is.EqualTo(TextureType.Icon));
      Assert.That(tex.FlicPtr, Is.EqualTo(1234));
    });
  }

  [Test]
  public void ReadTexture_RejectsTruncatedTexHeader() {
    using var ovl = new Ovl("truncated");

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadTexture("truncated", ovl, 0, new byte[Marshal.SizeOf<Tex>() - 1], null)));
  }

  [Test]
  public void ReadTexture_WithoutFlicRelocation_IsTextureless() {
    using var ovl = new Ovl("textureless");

    var texture = TextureDecoding.ReadTexture(
      "textureless", ovl, 0, new byte[Marshal.SizeOf<Tex>()], null);

    Assert.That(texture, Is.Null);
  }

  [Test]
  public void ReadTexture_RejectsNonzeroFlicPointerWithoutRelocation() {
    using var ovl = new Ovl("unrelocated");
    var bytes = new byte[Marshal.SizeOf<Tex>()];
    WriteUInt32(bytes, 52, 0xDEADBEEF);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadTexture("unrelocated", ovl, 0, bytes, null)));
  }

  [Test]
  public void ReadTexture_RejectsUnresolvedSecondPointerHop() {
    using var ovl = new Ovl("unresolved");
    Relocations(ovl)[52] = 1234;

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadTexture(
        "unresolved", ovl, 0, new byte[Marshal.SizeOf<Tex>()], null)));
  }

  [Test]
  public void ReadTexture_RejectsResolvedFlicWithoutExtraData() {
    using var ovl = new Ovl("missing-extra-data");
    Relocations(ovl)[52] = 1234;
    Relocations(ovl)[1234] = 5678;

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadTexture(
        "missing-extra-data", ovl, 0, new byte[Marshal.SizeOf<Tex>()], null)));
  }

  [Test]
  public void ReadFlic_RejectsTruncatedMipHeader() {
    var header = WriteUInt32s(Convert.ToUInt32(TextureFormat.Dxt1), 4, 4, 1);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadFlic("truncated", header)));
  }

  [Test]
  public void ReadFlic_DecodesStandaloneBaseMipAndStopsAtSentinel() {
    byte[] dxt1Block = [0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0];
    var chunk = WriteUInt32s(
      Convert.ToUInt32(TextureFormat.Dxt1), 4, 4, 1,
      4, 4, 8, 1,
      0x001FF800, 0,
      0, 0, 0, 0);
    Assert.That(chunk.AsSpan(32, 8).ToArray(), Is.EqualTo(dxt1Block));

    using var texture = TextureDecoding.ReadFlic("standalone", chunk);

    Assert.That(texture.Width, Is.EqualTo(4));
    Assert.That(texture.Height, Is.EqualTo(4));
    Assert.That(texture.MipLevels[0][0, 0], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
  }

  [Test]
  public void ReadFlic_RejectsImpossibleMipPayloadSize() {
    var chunk = WriteUInt32s(
      Convert.ToUInt32(TextureFormat.Dxt1), 4, 4, 1,
      4, 4, uint.MaxValue, uint.MaxValue);

    Assert.Throws<OverflowException>(new Action(() =>
      TextureDecoding.ReadFlic("impossible", chunk)));
  }

  [Test]
  public void ReadFlic_RejectsUnsupportedFormatAndImpossibleDimensions() {
    var unsupported = WriteUInt32s(Convert.ToUInt32(TextureFormat.P8), 4, 4, 1);
    var impossible = WriteUInt32s(
      Convert.ToUInt32(TextureFormat.Dxt1), Convert.ToUInt32(int.MaxValue), 2, 1);

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadFlic("unsupported", unsupported)));
    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadFlic("impossible", impossible)));
  }

  [Test]
  public void ReadBitmapTable_RejectsTruncatedInlineHeader() {
    using var ovl = new Ovl("truncated");
    var file = new OvlFile("truncated", FileType.BitmapTable, "truncated.common.ovl");

    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.ReadBitmapTable(
        "truncated", ovl, file, new ReadOnlyMemory<byte>(new byte[Marshal.SizeOf<BitmapTable>() - 1]))));
  }

  [Test]
  public void ReadBitmapTable_ZeroLengthStillRequiresReferenceChunks() {
    using var ovl = new Ovl("empty");
    var file = new OvlFile("empty", FileType.BitmapTable, "empty.common.ovl");

    Assert.Throws<InvalidOperationException>(new Action(() =>
      TextureDecoding.ReadBitmapTable("empty", ovl, file, WriteUInt32s(0, 0))));
  }

  [Test]
  public void DecodeBitmapTable_UsesStoredMipCount() {
    var chunks = BitmapTableChunks(TextureFormat.A8R8G8B8, 8, 8, 1, new byte[8 * 8 * 4]);

    var textures = TextureDecoding.DecodeBitmapTable("stored-mips", BitmapTableWithLength(1), chunks);
    try {
      Assert.That(textures, Has.Length.EqualTo(1));
      Assert.That(textures[0].MipCount, Is.EqualTo(1));
      Assert.That(textures[0].MipLevels, Has.Length.EqualTo(1));
      Assert.That(textures[0].MipLevels[0].Width, Is.EqualTo(8));
      Assert.That(textures[0].MipLevels[0].Height, Is.EqualTo(8));
    } finally {
      DisposeAll(textures);
    }
  }

  [Test]
  public void DecodeBitmapTable_EmptyTableStillRequiresExactChunks() {
    var textures = TextureDecoding.DecodeBitmapTable(
      "empty", BitmapTableWithLength(0), [WriteUInt32s(0, 0), []]);

    Assert.That(textures, Is.Empty);
    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.DecodeBitmapTable("empty", BitmapTableWithLength(0), [])));
  }

  [Test]
  public void DecodeBitmapTable_AdvancesSharedPixelCursorAcrossEntriesAndMips() {
    var headers = new List<byte>(WriteUInt32s(0, 0));
    headers.AddRange(WriteUInt32s(Convert.ToUInt32(TextureFormat.A8R8G8B8), 2, 2, 2));
    headers.AddRange(WriteUInt32s(Convert.ToUInt32(TextureFormat.A8R8G8B8), 1, 1, 1));
    var pixels = Enumerable.Range(0, 24).Select(Convert.ToByte).ToArray();

    var textures = TextureDecoding.DecodeBitmapTable(
      "shared-cursor", BitmapTableWithLength(2), [headers.ToArray(), pixels]);
    try {
      Assert.That(textures, Has.Length.EqualTo(2));
      Assert.That(textures[0].MipLevels[0][0, 0], Is.EqualTo(new Rgba32(1, 2, 3, 0)));
      Assert.That(textures[0].MipLevels[1][0, 0], Is.EqualTo(new Rgba32(17, 18, 19, 16)));
      Assert.That(textures[1].MipLevels[0][0, 0], Is.EqualTo(new Rgba32(21, 22, 23, 20)));
    } finally {
      DisposeAll(textures);
    }
  }

  [Test]
  public void DecodeBitmapTable_RequiresExactlyTwoChunks() {
    var header = BitmapTableHeader(TextureFormat.A8R8G8B8, 1, 1, 1);
    AssertInvalidBitmapTable([header]);
    AssertInvalidBitmapTable([header, new byte[4], []]);
  }

  [Test]
  public void DecodeBitmapTable_RequiresExactHeaderCursor() {
    var header = BitmapTableHeader(TextureFormat.A8R8G8B8, 1, 1, 1);
    AssertInvalidBitmapTable([header[..^1], new byte[4]]);
    AssertInvalidBitmapTable([[.. header, 0], new byte[4]]);
    header[0] = 1;
    AssertInvalidBitmapTable([header, new byte[4]]);
  }

  [Test]
  public void DecodeBitmapTable_RequiresExactPixelCursor() {
    var header = BitmapTableHeader(TextureFormat.A8R8G8B8, 1, 1, 1);
    AssertInvalidBitmapTable([header, new byte[3]]);
    AssertInvalidBitmapTable([header, new byte[5]]);
  }

  [Test]
  public void DecodeBitmapTable_RejectsInvalidHeaderValues() {
    AssertInvalidBitmapTable(BitmapTableChunks(TextureFormat.A8R8G8B8, 0, 1, 1, []));
    AssertInvalidBitmapTable(BitmapTableChunks(TextureFormat.A8R8G8B8, 1, 0, 1, []));
    AssertInvalidBitmapTable(BitmapTableChunks(TextureFormat.A8R8G8B8, 1, 1, 0, []));
    AssertInvalidBitmapTable(BitmapTableChunks(TextureFormat.P8, 1, 1, 1, [0]));
    AssertInvalidBitmapTable(BitmapTableChunks(
      TextureFormat.Dxt1, Convert.ToUInt32(int.MaxValue), 2, 1, []));
  }

  [Test]
  public void DecodeDxt1_InterpolatesBothFourColorEntries() {
    byte[] block = [0x00, 0xF8, 0x1F, 0x00, 0x0E, 0, 0, 0];
    using var image = DxtDecoder.DecodeDxt1(block, 4, 4);

    Assert.That(image[0, 0], Is.EqualTo(new Rgba32(170, 0, 85, 255)));
    Assert.That(image[1, 0], Is.EqualTo(new Rgba32(85, 0, 170, 255)));
  }

  [Test]
  public void DecodeDxt3_DecodesFourBitAlphaAfterColor() {
    byte[] block = [
      0x00, 0x00, 0x55, 0x55, 0xAA, 0xAA, 0xFF, 0xFF,
      0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0,
    ];
    using var image = DxtDecoder.DecodeDxt3(block, 4, 4);

    for (var y = 0; y < 4; y++)
    for (var x = 0; x < 4; x++)
      Assert.That(image[x, y], Is.EqualTo(new Rgba32(255, 0, 0, Convert.ToByte(y * 85))));
  }

  [TestCase(1)]
  [TestCase(2)]
  [TestCase(3)]
  public void DecodeDxt3_ClipsPartialBlocks(int size) {
    byte[] block = [
      0x00, 0x00, 0x55, 0x55, 0xAA, 0xAA, 0xFF, 0xFF,
      0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0,
    ];
    using var image = DxtDecoder.DecodeDxt3(block, size, size);

    Assert.That(image.Width, Is.EqualTo(size));
    Assert.That(image.Height, Is.EqualTo(size));
    Assert.That(image[size - 1, size - 1].A, Is.EqualTo(Convert.ToByte((size - 1) * 85)));
  }

  [Test]
  public void DecodeDxt5_DecodesInterpolatedAlpha() {
    byte[] block = [
      0xFF, 0x00, 0x92, 0x24, 0x49, 0x92, 0x24, 0x49,
      0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0,
    ];
    using var image = DxtDecoder.DecodeDxt5(block, 4, 4);

    Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 0, 0, 218)));
    Assert.That(image[3, 3], Is.EqualTo(new Rgba32(255, 0, 0, 218)));
  }

  [Test]
  public void DecodeDxt5_DecodesReverseEndpointAlpha() {
    byte[] block = [
      0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
      0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0,
    ];
    using var image = DxtDecoder.DecodeDxt5(block, 4, 4);

    Assert.That(image[0, 0], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
    Assert.That(image[3, 3], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
  }

  [TestCase(1)]
  [TestCase(2)]
  [TestCase(3)]
  public void DecodeDxt5_ClipsPartialBlocks(int size) {
    byte[] block = [
      0xFF, 0x00, 0x92, 0x24, 0x49, 0x92, 0x24, 0x49,
      0x00, 0xF8, 0x1F, 0x00, 0, 0, 0, 0,
    ];
    using var image = DxtDecoder.DecodeDxt5(block, size, size);

    Assert.That(image.Width, Is.EqualTo(size));
    Assert.That(image.Height, Is.EqualTo(size));
    Assert.That(image[size - 1, size - 1].A, Is.EqualTo(218));
  }

  [TestCase(TextureFormat.Dxt3)]
  [TestCase(TextureFormat.Dxt5)]
  public void DecodeDxtAlphaFormat_AlwaysUsesFourColorMode(TextureFormat format) {
    var block = new byte[16];
    if (format == TextureFormat.Dxt3) {
      Array.Fill(block, Convert.ToByte(0xFF), 0, 8);
    } else {
      block[0] = 0xFF;
    }
    block[8] = 0x1F;
    block[9] = 0x00;
    block[10] = 0x00;
    block[11] = 0xF8;
    block[12] = 0x03;

    using var image = format == TextureFormat.Dxt3
      ? DxtDecoder.DecodeDxt3(block, 4, 4)
      : DxtDecoder.DecodeDxt5(block, 4, 4);

    Assert.That(image[0, 0], Is.EqualTo(new Rgba32(170, 0, 85, 255)));
  }

  [Test]
  public void ReadFlic_BitmapTableCloneOwnsItsMipImages() {
    using var source = new Texture("table", TextureFormat.A8R8G8B8, 1, 1);
    source.MipLevels[0] = new Image<Rgba32>(1, 1, new Rgba32(12, 34, 56, 78));

    using var clone = TextureDecoding.ReadFlic("named", [0, 0, 0, 0], [source]);
    source.Dispose();

    Assert.That(clone.Name, Is.EqualTo("named"));
    Assert.That(clone.MipLevels[0][0, 0], Is.EqualTo(new Rgba32(12, 34, 56, 78)));
  }

  [Test]
  public void TextureCollection_OwnsEntriesAndImplementsListIndexer() {
    using var collection = new TextureCollection();
    var first = new Texture("same", TextureFormat.A8R8G8B8, 1, 1);
    var firstImage = new Image<Rgba32>(1, 1);
    first.MipLevels[0] = firstImage;
    var replacement = new Texture("same", TextureFormat.A8R8G8B8, 1, 1);
    replacement.MipLevels[0] = new Image<Rgba32>(1, 1);

    collection.Add(first);
    collection.Add(replacement);

    Assert.That(collection, Has.Count.EqualTo(1));
    Assert.That(((IReadOnlyList<Texture>)collection)[0], Is.SameAs(replacement));
    Assert.Throws<ObjectDisposedException>(new Action(() => firstImage.Clone()));
  }

  private static void AssertExactRead<T>(byte[] bytes, Action<T> assertValue) where T : struct {
    Assert.That(bytes, Has.Length.EqualTo(Marshal.SizeOf<T>()));
    using (var stream = new MemoryStream(bytes))
    using (var reader = new BinaryReader(stream)) {
      Assert.That(reader.Read<T>(out var value), Is.EqualTo(bytes.Length));
      assertValue(value);
    }

    for (var length = 0; length < bytes.Length; length++) {
      using var stream = new MemoryStream(bytes[..length]);
      using var reader = new BinaryReader(stream);
      Assert.That(reader.Read<T>(out var value), Is.Zero, $"short length {length}");
      Assert.That(value, Is.EqualTo(default(T)), $"short length {length}");
    }
  }

  private static BitmapTable BitmapTableWithLength(uint length) {
    using var stream = new MemoryStream(WriteUInt32s(0, length));
    using var reader = new BinaryReader(stream);
    return reader.ReadBitmapTable();
  }

  private static IReadOnlyList<byte[]> BitmapTableChunks(
    TextureFormat format, uint width, uint height, uint mipCount, byte[] pixels
  ) => [BitmapTableHeader(format, width, height, mipCount), pixels];

  private static byte[] BitmapTableHeader(
    TextureFormat format, uint width, uint height, uint mipCount
  ) => WriteUInt32s(0, 0, Convert.ToUInt32(format), width, height, mipCount);

  private static void AssertInvalidBitmapTable(IReadOnlyList<byte[]> chunks) =>
    Assert.Throws<InvalidDataException>(new Action(() =>
      TextureDecoding.DecodeBitmapTable("invalid", BitmapTableWithLength(1), chunks)));

  private static byte[] WriteUInt32s(params uint[] values) {
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    foreach (var value in values) writer.Write(value);
    return stream.ToArray();
  }

  private static void WriteUInt32(byte[] bytes, int offset, uint value) {
    using var stream = new MemoryStream(bytes);
    stream.Position = offset;
    using var writer = new BinaryWriter(stream);
    writer.Write(value);
  }

  private static void DisposeAll(IEnumerable<Texture> textures) {
    foreach (var texture in textures) texture.Dispose();
  }

  private static Dictionary<uint, uint> Relocations(Ovl ovl) =>
    (Dictionary<uint, uint>)typeof(Ovl)
      .GetField("relocations", BindingFlags.Instance | BindingFlags.NonPublic)!
      .GetValue(ovl)!;
}

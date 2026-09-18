using System.Reflection;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class FlexiTextureTests {
  [Test]
  public void Decode_UsesAnimationOrderFrameFlagsPaletteAlphaAndOrientation() {
    var fixture = new FlexiTextureFixture();

    var textures = fixture.Decode();
    try {
      using (Assert.EnterMultipleScope()) {
        Assert.That(textures.Fps, Is.EqualTo(12));
        Assert.That(textures.Length, Is.EqualTo(3));
        Assert.That(textures[0].Recolorable, Is.EqualTo(Recolorable.Third));
        Assert.That(textures[1].Recolorable, Is.EqualTo(Recolorable.First));
        Assert.That(textures[2].Recolorable, Is.EqualTo(Recolorable.Third));
        Assert.That(textures[0].Texture[0, 0], Is.EqualTo(new Rgba32(7, 8, 9, 30)));
        Assert.That(textures[1].Texture[0, 0], Is.EqualTo(new Rgba32(1, 2, 3, 255)));
        Assert.That(textures[0].Texture, Is.Not.SameAs(textures[2].Texture));
      }
    }
    finally {
      foreach (var frame in textures.Frames) frame.Texture.Dispose();
    }
  }

  [Test]
  public void Decode_WithoutAnimationUsesStoredFrameOrder() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteHeader(20, 0);

    var textures = fixture.Decode();
    try {
      using (Assert.EnterMultipleScope()) {
        Assert.That(textures.Length, Is.EqualTo(2));
        Assert.That(textures[0].Recolorable, Is.EqualTo(Recolorable.First));
        Assert.That(textures[1].Recolorable, Is.EqualTo(Recolorable.Third));
      }
    }
    finally {
      foreach (var frame in textures.Frames) frame.Texture.Dispose();
    }
  }

  [Test]
  public void Decode_UsesEachFrameDimensions() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteFrame(1, 0, 0);
    fixture.WriteFrame(1, 4, 1);
    fixture.WriteFrame(1, 8, 1);
    fixture.Data[FlexiTextureFixture.Texture1Address] = [1];
    fixture.Data[FlexiTextureFixture.Alpha1Address] = [64];

    var textures = fixture.Decode();
    try {
      using (Assert.EnterMultipleScope()) {
        Assert.That(textures[0].Texture.Width, Is.EqualTo(1));
        Assert.That(textures[0].Texture.Height, Is.EqualTo(1));
        Assert.That(textures[0].Texture[0, 0], Is.EqualTo(new Rgba32(7, 8, 9, 64)));
        Assert.That(textures[1].Texture.Width, Is.EqualTo(2));
        Assert.That(textures[1].Texture.Height, Is.EqualTo(2));
      }
    }
    finally {
      foreach (var frame in textures.Frames) frame.Texture.Dispose();
    }
  }

  [Test]
  public void Load_UsesRelocatedPerFrameDimensionsAndPixelSpans() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteFrame(1, 0, 0);
    fixture.WriteFrame(1, 4, 1);
    fixture.WriteFrame(1, 8, 1);
    fixture.Data[FlexiTextureFixture.Texture1Address] = [1];
    fixture.Data[FlexiTextureFixture.Alpha1Address] = [64];

    var (ovl, file) = fixture.CreateOvl();
    try {
      using (ovl) {
        var textures = FlexiTextureList.Load(ovl, file);
        try {
          using (Assert.EnterMultipleScope()) {
            Assert.That(textures[0].Texture.Width, Is.EqualTo(1));
            Assert.That(textures[0].Texture.Height, Is.EqualTo(1));
            Assert.That(textures[0].Texture[0, 0], Is.EqualTo(new Rgba32(7, 8, 9, 64)));
            Assert.That(textures[1].Texture.Width, Is.EqualTo(2));
            Assert.That(textures[1].Texture.Height, Is.EqualTo(2));
            Assert.That(textures[1].Texture[0, 0], Is.EqualTo(new Rgba32(1, 2, 3, 255)));
            Assert.That(textures[1].Texture[1, 0], Is.EqualTo(new Rgba32(10, 20, 30, 255)));
            Assert.That(textures[1].Texture[0, 1], Is.EqualTo(new Rgba32(10, 20, 30, 255)));
            Assert.That(textures[1].Texture[1, 1], Is.EqualTo(new Rgba32(1, 2, 3, 255)));
          }
        }
        finally {
          foreach (var frame in textures.Frames) frame.Texture.Dispose();
        }
      }
    }
    finally {
      File.Delete(file.Path);
    }
  }

  [Test]
  public void FlexibleTextureTag_MapsOnlyFtx() {
    using (Assert.EnterMultipleScope()) {
      Assert.That("ftx".ToFileType(), Is.EqualTo(FileType.FlexibleTexture));
      Assert.That(FileType.FlexibleTexture.ToTagString(), Is.EqualTo("ftx"));
      Assert.That("flt".ToFileType(), Is.Not.EqualTo(FileType.FlexibleTexture));
    }
  }

  [Test]
  public void FloatTag_IsKnownAndRoundTrips() {
    using (Assert.EnterMultipleScope()) {
      Assert.That("flt".ToFileType(), Is.EqualTo(FileType.Float));
      Assert.That(FileType.Float.ToTagString(), Is.EqualTo("flt"));
      Assert.That(FileType.Float.ToDisplayName(), Is.EqualTo("Floating-Point Number"));
    }
  }

  [TestCase(0)]
  [TestCase(35)]
  public void Decode_RejectsTruncatedHeader(int length) {
    Assert.Throws<InvalidDataException>(new Action(() => FlexiTextureList.Decode(
      "truncated", new byte[length], FlexiTextureFixture.ResourceAddress,
      (_, _) => null, (_, _) => false)));
  }

  [TestCase(0u, 2u, 2u, 1u, TestName = "zero scale")]
  [TestCase(1u, 0u, 0u, 1u, TestName = "zero dimensions")]
  [TestCase(1u, 2u, 4u, 1u, TestName = "non-square dimensions")]
  [TestCase(2u, 2u, 2u, 1u, TestName = "scale mismatch")]
  [TestCase(16u, 65536u, 65536u, 1u, TestName = "overflowing dimensions")]
  [TestCase(1u, 2u, 2u, 8u, TestName = "unknown recolorable flag")]
  public void Decode_RejectsInvalidHeader(uint scale, uint width, uint height, uint recolorable) {
    var fixture = new FlexiTextureFixture();
    fixture.WriteHeader(0, scale);
    fixture.WriteHeader(4, width);
    fixture.WriteHeader(8, height);
    fixture.WriteHeader(16, recolorable);

    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsInvalidFrameMetadata() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteFrame(0, 0, 2);
    AssertInvalid(fixture);

    fixture = new FlexiTextureFixture();
    fixture.WriteFrame(1, 12, 8);
    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsOverflowingCounts() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteHeader(28, uint.MaxValue);
    AssertInvalid(fixture);

    fixture = new FlexiTextureFixture();
    fixture.WriteHeader(20, uint.MaxValue);
    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsTruncatedFrameArray() {
    var fixture = new FlexiTextureFixture();
    fixture.Data[FlexiTextureFixture.FramesAddress] =
      fixture.Data[FlexiTextureFixture.FramesAddress][..^1];

    AssertInvalid(fixture);
  }

  [TestCase(FlexiTextureFixture.Palette1Address)]
  [TestCase(FlexiTextureFixture.Texture1Address)]
  [TestCase(FlexiTextureFixture.Alpha1Address)]
  public void Decode_RejectsTruncatedFrameData(uint address) {
    var fixture = new FlexiTextureFixture();
    fixture.Data[address] = fixture.Data[address][..^1];

    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsBrokenNonNullAlphaPointer() {
    var fixture = new FlexiTextureFixture();
    fixture.Data.Remove(FlexiTextureFixture.Alpha1Address);

    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsUnrelocatedPointers() {
    var fixture = new FlexiTextureFixture();
    fixture.Relocations.Remove(FlexiTextureFixture.ResourceAddress + 32);
    AssertInvalid(fixture);

    fixture = new FlexiTextureFixture();
    fixture.Relocations.Remove(FlexiTextureFixture.FramesAddress + 16);
    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsOutOfRangeAnimationFrame() {
    var fixture = new FlexiTextureFixture();
    fixture.WriteAnimation(0, 2);

    AssertInvalid(fixture);
  }

  [Test]
  public void Decode_RejectsPointerAddressOverflow() {
    var fixture = new FlexiTextureFixture();
    var overflowingAddress = uint.MaxValue - 10;
    fixture.WriteHeader(32, overflowingAddress);
    fixture.Data[overflowingAddress] = fixture.Data[FlexiTextureFixture.FramesAddress];
    fixture.Relocations[FlexiTextureFixture.ResourceAddress + 32] = overflowingAddress;

    AssertInvalid(fixture);
  }

  private static void AssertInvalid(FlexiTextureFixture fixture) =>
    Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));

  private sealed class FlexiTextureFixture {
    public const uint ResourceAddress = 0x1000;
    public const uint AnimationAddress = 0x2000;
    public const uint FramesAddress = 0x3000;
    public const uint Palette0Address = 0x4000;
    public const uint Texture0Address = 0x5000;
    public const uint Palette1Address = 0x6000;
    public const uint Texture1Address = 0x7000;
    public const uint Alpha1Address = 0x8000;

    public byte[] Header { get; } = new byte[36];
    public Dictionary<uint, byte[]> Data { get; } = [];
    public Dictionary<uint, uint> Relocations { get; } = [];

    public FlexiTextureFixture() {
      WriteHeader(0, 1);
      WriteHeader(4, 2);
      WriteHeader(8, 2);
      WriteHeader(12, 12);
      WriteHeader(16, Convert.ToUInt32(Recolorable.First));
      WriteHeader(20, 3);
      WriteHeader(24, AnimationAddress);
      WriteHeader(28, 2);
      WriteHeader(32, FramesAddress);

      var animation = new byte[3 * sizeof(uint)];
      Data[AnimationAddress] = animation;
      WriteUInt32(animation, 0, 1);
      WriteUInt32(animation, 4, 0);
      WriteUInt32(animation, 8, 1);

      var frames = new byte[2 * 28];
      Data[FramesAddress] = frames;
      WriteFrame(frames, 0, Recolorable.First, Palette0Address, Texture0Address, 0);
      WriteFrame(frames, 1, Recolorable.Third, Palette1Address, Texture1Address, Alpha1Address);

      var palette0 = new byte[256 * 4];
      WriteColor(palette0, 0, 1, 2, 3, 4);
      WriteColor(palette0, 1, 10, 20, 30, 40);
      Data[Palette0Address] = palette0;
      Data[Texture0Address] = [1, 0, 0, 1];

      var palette1 = new byte[256 * 4];
      WriteColor(palette1, 0, 4, 5, 6, 7);
      WriteColor(palette1, 1, 7, 8, 9, 10);
      Data[Palette1Address] = palette1;
      Data[Texture1Address] = [0, 1, 1, 0];
      Data[Alpha1Address] = [10, 20, 30, 40];

      Relocations[ResourceAddress + 24] = AnimationAddress;
      Relocations[ResourceAddress + 32] = FramesAddress;
      Relocations[FramesAddress + 16] = Palette0Address;
      Relocations[FramesAddress + 20] = Texture0Address;
      Relocations[FramesAddress + 28 + 16] = Palette1Address;
      Relocations[FramesAddress + 28 + 20] = Texture1Address;
      Relocations[FramesAddress + 28 + 24] = Alpha1Address;
    }

    public FlexiTextureList Decode() => FlexiTextureList.Decode(
      "fixture", Header, ResourceAddress, Resolve, IsRelocatedPointer);

    public (Ovl Ovl, OvlFile File) CreateOvl() {
      var ovl = new Ovl("fixture");
      var file = new OvlFile("fixture", FileType.FlexibleTexture, Path.GetTempFileName());
      File.WriteAllBytes(file.Path, Header);
      ovl.Add(file, new OvlEntry(0, Convert.ToUInt32(Header.Length)));

      var flags = BindingFlags.Instance | BindingFlags.NonPublic;
      var entryDataPtrs = (Dictionary<OvlFile, uint>)typeof(Ovl)
        .GetField("entryDataPtrs", flags)!.GetValue(ovl)!;
      entryDataPtrs[file] = ResourceAddress;

      var fileTypeBlocks = (List<FileTypeBlock[]>)typeof(Ovl)
        .GetField("allFileTypeBlocks", flags)!.GetValue(ovl)!;
      fileTypeBlocks.Add([
        new FileTypeBlock {
          Blocks = [.. Data.Select(pair => new FileBlock {
            Path = file.Path,
            RelativeOffset = pair.Key,
            Size = Convert.ToUInt32(pair.Value.Length),
            Data = pair.Value,
          })],
        },
      ]);

      var relocations = (Dictionary<uint, uint>)typeof(Ovl)
        .GetField("relocations", flags)!.GetValue(ovl)!;
      foreach (var relocation in Relocations)
        relocations.Add(relocation.Key, relocation.Value);

      return (ovl, file);
    }

    public void WriteHeader(int offset, uint value) => WriteUInt32(Header, offset, value);

    public void WriteAnimation(int index, uint value) =>
      WriteUInt32(Data[AnimationAddress], index * sizeof(uint), value);

    public void WriteFrame(int index, int offset, uint value) =>
      WriteUInt32(Data[FramesAddress], index * 28 + offset, value);

    private ReadOnlyMemory<byte>? Resolve(uint address, int length) {
      foreach (var (blockAddress, block) in Data) {
        if (address < blockAddress) continue;
        var offset = Convert.ToUInt64(address) - Convert.ToUInt64(blockAddress);
        if (offset > Convert.ToUInt64(block.Length)) continue;
        var start = Convert.ToInt32(offset);
        if (length > block.Length - start) return null;
        return new ReadOnlyMemory<byte>(block, start, length);
      }
      return null;
    }

    private bool IsRelocatedPointer(uint sourceAddress, uint targetAddress) =>
      Relocations.TryGetValue(sourceAddress, out var value) && value == targetAddress;

    private static void WriteFrame(
      byte[] frames,
      int index,
      Recolorable recolorable,
      uint palette,
      uint texture,
      uint alpha
    ) {
      var offset = index * 28;
      WriteUInt32(frames, offset, 1);
      WriteUInt32(frames, offset + 4, 2);
      WriteUInt32(frames, offset + 8, 2);
      WriteUInt32(frames, offset + 12, Convert.ToUInt32(recolorable));
      WriteUInt32(frames, offset + 16, palette);
      WriteUInt32(frames, offset + 20, texture);
      WriteUInt32(frames, offset + 24, alpha);
    }

    private static void WriteColor(
      byte[] palette, byte index, byte red, byte green, byte blue, byte alpha
    ) {
      var offset = index * 4;
      palette[offset] = blue;
      palette[offset + 1] = green;
      palette[offset + 2] = red;
      palette[offset + 3] = alpha;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value) {
      using var stream = new MemoryStream(bytes);
      stream.Position = offset;
      using var writer = new BinaryWriter(stream);
      writer.Write(value);
    }
  }
}

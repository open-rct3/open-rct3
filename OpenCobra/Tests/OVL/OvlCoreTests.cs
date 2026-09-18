using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class OvlCoreTests {
  private string tempDir = null!;
  private string commonPath = null!;
  private string uniquePath = null!;
  private SyntheticArchive common = null!;
  private SyntheticArchive unique = null!;
  private uint commonResourceAddress;
  private uint uniqueResourceAddress;
  private uint validRelocationSource;
  private uint invalidRelocationSource;
  private uint truncatedRelocationSource;

  [SetUp]
  public void SetUp() {
    tempDir = Directory.CreateTempSubdirectory().FullName;
    commonPath = Path.Combine(tempDir, "core.common.ovl");
    uniquePath = Path.Combine(tempDir, "core.unique.ovl");

    var strings = Encoding.ASCII.GetBytes("\0Common:tex\0Unique:tex\0");
    common = new SyntheticArchive(0, ["btbl", "flic"]);
    common.Blocks[0] = [strings];
    common.Blocks[1] = [
      [0xA0, 0xA1, 0xA2],
      [0xB0, 0xB1, 0xB2, 0xB3],
      new byte[8],
    ];
    common.Blocks[2] = [new byte[16], new byte[40]];

    commonResourceAddress = common.AddressOf(1, 0, 1);
    var validTarget = common.AddressOf(1, 1);
    validRelocationSource = common.AddressOf(1, 2);
    invalidRelocationSource = validRelocationSource + 4;
    truncatedRelocationSource = common.AddressOf(1, 0, 2);
    WriteUInt32(common.Blocks[1][2], 0, validTarget);
    WriteUInt32(common.Blocks[1][2], 4, uint.MaxValue);
    WriteSymbol(common.Blocks[2][0], 1, commonResourceAddress);
    WriteLoaderEntry(common.Blocks[2][1], 0, 0, validTarget);
    WriteLoaderEntry(common.Blocks[2][1], 20, 1, common.AddressOf(1, 0));
    common.Relocations = [
      validRelocationSource,
      invalidRelocationSource,
      truncatedRelocationSource,
    ];

    unique = new SyntheticArchive(common.Size, ["tex"]);
    unique.Blocks[1] = [[0xC0, 0xC1, 0xC2]];
    unique.Blocks[2] = [new byte[16], new byte[20]];
    uniqueResourceAddress = unique.AddressOf(1, 0);
    WriteSymbol(unique.Blocks[2][0], 12, uniqueResourceAddress);
    WriteLoaderEntry(unique.Blocks[2][1], 0, 0, uniqueResourceAddress);

    common.Write(commonPath);
    unique.Write(uniquePath);
  }

  [TearDown]
  public void TearDown() {
    Directory.Delete(tempDir, recursive: true);
  }

  [Test]
  public void Relocations_ResolveAcrossMergedCommonAndUniqueAddressSpace() {
    using var ovl = Ovl.Load(commonPath);

    using (Assert.EnterMultipleScope()) {
      Assert.That(
        ovl.TryResolveRelocation(commonResourceAddress, out var commonData, out var commonOffset),
        Is.True);
      Assert.That(commonData![Convert.ToInt32(commonOffset)], Is.EqualTo(0xA1));
      Assert.That(
        ovl.TryResolveRelocation(uniqueResourceAddress, out var uniqueData, out var uniqueOffset),
        Is.True);
      Assert.That(uniqueData![Convert.ToInt32(uniqueOffset)], Is.EqualTo(0xC0));
      Assert.That(uniqueResourceAddress, Is.EqualTo(common.Size));
      Assert.That(ovl.TryResolveRelocation(0, out _, out _), Is.False);
    }
  }

  [Test]
  public void Relocations_RespectBlockBoundariesAndRejectInvalidTargets() {
    using var ovl = Ovl.Load(commonPath);
    var firstBlockEnd = common.AddressOf(1, 0) + Convert.ToUInt32(common.Blocks[1][0].Length);

    using (Assert.EnterMultipleScope()) {
      Assert.That(ovl.TryReadBytes(firstBlockEnd - 1, 1, out var lastByte), Is.True);
      Assert.That(lastByte, Is.EqualTo(new byte[] { 0xA2 }));
      Assert.That(ovl.TryReadBytes(firstBlockEnd, 1, out var nextByte), Is.True);
      Assert.That(nextByte, Is.EqualTo(new byte[] { 0xB0 }));
      Assert.That(ovl.TryReadBytes(firstBlockEnd - 1, 2, out _), Is.False);
      Assert.That(ovl.TryReadBytes(firstBlockEnd - 1, -1, out _), Is.False);

      Assert.That(ovl.TryGetRelocationSource(validRelocationSource, out var validTarget), Is.True);
      Assert.That(ovl.TryResolveRelocation(validTarget, out _, out _), Is.True);
      Assert.That(
        ovl.TryGetRelocationSource(invalidRelocationSource, out var invalidTarget), Is.True);
      Assert.That(invalidTarget, Is.EqualTo(uint.MaxValue));
      Assert.That(ovl.TryResolveRelocation(invalidTarget, out _, out _), Is.False);
      Assert.That(ovl.TryGetRelocationSource(truncatedRelocationSource, out _), Is.False);
    }
  }

  [Test]
  public void LoaderEntries_PreserveOnDiskOrderAcrossArchivePair() {
    using var ovl = Ovl.Load(commonPath);

    Assert.That(ovl.LoaderEntriesInOrder, Is.EqualTo(new[] {
      new OvlLoaderEntry(
        "btbl", common.AddressOf(1, 1), commonPath, common.AddressOf(2, 1)),
      new OvlLoaderEntry(
        "flic", common.AddressOf(1, 0), commonPath, common.AddressOf(2, 1, 20)),
      new OvlLoaderEntry(
        "tex", uniqueResourceAddress, uniquePath, unique.AddressOf(2, 1)),
    }));
  }

  [Test]
  public void ResourceReads_ReturnOnlyTheResolvedBlockSliceAndRejectInvalidFileRanges() {
    using var ovl = Ovl.Load(commonPath);
    var commonFile = ovl.Find("Common", FileType.Texture);
    var uniqueFile = ovl.Find("Unique", FileType.Texture);

    Assert.That(commonFile, Is.Not.Null);
    Assert.That(uniqueFile, Is.Not.Null);
    using (Assert.EnterMultipleScope()) {
      Assert.That(ovl.ReadResource(commonFile!), Is.EqualTo(new byte[] { 0xA1, 0xA2 }));
      Assert.That(ovl.ReadResource(uniqueFile!), Is.EqualTo(new byte[] { 0xC0, 0xC1, 0xC2 }));
    }

    var invalidFile = new OvlFile("Invalid", FileType.Texture, commonPath);
    ovl.Add(invalidFile, new OvlEntry(uint.MaxValue, 4));
    Assert.That(ovl.ReadResource(invalidFile), Is.Null);
  }

  [Test]
  public void Load_RejectsTruncatedBlockData() {
    var bytes = File.ReadAllBytes(commonPath);
    var relocationTailLength = sizeof(uint) + common.Relocations.Length * sizeof(uint) + sizeof(uint);
    File.WriteAllBytes(commonPath, bytes[..^(relocationTailLength + 1)]);

    Assert.Throws<InvalidDataException>(new Action(() => Ovl.Load(commonPath)));
  }

  [TestCase(0u)]
  [TestCase(2u)]
  [TestCase(6u)]
  public void Load_RejectsUnsupportedVersionBeforeReadingVersionSpecificFields(uint version) {
    using (var stream = File.Create(commonPath))
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false)) {
      writer.Write(0x4B524746u);
      writer.Write(0u);
      writer.Write(version);
    }

    var error = Assert.Throws<InvalidDataException>(new Action(() => Ovl.Load(commonPath)));
    Assert.That(error!.Message, Does.Contain($"Unsupported OVL version {version}"));
  }

  [Test]
  public void Load_RejectsImplausibleBlockCountBeforeMaterializingEntries() {
    using (var stream = File.Create(commonPath))
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false)) {
      writer.Write(0x4B524746u);
      writer.Write(0u);
      writer.Write(4u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(1_000_000_000u);
      writer.Write(0u);
    }

    var error = Assert.Throws<InvalidDataException>(new Action(() => Ovl.Load(commonPath)));
    Assert.That(error!.Message, Does.Contain("block count"));
  }

  [Test]
  public void Load_PreflightsBlockCountAgainstRemainingMetadata() {
    using (var stream = File.Create(commonPath))
    using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false)) {
      writer.Write(0x4B524746u);
      writer.Write(0u);
      writer.Write(4u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(1u);
      writer.Write(0u);
    }

    var error = Assert.Throws<InvalidDataException>(new Action(() => Ovl.Load(commonPath)));
    Assert.That(error!.Message, Does.Contain("block type 0 metadata"));
  }

  private static void WriteSymbol(byte[] data, uint nameAddress, uint dataAddress) {
    WriteUInt32(data, 0, nameAddress);
    WriteUInt32(data, 4, dataAddress);
  }

  private static void WriteLoaderEntry(byte[] data, int offset, uint loaderType, uint dataAddress) {
    WriteUInt32(data, offset, loaderType);
    WriteUInt32(data, offset + 4, dataAddress);
  }

  private static void WriteUInt32(byte[] data, int offset, uint value) =>
    BitConverter.GetBytes(value).CopyTo(data, offset);

  private sealed class SyntheticArchive(uint baseAddress, string[] loaderTags) {
    public byte[][][] Blocks { get; } = Enumerable.Range(0, 9)
      .Select(_ => Array.Empty<byte[]>()).ToArray();
    public uint[] Relocations { get; set; } = [];

    public uint Size => Convert.ToUInt32(
      Blocks.SelectMany(blocks => blocks).Sum(block => block.Length));

    public uint AddressOf(int typeIndex, int blockIndex, int offset = 0) {
      var address = Convert.ToUInt64(baseAddress);
      foreach (var block in Blocks.Take(typeIndex).SelectMany(blocks => blocks))
        address += Convert.ToUInt64(block.Length);
      foreach (var block in Blocks[typeIndex].Take(blockIndex))
        address += Convert.ToUInt64(block.Length);
      address += Convert.ToUInt64(offset);
      return Convert.ToUInt32(address);
    }

    public void Write(string path) {
      using var stream = File.Create(path);
      using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
      writer.Write(0x4B524746u);
      writer.Write(0u);
      writer.Write(4u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(Convert.ToUInt32(loaderTags.Length));
      foreach (var tag in loaderTags) {
        WriteString(writer, $"{tag}_loader");
        WriteString(writer, tag);
        writer.Write(0u);
        WriteString(writer, tag);
      }

      foreach (var typeBlocks in Blocks) {
        writer.Write(Convert.ToUInt32(typeBlocks.Length));
        writer.Write(0u);
        foreach (var block in typeBlocks)
          writer.Write(Convert.ToUInt32(block.Length));
      }

      writer.Write(0u);
      writer.Write(0u);
      foreach (var block in Blocks.SelectMany(blocks => blocks))
        writer.Write(block);

      writer.Write(Convert.ToUInt32(Relocations.Length));
      foreach (var relocation in Relocations)
        writer.Write(relocation);
      writer.Write(0u);
    }

    private static void WriteString(BinaryWriter writer, string value) {
      var bytes = Encoding.ASCII.GetBytes(value);
      writer.Write(Convert.ToUInt16(bytes.Length));
      writer.Write(bytes);
    }
  }
  private const int LoaderStructSize = 20;

  [Test]
  public void Load_RecordsExactLoaderStructAddresses() {
    WithTownHallOvl(ovl => {
      Assert.That(ovl.LoaderEntriesInOrder, Is.Not.Empty);
      foreach (var entry in ovl.LoaderEntriesInOrder) {
        Assert.That(
          ovl.TryReadBytes(entry.StructAddress, LoaderStructSize, out var bytes), Is.True);
        Assert.That(bytes, Has.Length.EqualTo(LoaderStructSize));
        Assert.That(BitConverter.ToUInt32(bytes, 4), Is.EqualTo(entry.DataAddress));
        Assert.That(
          ovl.TryGetRelocationSource(entry.StructAddress + 4, out var dataAddress), Is.True);
        Assert.That(dataAddress, Is.EqualTo(entry.DataAddress));
      }

      foreach (var group in ovl.LoaderEntriesInOrder.GroupBy(entry => entry.SourcePath)) {
        var entries = group.ToList();
        for (var index = 1; index < entries.Count; index++)
          Assert.That(entries[index].StructAddress - entries[index - 1].StructAddress,
            Is.EqualTo(Convert.ToUInt32(LoaderStructSize)));
      }

      foreach (var block in ovl.SymbolReferenceBlocksInOrder) {
        using (Assert.EnterMultipleScope()) {
          Assert.That(block.RecordStride,
            Is.EqualTo(block.Version == OpenCobra.OVL.Version.One ? 12 : 16));
          Assert.That(block.Data.Length,
            Is.EqualTo(Convert.ToInt32(block.RecordCount) * block.RecordStride));
        }
      }

      var shapeFile = ovl.Keys.Single(file =>
        file.Name == "RS-TownHall" && file.Type == FileType.StaticShape);
      Assert.That(ovl.TryGetDataPointer(shapeFile, out var shapeAddress), Is.True);
      var shapeLoader = ovl.LoaderEntriesInOrder.Single(entry =>
        entry.DataAddress == shapeAddress && entry.Tag.ToFileType() == FileType.StaticShape);
      using (Assert.EnterMultipleScope()) {
        Assert.That(shapeLoader.StructAddress, Is.Not.Zero);
        Assert.That(shapeLoader.SourcePath, Does.EndWith("fixture.unique.ovl"));
      }
    });
  }

  [Test]
  public void Dispose_ClearsInternalLoaderAndSymbolReferenceIndexes() {
    WithTownHallOvl(ovl => {
      var loaderEntries = ovl.LoaderEntriesInOrder;
      var symbolReferenceBlocks = ovl.SymbolReferenceBlocksInOrder;
      using (Assert.EnterMultipleScope()) {
        Assert.That(loaderEntries, Is.Not.Empty);
        Assert.That(symbolReferenceBlocks, Is.Not.Empty);
      }

      ovl.Dispose();

      using (Assert.EnterMultipleScope()) {
        Assert.That(loaderEntries, Is.Empty);
        Assert.That(symbolReferenceBlocks, Is.Empty);
      }
    });
  }

  [Test]
  public void Load_SkyBeamZeroSymbolReferences_IgnoresUnrelatedThirdType2Block() {
    WithEmbeddedOvl(".CFRs.SkyBeam.Style.common.ovl", ovl => {
      Assert.That(ovl.Count, Is.Positive);
      Assert.That(ovl.SymbolReferenceBlocksInOrder.Where(block =>
        block.SourcePath.EndsWith("Style.common.ovl", StringComparison.OrdinalIgnoreCase)),
        Is.Empty);
    });
  }

  private static void WithTownHallOvl(Action<Ovl> action) =>
    WithEmbeddedOvl(".RS-TownHall.common.ovl", action);

  private static void WithEmbeddedOvl(string commonResourceSuffix, Action<Ovl> action) {
    var assembly = typeof(OvlCoreTests).Assembly;
    var resources = assembly.GetManifestResourceNames();
    var commonResource = resources.Single(name =>
      name.EndsWith(commonResourceSuffix, StringComparison.OrdinalIgnoreCase));
    var uniqueResource = commonResource[..^".common.ovl".Length] + ".unique.ovl";
    var tempDir = Directory.CreateTempSubdirectory().FullName;
    try {
      var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
      CopyResource(assembly, commonResource, commonPath);
      CopyResource(assembly, uniqueResource, Path.Combine(tempDir, "fixture.unique.ovl"));
      using var ovl = Ovl.Load(commonPath);
      action(ovl);
    } finally {
      Directory.Delete(tempDir, recursive: true);
    }
  }

  private static void CopyResource(
    System.Reflection.Assembly assembly,
    string resourceName,
    string path
  ) {
    using var input = assembly.GetManifestResourceStream(resourceName);
    Assert.That(input, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
    using var output = File.Create(path);
    input.CopyTo(output);
  }
}

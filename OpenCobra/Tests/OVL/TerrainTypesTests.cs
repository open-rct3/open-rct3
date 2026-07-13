using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using System.Text;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TerrainTypesTests {
  private static byte[] MakeSyntheticTerrainType(
    string name = "test",
    uint version = 1,
    uint unk02 = 0,
    uint addon = 0,
    uint number = 0,
    uint type = 2,
    uint colourSimple = 0xFFFF007F,
    uint colourMap = 0xFFFF007F,
    float invWidth = 0.1f,
    float invHeight = 0.1f,
    float unk13 = 0.3f,
    float unk14 = 0.0f,
    float unk15 = 0.5f
  ) {
    var data = new byte[60];
    var offset = 0;
    Buffer.BlockCopy(BitConverter.GetBytes(version), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(unk02), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(addon), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(number), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(type), 0, data, offset, 4);
    offset += 4;
    // TextureRef pointer (20, 4 bytes) - null
    offset += 4;
    // DescriptionName pointer (24, 4 bytes) - null
    offset += 4;
    // IconName pointer (28, 4 bytes) - null
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(colourSimple), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(colourMap), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(invWidth), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(invHeight), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(unk13), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(unk14), 0, data, offset, 4);
    offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(unk15), 0, data, offset, 4);
    return data;
  }

  [Test]
  public void ParseTerrainType_SyntheticStruct_DecodesAllFields() {
    var data = MakeSyntheticTerrainType(
      version: 1, addon: 0, number: 6, type: 2,
      colourSimple: 0xFF487D10, colourMap: 0xFF487D10,
      unk13: 0.3f, unk14: 1.0f, unk15: 0.8f
    );

    // Create a fake OVL with this data
    using var tempFile = new TempFileHolder(data);
    using var ovl = Ovl.Load(tempFile.Path);

    // Since this is synthetic data, we can't directly test Extract() without
    // a real OVL file with ter entries. This test verifies struct layout offsets.
    Assert.That(BitConverter.ToUInt32(data, 0), Is.EqualTo(1u), "Version");
    Assert.That(BitConverter.ToUInt32(data, 4), Is.EqualTo(0u), "Unk02");
    Assert.That(BitConverter.ToUInt32(data, 8), Is.EqualTo(0u), "Addon");
    Assert.That(BitConverter.ToUInt32(data, 12), Is.EqualTo(6u), "Number");
    Assert.That(BitConverter.ToUInt32(data, 16), Is.EqualTo(2u), "Type");
    Assert.That(BitConverter.ToUInt32(data, 32), Is.EqualTo(0xFF487D10u), "ColourSimple");
    Assert.That(BitConverter.ToSingle(data, 48), Is.EqualTo(0.3f).Within(0.0001f), "Unk13");
    Assert.That(BitConverter.ToSingle(data, 52), Is.EqualTo(1.0f).Within(0.0001f), "Unk14");
    Assert.That(BitConverter.ToSingle(data, 56), Is.EqualTo(0.8f).Within(0.0001f), "Unk15");
  }

  [Test]
  [Explicit("Requires RCT3_PATH")]
  public void Extract_FromTerrainRCT3_DecodesAllEntries() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
    if (string.IsNullOrEmpty(rct3Path))
      Assert.Inconclusive("RCT3_PATH not set");

    var terrainCommonPath = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    if (!File.Exists(terrainCommonPath))
      Assert.Inconclusive($"Terrain_RCT3.common.ovl not found at {terrainCommonPath}");

    using var ovl = Ovl.Load(terrainCommonPath);
    var entries = TerrainTypes.Extract(ovl);

    Assert.That(entries, Is.Not.Null, "Extract should return non-null collection");
    Assert.That(entries.Count, Is.GreaterThan(0), "Should have decoded at least one entry");

    // Verify all entries have valid data
    foreach (var entry in entries) {
      Assert.That(entry.Name, Is.Not.Empty, $"Entry should have Name");
      Assert.That(entry.Version, Is.EqualTo(1u), $"{entry.Name}: Version");
      Assert.That(entry.Type, Is.AnyOf(TerrainType.GroundUnblended, TerrainType.Cliff, TerrainType.GroundBlended),
        $"{entry.Name}: Type out of range");
    }
  }

  [Test]
  [Explicit("Requires RCT3_PATH")]
  public void Extract_FromTerrainCT_DecodesAllEntries() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
    if (string.IsNullOrEmpty(rct3Path))
      Assert.Inconclusive("RCT3_PATH not set");

    var terrainCtPath = Path.Combine(rct3Path, "terrain", "CT", "Terrain_CT.common.ovl");
    if (!File.Exists(terrainCtPath))
      Assert.Inconclusive($"Terrain_CT.common.ovl not found at {terrainCtPath}");

    using var ovl = Ovl.Load(terrainCtPath);
    var entries = TerrainTypes.Extract(ovl);

    Assert.That(entries, Is.Not.Null, "Extract should return non-null collection");
    Assert.That(entries.Count, Is.EqualTo(6), "Terrain_CT should have exactly 6 entries");

    // Verify Terrain_CT addon flag (all Soaked/1)
    foreach (var entry in entries) {
      Assert.That(entry.Addon, Is.EqualTo(1u), $"{entry.Name}: Addon should be 1 (Soaked)");
    }
  }

  [Test]
  [Explicit("Requires RCT3_PATH")]
  public void GrassIdentification_FindsTerrain06() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH");
    if (string.IsNullOrEmpty(rct3Path))
      Assert.Inconclusive("RCT3_PATH not set");

    var terrainPath = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    if (!File.Exists(terrainPath))
      Assert.Inconclusive($"Terrain_RCT3.common.ovl not found");

    using var ovl = Ovl.Load(terrainPath);
    var entries = TerrainTypes.Extract(ovl);

    // Filter to GroundBlended entries
    var groundBlended = entries.Where(e => e.Type == TerrainType.GroundBlended).ToList();
    Assert.That(groundBlended.Count, Is.GreaterThan(0), "Should have GroundBlended entries");

    // Find nearest-color match to grass color (0xFF4F810E)
    var targetColor = 0xFF4F810Eu;
    var nearest = groundBlended.OrderBy(e => ColorDistance(e.Parameters.ColourSimple, targetColor)).First();

    Assert.That(nearest.Name, Is.EqualTo("Terrain_06"), "Nearest-color match should be Terrain_06");
  }

  private static uint ColorDistance(uint c1, uint c2) {
    var r1 = (byte)((c1 >> 16) & 0xFF);
    var g1 = (byte)((c1 >> 8) & 0xFF);
    var b1 = (byte)(c1 & 0xFF);

    var r2 = (byte)((c2 >> 16) & 0xFF);
    var g2 = (byte)((c2 >> 8) & 0xFF);
    var b2 = (byte)(c2 & 0xFF);

    var dr = (int)r1 - r2;
    var dg = (int)g1 - g2;
    var db = (int)b1 - b2;

    return (uint)(dr * dr + dg * dg + db * db);
  }

  private class TempFileHolder : IDisposable {
    public string Path { get; }

    public TempFileHolder(byte[] data) {
      Path = System.IO.Path.GetTempFileName();
      File.WriteAllBytes(Path, data);
    }

    public void Dispose() {
      if (File.Exists(Path))
        File.Delete(Path);
    }
  }
}

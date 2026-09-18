using System.Collections.Generic;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class TerrainTypesIntegrationTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void InstalledTerrainPairs_DecodeTypedReferences() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var commonPaths = InstalledPairs(rct3Path).ToList();
    Assert.That(commonPaths, Is.Not.Empty, "No installed OVL pairs found.");

    var terrainPairCount = 0;
    var terrainEntryCount = 0;
    foreach (var commonPath in commonPaths) {
      using var ovl = Ovl.Load(commonPath);
      var terrainEntries = ovl.Keys.Where(file => file.Type == FileType.TerrainType).ToList();
      if (terrainEntries.Count == 0) continue;

      var pairName = Path.GetRelativePath(rct3Path, commonPath);
      IReadOnlyList<TerrainType> terrains;
      try {
        terrains = TerrainTypes.Extract(ovl);
      } catch (Exception exception) {
        Assert.Fail($"{pairName}: terrain decoding failed: {exception}");
        return;
      }

      terrainPairCount++;
      terrainEntryCount += terrainEntries.Count;
      TestContext.Progress.WriteLine(
        $"{pairName}: {terrainEntries.Count} TER entries, {terrains.Count} decoded; " +
        $"addons={string.Join(',', terrains.Select(terrain => terrain.Addon).Distinct().Order())}");

      Assert.That(terrains, Has.Count.EqualTo(terrainEntries.Count), pairName);
      Assert.That(
        terrains.Select(terrain => terrain.Name),
        Is.EquivalentTo(terrainEntries.Select(entry => entry.Name)),
        $"{pairName}: decoded names must preserve the source TER resource names");
      using (Assert.EnterMultipleScope()) {
        foreach (var terrain in terrains) {
          var context = $"{pairName}/{terrain.Name}";
          Assert.That(terrain.Name, Is.Not.Empty, context);
          Assert.That(Enum.IsDefined(typeof(Addon), terrain.Addon), Is.True, context);
          Assert.That(Enum.IsDefined(typeof(TerrainTypeKind), terrain.Type), Is.True, context);
          Assert.That(terrain.Description.Type, Is.EqualTo(FileType.Text), context);
          Assert.That(terrain.Description.Name, Is.Not.Empty, context);
          Assert.That(terrain.Icon.Type, Is.EqualTo(FileType.GuiSkinItem), context);
          Assert.That(terrain.Icon.Name, Is.Not.Empty, context);
          Assert.That(terrain.Texture.Type, Is.EqualTo(FileType.Texture), context);
          Assert.That(terrain.Texture.Name, Is.Not.Empty, context);
          Assert.That(
            ovl.Keys.Any(file => file.Type == FileType.Texture && file.Name == terrain.Texture.Name),
            Is.True,
            $"{context}: missing texture resource {terrain.Texture.QualifiedName}");
        }
      }
    }

    TestContext.Progress.WriteLine(
      $"Installed TER scan: {commonPaths.Count} OVL pairs examined; " +
      $"{terrainPairCount} pairs contained {terrainEntryCount} decoded entries.");
    Assert.That(terrainPairCount, Is.GreaterThan(0), "No installed OVL pair contains TER entries.");
    Assert.That(terrainEntryCount, Is.GreaterThan(0), "No installed TER entries were decoded.");
  }

  private static IEnumerable<string> InstalledPairs(string rct3Path) {
    const string commonSuffix = ".common.ovl";
    foreach (var commonPath in Directory.EnumerateFiles(
      rct3Path, $"*{commonSuffix}", SearchOption.AllDirectories)) {
      var uniquePath = commonPath[..^commonSuffix.Length] + ".unique.ovl";
      if (File.Exists(uniquePath)) yield return commonPath;
    }
  }
}

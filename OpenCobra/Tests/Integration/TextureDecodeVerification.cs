// TextureDecodeVerification
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Verifies the relocation-fixup table + Tex.FlicPtr two-hop chase fix against two known single-file
// repro targets, without needing the full 7,490-file scan.
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class TextureDecodeVerification {
  private static readonly string[] MainTexturelessRuntimeTargets = [
    "DrawSolidColour",
    "DrawSolidColourOpaque",
    "GUIRendererBitmap",
    "GUIRendererColour",
    "GUIRendererZMask",
    "TerrainDebug",
    "TerrainDetailAndLightmap",
    "TerrainGrid2StageDummy",
  ];

  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void MainCommonOvl_DecodesAllBackedTexEntries() {
    var rct3 = Rct3Path()!;
    var mainPath = Path.Combine(rct3, "Main.common.ovl");
    Assert.That(File.Exists(mainPath), Is.True, $"Main.common.ovl not found at: {mainPath}");

    using var ovl = Ovl.Load(mainPath);
    var texEntries = ovl.Keys.Where(key => key.Type == FileType.Texture).ToList();
    using var textures = Textures.Extract(ovl);
    var decodedNames = textures.Names.ToHashSet();
    var decodedTexNames = texEntries.Select(entry => entry.ToString()).Where(decodedNames.Contains).ToList();
    var textureless = texEntries.Where(entry => !decodedNames.Contains(entry.ToString())).ToList();

    TestContext.Out.WriteLine(
      $"Main.common.ovl: {texEntries.Count} Texture entries, {decodedTexNames.Count} genuine entries decoded");
    Assert.That(textureless.Select(entry => entry.Name),
      Is.EquivalentTo(MainTexturelessRuntimeTargets),
      "The undecoded Main TEX entries changed; investigate instead of accepting another 76/84 split");
    foreach (var entry in textureless) {
      Assert.That(ovl.TryGetDataPointer(entry, out var texAddress), Is.True);
      var bytes = ovl.ReadResource(entry);
      Assert.That(bytes, Is.Not.Null.And.Length.GreaterThanOrEqualTo(56));
      var rawFlicPtr = BitConverter.ToUInt32(bytes, 52);
      var hasFlicRelocation = ovl.TryGetRelocationSource(texAddress + 52, out _);
      TestContext.Out.WriteLine(
        $"Textureless: {entry.Name}; raw FlicPtr={rawFlicPtr:X}; relocation={hasFlicRelocation}");
      Assert.That(rawFlicPtr, Is.Zero,
        $"Textureless runtime target '{entry}' must have a zero raw FLIC pointer");
      Assert.That(hasFlicRelocation, Is.False,
        $"Undecoded texture '{entry}' has a backing FLIC relocation and needs investigation");
    }

    Assert.That(texEntries, Has.Count.EqualTo(84), "Expected 84 Texture entries per bug doc Part 4");
    Assert.That(decodedTexNames, Has.Count.EqualTo(76));
    Assert.That(textureless, Has.Count.EqualTo(8),
      "Expected the remaining entries to be textureless TEX records without FLIC relocations");
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void Af01BodyMain_StandaloneTexEntry_Decodes() {
    var rct3 = Rct3Path()!;
    var path = Path.Combine(rct3, "Characters", "AF", "AF01_Body_Main.common.ovl");
    Assert.That(File.Exists(path), Is.True, $"AF01_Body_Main.common.ovl not found at: {path}");

    using var ovl = Ovl.Load(path);
    // Per bug doc Part 5: this file's mms/prt entries are conclusively out of scope (not
    // texture-shaped data) - only its one genuine, non-mms/prt "tex" entry is in scope here.
    var texEntries = ovl.Keys.Where(key => key.Type == FileType.Texture).ToList();
    TestContext.Out.WriteLine($"AF01_Body_Main.common.ovl: {texEntries.Count} genuine Texture entries: " +
      string.Join(", ", texEntries.Select(e => e.Name)));

    Assert.That(texEntries, Is.Not.Empty, "Expected at least one genuine tex-tagged entry");

    using var textures = Textures.Extract(ovl);
    TestContext.Out.WriteLine($"Decoded: {string.Join(", ", textures.Names)}");
    var decodedTexNames = texEntries.Select(entry => entry.ToString()).Intersect(textures.Names).ToList();

    Assert.That(texEntries, Has.Count.EqualTo(1));
    Assert.That(decodedTexNames, Has.Count.EqualTo(1),
      "Expected the genuine standalone TEX entry to remain decodable");
  }
}

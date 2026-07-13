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
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void MainCommonOvl_TexEntries_DecodeAfterRelocationFix() {
    var rct3 = Rct3Path()!;
    var mainPath = Path.Combine(rct3, "Main.common.ovl");
    Assert.That(File.Exists(mainPath), Is.True, $"Main.common.ovl not found at: {mainPath}");

    using var ovl = Ovl.Load(mainPath);
    var texEntryCount = ovl.Keys.Count(key => key.Type == FileType.Texture);
    var textures = Textures.Extract(ovl);

    TestContext.Out.WriteLine($"Main.common.ovl: {texEntryCount} Texture entries, {textures.Count} decoded");

    Assert.That(texEntryCount, Is.EqualTo(84), "Expected 84 Texture entries per bug doc Part 4");
    Assert.That(textures.Count, Is.GreaterThan(0),
      "Expected at least some of Main.common.ovl's 84 Texture entries to decode after the relocation-table fix");
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void Af01BodyMain_GenuineTexEntry_DecodesAfterRelocationFix() {
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

    var textures = Textures.Extract(ovl);
    TestContext.Out.WriteLine($"Decoded: {string.Join(", ", textures.Names)}");

    Assert.That(textures.Count, Is.GreaterThan(0),
      "Expected the genuine tex entry to decode after the relocation-table fix");
  }
}

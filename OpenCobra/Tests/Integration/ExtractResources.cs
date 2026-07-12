// ExtractResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class ExtractResources {
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void Load_NullbmpFtx_ExtractsFlexibleTexture() {
    var rct3 = Rct3Path()!;

    var commonPath = Path.Combine(rct3, "nullbmp.common.ovl");
    Assert.That(File.Exists(commonPath), Is.True, $"nullbmp.common.ovl not found at: {commonPath}");

    var resources = Ovl.Load(commonPath);
    Assert.That(resources, Is.Not.Empty, "No resources found in nullbmp.common.ovl");

    var ftxEntry = resources.FirstOrDefault(e => e.Key.Type == FileType.FlexibleTexture);
    Assert.That(ftxEntry, Is.Not.Default, "No FlexibleTexture (ftx) resource found");

    var bytes = resources.ReadResource(ftxEntry.Key);
    Assert.That(bytes, Is.Not.Null);

    var asString = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 16));
    using (Assert.EnterMultipleScope()) {
      // Must NOT be the symbol name string
      Assert.That(asString, Does.Not.Contain("nullbmp:"),
          "Load returned the symbol name string instead of texture data");

      // FlexiTextureInfoStruct: scale(u32), width(u32), height(u32), ...
      // Valid FTX textures are square powers of two (width == height, both power-of-two)
      Assert.That(bytes, Has.Length.GreaterThanOrEqualTo(12), "Too short to contain FTX width/height");
      var width  = BitConverter.ToUInt32(bytes, 4);
      var height = BitConverter.ToUInt32(bytes, 8);
      Assert.That(width, Is.EqualTo(height), "FTX width and height must be equal (square texture)");
      Assert.That(width == 0 || (width & (width - 1)) == 0,
        $"FTX width {width} is not a power of two");
    }

    // `offset1` and `fts2` in FlexiTextureInfoStruct are relocated pointers, not inline data, so
    // fully decoding the texture (palette, indexed pixels, alpha mask) requires following them
    // through Ovl.TryResolveRelocation rather than reading the resource's raw bytes sequentially.
    var collection = FlexiTextureList.Load(resources, ftxEntry.Key);
    using (Assert.EnterMultipleScope()) {
      Assert.That(collection.Count, Is.GreaterThan(0), "FlexiTexture must have at least one frame");

      var frame = collection.First();
      var frameMip = frame.MipLevels[0];
      Assert.That(frameMip, Is.Not.Null);
      Assert.That(Convert.ToInt32(frame.Width), Is.GreaterThan(0).And.EqualTo(Convert.ToInt32(frame.Height)));
      Assert.That(frameMip!.Width, Is.EqualTo(Convert.ToInt32(frame.Width)));
      Assert.That(frameMip.Height, Is.EqualTo(Convert.ToInt32(frame.Height)));

      // Pixels must vary; a resolver bug landing on the wrong block tends to produce either a
      // crash, a single repeated color, or the symbol name string reinterpreted as pixel data.
      var distinctPixels = new HashSet<Rgba32>();
      for (var y = 0; y < frameMip.Height && distinctPixels.Count < 2; y++)
        for (var x = 0; x < frameMip.Width && distinctPixels.Count < 2; x++)
          distinctPixels.Add(frameMip[x, y]);
      Assert.That(distinctPixels, Has.Count.GreaterThan(1), "Decoded FTX texture has no pixel variation");
    }
  }

  [Test]
  public void Load_ShapesCommon_HasResources() {
    var rct3 = Rct3Path();
    if (rct3 == null)
      Assert.Ignore("Cannot find RCT3. Skipping integration test.");

    var shapesPath = Path.Combine(rct3, "test", "Shapes.common.ovl");
    Assert.That(File.Exists(shapesPath), Is.True, $"Shapes.common.ovl not found at: {shapesPath}");

    var resources = Ovl.Load(shapesPath);
    Assert.That(resources, Is.Not.Empty, "No resources found in Shapes.common.ovl");
  }

  private static IEnumerable<TestCaseData> GetOvlFixtures() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);

    var rct3Path = Rct3Path();
    if (string.IsNullOrEmpty(rct3Path)) {
      yield return new TestCaseData(string.Empty)
        .Explicit("Cannot find RCT3. Skipping integration test.")
        .Ignore("Cannot find RCT3. Skipping integration test.");
      yield break;
    }

    var files = Directory.GetFiles(rct3Path, "*.ovl", SearchOption.AllDirectories);
    if (files.Length == 0) {
      yield return new TestCaseData(string.Empty).Ignore("No OVL fixtures found.");
      yield break;
    }

    foreach (var file in files)
      yield return new TestCaseData(file);
  }

  // shs (StaticShape) data is confirmed duplicated identically across a pack's common.ovl/
  // unique.ovl halves (see ovl-static-shapes.md's Production OVLs section) - scanning *.unique.ovl
  // only halves the runtime for zero extra coverage versus scanning both halves of every pack.
  private static IEnumerable<TestCaseData> GetUniqueOvlFixtures() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);

    var rct3Path = Rct3Path();
    if (string.IsNullOrEmpty(rct3Path)) {
      yield return new TestCaseData(string.Empty)
        .Explicit("Cannot find RCT3. Skipping integration test.")
        .Ignore("Cannot find RCT3. Skipping integration test.");
      yield break;
    }

    var files = Directory.GetFiles(rct3Path, "*.unique.ovl", SearchOption.AllDirectories);
    if (files.Length == 0) {
      yield return new TestCaseData(string.Empty).Ignore("No unique.ovl fixtures found.");
      yield break;
    }

    foreach (var file in files)
      yield return new TestCaseData(file);
  }

  [TestCaseSource(nameof(GetUniqueOvlFixtures))]
  public void StaticShapes_AreDecodable(string ovlPath) {
    Assert.That(ovlPath, Does.Exist, $"OVL not found: {ovlPath}");

    using var ovl = Ovl.Load(ovlPath);
    var shsCount = ovl.Keys.Count(key => key.Type == FileType.StaticShape);
    if (shsCount == 0)
      Assert.Ignore($"No StaticShape (shs) resources found in {Path.GetFileName(ovlPath)}.");

    var shapes = StaticShapes.Extract(ovl);
    using (Assert.EnterMultipleScope()) {
      // A symbol that fails to decode is dropped from the result rather than throwing (see
      // StaticShapes.Extract's failures-bag handling) - assert nothing was silently dropped,
      // since every shs symbol in this fixture is already known to decode (manually verified
      // during implementation against test/Shapes.unique.ovl and ACAM/ACAM.unique.ovl).
      Assert.That(shapes, Has.Count.EqualTo(shsCount),
        $"Expected every shs symbol in {Path.GetFileName(ovlPath)} to decode; some were dropped as failures.");

      foreach (var shape in shapes) {
        // A shape can legitimately have zero meshes - confirmed against real data (e.g.
        // "invisibleproxy" in Enclosures/Shelters, and vehicle "Bogey"/"_ME" track-joint pieces),
        // not a decode failure. Only assert non-empty Vertices/Triangles for shapes that do have
        // meshes; a mesh-less shape has nothing further to check here.
        if (shape.Meshes.Count == 0) continue;
        Assert.That(shape.Meshes.Any(m => m.Vertices.Count > 0), Is.True,
          $"{shape.Name}: expected at least one mesh with non-empty Vertices");
        Assert.That(shape.Meshes.Any(m => m.Triangles.Count > 0), Is.True,
          $"{shape.Name}: expected at least one mesh with non-empty Triangles");
      }
    }
  }

  [Test]
  public void Load_ACAMHull_DecodesRealShapeWithGenuinelyNullSymbolRefs() {
    var rct3 = Rct3Path();
    if (rct3 == null)
      Assert.Ignore("Cannot find RCT3. Skipping integration test.");

    var acamPath = Path.Combine(rct3, "ACAM", "ACAM.unique.ovl");
    Assert.That(File.Exists(acamPath), Is.True, $"ACAM.unique.ovl not found at: {acamPath}");

    using var ovl = Ovl.Load(acamPath);
    var file = ovl.Find("ACAMHull", FileType.StaticShape);
    Assert.That(file, Is.Not.Null, "ACAMHull symbol not found");

    var shape = StaticShapes.TryExtractOne(ovl, file!);
    Assert.That(shape, Is.Not.Null, "Failed to decode ACAMHull");

    using (Assert.EnterMultipleScope()) {
      Assert.That(shape!.Value.Meshes, Has.Count.EqualTo(3), "ACAMHull is known to have 3 meshes");
      Assert.That(shape.Value.Meshes.Sum(m => m.Vertices.Count), Is.EqualTo(305),
        "ACAMHull is known to have 305 total vertices");
      Assert.That(shape.Value.Meshes.Sum(m => m.Triangles.Count), Is.EqualTo(486),
        "ACAMHull is known to have 486 total triangles");
      Assert.That(shape.Value.Effects, Has.Count.EqualTo(5), "ACAMHull is known to have 5 effects");
      // ACAMHull's mesh 0 has no ftx_ref/txs_ref listed in the relocation table at all (confirmed
      // during implementation, not assumed) - a genuinely absent reference, not a resolution bug.
      Assert.That(shape.Value.Meshes[0].FtxRef, Is.Null);
      Assert.That(shape.Value.Meshes[0].TxsRef, Is.Null);
    }
  }

  /// <remarks>
  /// A small number of real archives (e.g. "MapColourRed" in Style.common.ovl, "PlatformHeight" in
  /// tracks/Platforms/Vanilla/*.ovl) have non-texture symbols mislabeled as FileType.FlexibleTexture,
  /// a separate pre-existing resource-classification bug (tracked as a follow-up, in the same family as
  /// ".agents/bugs/ovl-resource-relocation.md"). Those entries fail even the cheap raw-header
  /// plausibility check (square, power-of-two dimensions) that every real FTX resource passes, so they
  /// are skipped here rather than asserted on.
  /// </remarks>
  [TestCaseSource(nameof(GetOvlFixtures))]
  public void FtxResources_AreDecodable(string ovlPath) {
    Assert.That(ovlPath, Does.Exist, $"OVL not found: {ovlPath}");

    using var ovl = Ovl.Load(ovlPath);
    var ftxEntries = ovl.Keys.Where(key => key.Type == FileType.FlexibleTexture).ToList();
    if (ftxEntries.Count == 0)
      Assert.Ignore($"No FlexibleTexture (ftx) resources found in {Path.GetFileName(ovlPath)}.");

    using (Assert.EnterMultipleScope()) {
      foreach (var entry in ftxEntries) {
        var bytes = ovl.ReadResource(entry);
        if (bytes == null || bytes.Length < 12) continue;

        var width = BitConverter.ToUInt32(bytes, 4);
        var height = BitConverter.ToUInt32(bytes, 8);
        var isPlausibleFtxHeader = width == height && width > 0 && (width & (width - 1)) == 0;
        if (!isPlausibleFtxHeader) continue;

        var collection = FlexiTextureList.Load(ovl, entry);
        Assert.That(collection.Count, Is.GreaterThan(0),
          $"{entry.Name} in {Path.GetFileName(ovlPath)}: expected at least one decoded frame");
        var frame = collection.First();
        Assert.That(Convert.ToInt32(frame.Width), Is.EqualTo(Convert.ToInt32(width)),
          $"{entry.Name} in {Path.GetFileName(ovlPath)}: decoded width does not match header");
        Assert.That(Convert.ToInt32(frame.Height), Is.EqualTo(Convert.ToInt32(height)),
          $"{entry.Name} in {Path.GetFileName(ovlPath)}: decoded height does not match header");
      }
    }
  }
}

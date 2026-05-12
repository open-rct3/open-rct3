// ExtractResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OVL.Tests;

[TestFixture]
public class ExtractResources {
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  public void Load_NullbmpFtx_ExtractsFlexibleTexture() {
    var rct3 = Rct3Path();
    if (rct3 == null)
      Assert.Ignore("Cannot find RCT3. Skipping integration test.");

    var commonPath = Path.Combine(rct3, "nullbmp.common.ovl");
    Assert.That(File.Exists(commonPath), Is.True, $"nullbmp.common.ovl not found at: {commonPath}");

    var resources = Ovl.Load(commonPath);
    Assert.That(resources, Is.Not.Empty, "No resources found in nullbmp.common.ovl");

    var ftxEntry = resources.FirstOrDefault(e => e.Key.Type == FileType.FlexibleTexture);
    Assert.That(ftxEntry, Is.Not.Default, "No FlexibleTexture (ftx) resource found");

    var bytes = Ovl.ReadResource(resources, ftxEntry.Key);
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
}

// ExtractResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using DotNetEnv;
using OpenCobra.OVL;
using File = System.IO.File;

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
  public void GetResourceBytes_NullbmpFtx_ReturnsBitmapData() {
    var rct3 = Rct3Path();
    if (rct3 == null)
      Assert.Ignore("RCT3_PATH environment variable not set — skipping integration test");

    var commonPath = Path.Combine(rct3, "nullbmp.common.ovl");
    Assert.That(File.Exists(commonPath), Is.True, $"nullbmp.common.ovl not found at: {commonPath}");

    var ovl = Ovl.Load(commonPath);

    var entry = ovl.LoaderEntries.FirstOrDefault(e => e.Tag == "ftx");
    Assert.That(entry, Is.Not.Default, "No ftx loader entry found in nullbmp.common.ovl");

    var bytes = ovl.GetResourceBytes(entry);
    Assert.That(bytes, Is.Not.Null, "GetResourceBytes returned null");

    var asString = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 16));
    using (Assert.EnterMultipleScope()) {
      // Must NOT be the symbol name string "nullbmp:ftx"
      Assert.That(asString, Does.Not.Contain("nullbmp:ftx"),
          "GetResourceBytes returned the symbol name string instead of bitmap data");

      // FlexiTextureInfoStruct: scale(u32), width(u32), height(u32), ...
      // Valid FTX textures are square powers of two (width == height, both power-of-two)
      Assert.That(bytes!, Has.Length.GreaterThanOrEqualTo(12), "Too short to contain FTX width/height");
      var width  = BitConverter.ToUInt32(bytes, 4);
      var height = BitConverter.ToUInt32(bytes, 8);
      Assert.That(width, Is.EqualTo(height), "FTX width and height must be equal (square texture)");
      Assert.That(width == 0 || (width & (width - 1)) == 0,
        $"FTX width {width} is not a power of two");
    }
  }
}

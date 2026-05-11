// ListResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using DotNetEnv;
using OpenCobra.OVL;

namespace OVL.Tests;

[TestFixture]
public class ListResources {
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  private static string[] GetOvlFixtures() {
    var rct3Path = Rct3Path();
    if (string.IsNullOrEmpty(rct3Path) || !Directory.Exists(rct3Path))
      return [];

    return Directory.GetFiles(rct3Path, "*.common.ovl", SearchOption.AllDirectories);
  }

  [TestCaseSource(nameof(GetOvlFixtures))]
  public void LoadOvlPair(string ovlPath) {
    Assert.That(ovlPath, Does.Exist, $"OVL not found: {ovlPath}");

    var result = Ovl.Load(ovlPath);
    Assert.That(result, Is.Not.Null);
  }
}

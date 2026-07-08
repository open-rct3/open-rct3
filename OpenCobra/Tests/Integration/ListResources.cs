// ListResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using DotNetEnv;
using OpenCobra.OVL;
using System.Collections.Generic;

namespace OVL.Tests;

[TestFixture]
public class ListResources {
  private static readonly string cannotFindRct3 = "Cannot find RCT3. Skipping integration test.";
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [OneTimeSetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  private static IEnumerable<TestCaseData> GetOvlFixtures() {
    var rct3Path = Rct3Path();
    if (string.IsNullOrEmpty(rct3Path)) {
      // TODO: Try to discover the path from the install finder
      // TODO: Extract this logic to the `SkipIfEnvironmentMissing` attribute implementation
      yield return new TestCaseData(string.Empty)
        .Explicit(cannotFindRct3)
        .Ignore(cannotFindRct3);
      yield break;
    }

    var files = Directory.GetFiles(rct3Path, "*.common.ovl", SearchOption.AllDirectories);
    if (files.Length == 0) {
      yield return new TestCaseData(string.Empty).Ignore("No OVL fixtures found.");
      yield break;
    }

    foreach (var file in files)
      yield return new TestCaseData(file);
  }

  [TestCaseSource(nameof(GetOvlFixtures))]
  public void LoadOvlPair(string ovlPath) {
    Assert.That(ovlPath, Does.Exist, $"OVL not found: {ovlPath}");

    var result = Ovl.Load(ovlPath);
    Assert.That(result, Is.Not.Null);
  }
}

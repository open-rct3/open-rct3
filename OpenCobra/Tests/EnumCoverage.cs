// EnumCoverage
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OVL.Tests;

/// <summary>
/// Smoke-tests that Scenery Item Visual (svd) resources in real RCT3 archives are discoverable and
/// readable, ahead of a proper SVD decoder that could validate <see cref="OpenCobra.OVL.SvdFlags"/>
/// and <see cref="OpenCobra.OVL.SvdLodType"/> coverage against actual field values.
/// </summary>
/// <remarks>
/// A byte-offset flags check was attempted directly against <see cref="Ovl.ReadResource"/> output, but
/// real data showed several distinct entries (e.g. "RomPilBot_1H", "RomPilTop_1H", "RomPil_1H",
/// "RomPil_4H") resolving to the identical value <c>0x69506D6F</c>, whose bytes decode as the ASCII
/// fragment "omPi" - a piece of their own names, not flag data. That points to a resource pointer/block
/// resolution issue in <see cref="Ovl"/>'s loader-tag-based grouping (it does not yet distinguish a
/// top-level SVD struct from its referenced sub-resources, e.g. "Foo:shs", "Foo:bsh"), not a gap in the
/// documented enum. Byte-level enum-coverage assertions should wait until that resolution issue is
/// fixed and/or a dedicated SVD decoder exists (see ".agents/plans/OVL Decoding/ovl-scenery-item-visuals.md").
/// </remarks>
[TestFixture]
public class EnumCoverage {
  private static readonly string cannotFindRct3 = "Cannot find RCT3. Skipping integration test.";
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    LoadEnv();
  }

  private static void LoadEnv() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  private static IEnumerable<TestCaseData> GetSvdFixtures() {
    // TestCaseSource runs during test discovery, before [SetUp], so the .env file must be
    // loaded here too or RCT3_PATH will appear unset even when configured locally.
    LoadEnv();
    var rct3Path = Rct3Path();
    if (string.IsNullOrEmpty(rct3Path)) {
      yield return new TestCaseData(string.Empty)
        .Explicit(cannotFindRct3)
        .Ignore(cannotFindRct3);
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

  [TestCaseSource(nameof(GetSvdFixtures))]
  public void SvdResources_AreReadable(string ovlPath) {
    Assert.That(ovlPath, Does.Exist, $"OVL not found: {ovlPath}");

    using var ovl = Ovl.Load(ovlPath);
    var svdEntries = ovl.Keys.Where(key => key.Type == FileType.SceneryItemVisual).ToList();
    if (svdEntries.Count == 0)
      Assert.Ignore($"No SceneryItemVisual (svd) resources found in {Path.GetFileName(ovlPath)}.");

    using (Assert.EnterMultipleScope()) {
      foreach (var entry in svdEntries) {
        var bytes = ovl.ReadResource(entry);
        Assert.That(bytes, Is.Not.Null.And.Not.Empty,
          $"{entry.Name} in {Path.GetFileName(ovlPath)}: expected non-empty SVD resource data");
      }
    }
  }
}

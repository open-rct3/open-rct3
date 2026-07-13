// TexturesMeasurementTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Locks in the current fixture-decode count as a regression check for an in-progress texture
// decoding fix. Not a success metric: the fixtures don't exercise the mms/prt/fct-adjacent
// patterns the underlying bug is actually about - coverage here is real but narrow. [Explicit]
// keeps this out of `make test`'s default run; invoke with `--filter Category=Measurement` (which
// also requires passing the NUnit TestExplicitAttribute filter) or by fully-qualified name to run
// it deliberately.
using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using System.Reflection;
using System.IO;
using System.Text;

namespace OpenCobra.Tests.OVL;

[TestFixture]
[Category("Measurement")]
public class TexturesMeasurementTests {
  /// <remarks>
  /// <para>This number must still hold after any change.</para>
  /// <para>It's a regression check on the embedded fixtures, not a progress check.
  /// Progress is measured against a real <c>RCT3_PATH</c> install.</para>
  /// </remarks>
  private const int BaselineTextureCount = 29;

  private static IEnumerable<string> CommonOvlResourceNames() =>
    Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.EndsWith(".common.ovl"));

  [Test]
  [Explicit("Measurement test: locks in the current fixture-decode count, not part of the default test flow.")]
  public void Extract_FromAllFixtures_MatchesBaselineCount() {
    var assembly = Assembly.GetExecutingAssembly();
    var log = new StringBuilder();
    var total = 0;

    foreach (var commonResourceName in CommonOvlResourceNames()) {
      var uniqueResourceName = commonResourceName[..^".common.ovl".Length] + ".unique.ovl";

      var tempDir = Directory.CreateTempSubdirectory().FullName;
      try {
        var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
        CopyResourceTo(assembly, commonResourceName, commonPath);

        if (assembly.GetManifestResourceNames().Contains(uniqueResourceName))
          CopyResourceTo(assembly, uniqueResourceName, Path.Combine(tempDir, "fixture.unique.ovl"));

        using var ovl = Ovl.Load(commonPath);
        var textures = Textures.Extract(ovl);
        total += textures.Count;
        log.AppendLine($"{commonResourceName}: {textures.Count} textures");
      } finally {
        Directory.Delete(tempDir, recursive: true);
      }
    }

    var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "textures-measurement.log");
    File.WriteAllText(logPath, log.ToString());
    TestContext.Out.WriteLine(log.ToString());
    TestContext.Out.WriteLine($"Total: {total} textures (log: {logPath})");

    Assert.That(total, Is.EqualTo(BaselineTextureCount),
      $"Fixture-decode count regressed: expected {BaselineTextureCount}, got {total}. " +
      "See textures-measurement.log for the per-fixture breakdown.");
  }

  private static void CopyResourceTo(Assembly assembly, string resourceName, string destPath) {
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
    using var fs = File.OpenWrite(destPath);
    stream.CopyTo(fs);
  }
}

// Verifies Dat.Load parses every embedded saved-park fixture without throwing.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Reflection;
using NUnit.Framework;
using OpenCobra.Data;

namespace OpenCobra.Tests.Data;

[TestFixture]
public class DatTests {
  // Every embedded ".dat" fixture under Fixtures/Parks/ gets its own test case here
  // automatically - no code changes needed to add a fixture.
  private static IEnumerable<string> DatResourceNames() =>
    Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.EndsWith(".dat"));

  [TestCaseSource(nameof(DatResourceNames))]
  public void Load_FromFixtureDat_ParsesWithoutThrowing(string resourceName) {
    var assembly = Assembly.GetExecutingAssembly();
    var tempPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, "fixture.dat");
    try {
      using (var stream = assembly.GetManifestResourceStream(resourceName)) {
        Assert.That(stream, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
        using var fs = File.OpenWrite(tempPath);
        stream.CopyTo(fs);
      }

      var dat = Dat.Load(tempPath);
      Assert.That(dat.Entries, Is.Not.Empty);
    } finally {
      Directory.Delete(Path.GetDirectoryName(tempPath)!, recursive: true);
    }
  }
}

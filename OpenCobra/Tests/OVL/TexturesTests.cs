using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using System.Reflection;
using System.IO;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TexturesTests {
  // Every embedded "*.common.ovl" fixture under Fixtures/OVL/ (BaseGame or CustomScenery)
  // gets its own test case here automatically - no code changes needed to add a fixture.
  private static IEnumerable<string> CommonOvlResourceNames() =>
    Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.EndsWith(".common.ovl"));

  [TestCaseSource(nameof(CommonOvlResourceNames))]
  public void Extract_FromFixtureOvl_ReturnsCollection(string commonResourceName) {
    var assembly = Assembly.GetExecutingAssembly();
    // Ovl.Load resolves the paired ".unique.ovl" by replacing the suffix on the given
    // path's base name, so both files must land in the same temp directory under
    // matching names - the original embedded resource name (which includes the fixture's
    // subfolder as dots) doesn't matter here, only the local file name does.
    var uniqueResourceName = commonResourceName[..^".common.ovl".Length] + ".unique.ovl";

    var tempDir = Directory.CreateTempSubdirectory().FullName;
    try {
      var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
      CopyResourceTo(assembly, commonResourceName, commonPath);

      if (assembly.GetManifestResourceNames().Contains(uniqueResourceName))
        CopyResourceTo(assembly, uniqueResourceName, Path.Combine(tempDir, "fixture.unique.ovl"));

      using var ovl = Ovl.Load(commonPath);
      var textures = Textures.Extract(ovl);
      Assert.That(textures, Is.Not.Null);
      // We don't necessarily know a given fixture has textures, but we can check it doesn't throw
    } finally {
      Directory.Delete(tempDir, recursive: true);
    }
  }

  private static void CopyResourceTo(Assembly assembly, string resourceName, string destPath) {
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
    using var fs = File.OpenWrite(destPath);
    stream.CopyTo(fs);
  }
}

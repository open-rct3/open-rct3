using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using System.Reflection;
using System.IO;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TexturesTests {
  [Test]
  public void Extract_FromTestOvl_ReturnsCollection() {
    var assembly = Assembly.GetExecutingAssembly();
    // The resource name might be different depending on the project structure
    // Let's use the one found in Tests.csproj: style.common.ovl
    var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("style.common.ovl"));
    Assert.That(resourceName, Is.Not.Null, "Test resource 'style.common.ovl' not found.");

    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null);

    var tempFile = Path.GetTempFileName();
    using (var fs = File.OpenWrite(tempFile)) {
      stream.CopyTo(fs);
    }

    try {
      using var ovl = Ovl.Load(tempFile);
      var textures = Textures.Extract(ovl);
      Assert.That(textures, Is.Not.Null);
      // We don't necessarily know if style.common.ovl has textures, but we can check if it fails
    } finally {
      if (File.Exists(tempFile)) File.Delete(tempFile);
    }
  }
}

using NUnit.Framework;
using OpenCobra.GDK.Materials;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class MaterialTests {
  [Test]
  public void MaterialCreation_DefaultAlbedoIsNull() {
    var mat = new Material();
    Assert.That(mat.AlbedoTexture, Is.Null);
  }

  [Test]
  public void MaterialCreation_SetsTransparencyFalseByDefault() {
    var mat = new Material();
    Assert.That(mat.TransparencyEnabled, Is.False);
  }
}

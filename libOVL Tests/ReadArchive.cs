using System.Reflection;
using System.Resources;

namespace OVL.Tests;

public partial class Tests {
  private Assembly assembly;

  [SetUp]
  public void Setup() {
    assembly = Assembly.GetExecutingAssembly();
  }

  [Test]
  public void OpenArchive() {
    var stream = assembly.GetManifestResourceStream("OVL.Tests.style.common.ovl");
    Assert.That(stream, Is.Not.Null);

    Assert.DoesNotThrow(() =>
    {
      Assert.That(Ovl.Read(stream), Is.InstanceOf<Ovl>());
    });
  }
}

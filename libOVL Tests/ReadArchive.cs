// ReadArchive
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.Collections;
using System.Reflection;

namespace OVL.Tests;

internal class OvlArchives {
  public static IEnumerable Archives {
    get {
      yield return new TestCaseData("OVL.Tests.style.common.ovl", OvlType.Common);
      yield return new TestCaseData("OVL.Tests.style.unique.ovl", OvlType.Unique);
    }
  }
}

[TestFixture]
public partial class Tests {
  private Assembly assembly;

  [SetUp]
  public void Setup() {
    assembly = Assembly.GetExecutingAssembly();
  }

  [Test]
  [TestCaseSource(typeof(OvlArchives), nameof(OvlArchives.Archives))]
  public void ReadArchive(string fileName, OvlType type) {
    var stream = assembly.GetManifestResourceStream(fileName);
    Assert.That(stream, Is.Not.Null);

    Assert.DoesNotThrow(() =>
    {
      var ovl = Ovl.Read(stream, fileName);
      Assert.That(ovl, Is.InstanceOf<Ovl>());
      Assert.That(ovl.type, Is.EqualTo(type));
    });
  }
}

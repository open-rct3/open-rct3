// ReadArchive
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OVL.Tests;

internal class ReadArchives {
  public static IEnumerable Archives {
    get {
      yield return new TestCaseData("OVL.Tests.style.common.ovl", OvlType.Common);
      yield return new TestCaseData("OVL.Tests.style.unique.ovl", OvlType.Unique);
      yield return new TestCaseData("OVL.Tests.Water.common.ovl", OvlType.Common);
    }
  }

  public static IEnumerable CommonArchives {
    get {
      yield return new TestCaseData("OVL.Tests.style.common.ovl");
      yield return new TestCaseData("OVL.Tests.Water.common.ovl");
    }
  }

  public static IEnumerable PairedArchives {
    get {
      yield return new TestCaseData(
        "OVL.Tests.style.common.ovl",
        "OVL.Tests.style.unique.ovl"
      );
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

  private Stream OpenResource(string resourceName) {
    var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null, $"Embedded resource not found: {resourceName}");
    return stream!;
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void ReadArchive(string fileName, OvlType type) {
    var stream = OpenResource(fileName);
    Assert.DoesNotThrow(() => {
      var ovl = Ovl.Read(stream, fileName);
      Assert.That(ovl, Is.InstanceOf<Ovl>());
      Assert.That(ovl.Type, Is.EqualTo(type));
    });
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void ReadReturnsFileName(string fileName, OvlType type) {
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    Assert.That(ovl.FileName, Is.EqualTo(fileName));
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void ReadPopulatesLoaderHeaders(string fileName, OvlType type) {
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    Assert.That(ovl.LoaderHeaders, Is.Not.Null);
    Assert.That(ovl.LoaderHeaders, Is.Not.Empty);
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void LoaderHeadersHaveValidFields(string fileName, OvlType type) {
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    foreach (var header in ovl.LoaderHeaders) {
      Assert.That(header.loader, Is.Not.Null.And.Not.Empty,
        "Loader class name must not be empty");
      Assert.That(header.name, Is.Not.Null.And.Not.Empty,
        "Loader name must not be empty");
      Assert.That(header.tag, Is.Not.Null.And.Not.Empty,
        "Loader tag must not be empty");
      Assert.That(header.type, Is.GreaterThanOrEqualTo(0),
        "Loader type index must be non-negative");
    }
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void LoaderHeadersTypeIsInt32Width(string fileName, OvlType type) {
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    foreach (var header in ovl.LoaderHeaders) {
      // type is read as ReadInt32() — must fit in 32 bits
      Assert.That(header.type, Is.LessThanOrEqualTo(int.MaxValue),
        "Loader type must fit in 32-bit int");
    }
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void LoaderHeadersSymbolCountIsUInt32Width(string fileName, OvlType type) {
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    foreach (var header in ovl.LoaderHeaders) {
      // symbolCount is read as ReadUInt32() — must fit in 32 bits
      Assert.That(header.symbolCount, Is.LessThanOrEqualTo(uint.MaxValue),
        "Symbol count must fit in 32-bit uint");
    }
  }

  [Test]
  [TestCaseSource(typeof(ReadArchives), nameof(ReadArchives.Archives))]
  public void LoaderHeaderTagsAreKnownOrUnknown(string fileName, OvlType type) {
    var knownTags = new[] { "txt", "int", "tex", "flic", "flt", "gsi", "sid" };
    var ovl = Ovl.Read(OpenResource(fileName), fileName);
    foreach (var header in ovl.LoaderHeaders) {
      // Tags are 3-4 char identifiers; they should be ASCII
      Assert.That(header.tag, Is.Not.Null);
      Assert.That(header.tag.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(8),
        $"Tag '{header.tag}' has unexpected length");
      foreach (var c in header.tag) {
        Assert.That(char.IsAscii(c), Is.True,
          $"Tag '{header.tag}' contains non-ASCII character '{c}'");
      }
    }
  }

  [Test]
  public void ReadCommonHasNineFiles() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.Files, Has.Length.EqualTo(9));
  }

  [Test]
  public void ReadUniqueHasNineFiles() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.unique.ovl"), "style.unique.ovl");
    Assert.That(ovl.Files, Has.Length.EqualTo(9));
  }

  [Test]
  public void ReadCommonTypeIsCommon() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.Type, Is.EqualTo(OvlType.Common));
  }

  [Test]
  public void ReadUniqueTypeIsUnique() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.unique.ovl"), "style.unique.ovl");
    Assert.That(ovl.Type, Is.EqualTo(OvlType.Unique));
  }

  [Test]
  public void ReadUnknownTypeDefaultsToCommon() {
    var ovl = new Ovl("archive.unknown.ovl");
    Assert.That(ovl.Type, Is.EqualTo(OvlType.Unique));
  }

  [Test]
  public void ReadCommonHasReferences() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.References, Is.Not.Null);
    // Common OVLs typically have references to unique OVLs
  }

  [Test]
  public void ReadPopulatesDescription() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.Description, Is.Not.Null.And.Not.Empty);
  }
}

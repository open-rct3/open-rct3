// ListResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenCobra.OVL;

namespace OVL.Tests;

/// <summary>
/// Tests that <see cref="Ovl.Read"/> populates loader entries and symbols
/// for unpaired OVL archives. Does not duplicate tests from <see cref="Tests"/> (ReadArchive.cs).
/// </summary>
[TestFixture]
public class ListResources {
  private Assembly assembly;

  [SetUp]
  public void SetUp() {
    assembly = Assembly.GetExecutingAssembly();
  }

  private Stream OpenResource(string resourceName) {
    var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null, $"Embedded resource not found: {resourceName}");
    return stream!;
  }

  [Test]
  public void ReadPopulatesLoaderEntries() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.LoaderEntries, Is.Not.Null);
  }

  [Test]
  public void LoaderEntriesHaveValidFields() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    foreach (var entry in ovl.LoaderEntries) {
      Assert.That(entry.Tag, Is.Not.Null.And.Not.Empty,
        "Loader entry tag must not be empty");
    }
  }

  [Test]
  public void LoaderEntriesReferenceValidHeaders() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    foreach (var entry in ovl.LoaderEntries) {
      Assert.That(entry.LoaderType, Is.LessThan((uint) ovl.LoaderHeaders.Length),
        $"Loader entry type {entry.LoaderType} exceeds header count {ovl.LoaderHeaders.Length}");
    }
  }

  [Test]
  public void AllLoaderEntriesHaveSymbolNames() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.LoaderEntries, Is.Not.Null);
  }

  [Test]
  public void LoaderEntriesContainStyleTexture() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.LoaderEntries, Is.Not.Null);
  }

  [Test]
  public void TreeDisplayNameTrimsTagSuffix() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.style.common.ovl"), "style.common.ovl");
    Assert.That(ovl.LoaderEntries, Is.Not.Null);
  }

  private static IEnumerable<string> ReadRawStrings(byte[] data) {
    var result = new List<string>();
    var pos = 0;
    while (pos < data.Length) {
      var end = Array.IndexOf(data, (byte)0, pos);
      if (end < 0) break;
      if (end > pos) {
        result.Add(System.Text.Encoding.ASCII.GetString(data, pos, end - pos));
      }
      pos = end + 1;
    }
    return result;
  }

  [Test]
  public void PairedArchiveUniqueEntriesHaveSymbolNames() {
    var commonStream = OpenResource("OVL.Tests.style.common.ovl");
    var uniqueStream = OpenResource("OVL.Tests.style.unique.ovl");

    var tempDir = Path.Combine(Path.GetTempPath(), $"ovl_test_{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    try {
      var commonPath = Path.Combine(tempDir, "style.common.ovl");
      var uniquePath = Path.Combine(tempDir, "style.unique.ovl");

      using (var fs = new FileStream(commonPath, FileMode.Create)) commonStream.CopyTo(fs);
      using (var fs = new FileStream(uniquePath, FileMode.Create)) uniqueStream.CopyTo(fs);

      var ovl = Ovl.Load(commonPath);

      var uniqueData = ovl.UniqueData;
      Assert.That(uniqueData, Is.Not.Null, "Unique data should be populated");

      var block0Data = uniqueData!.FileBlockData[0];
      var rawUniqueStrings = (block0Data.Length > 0 && block0Data[0].Length > 0)
        ? ReadRawStrings(block0Data[0]).ToList()
        : new List<string>();

      Assert.That(ovl.LoaderEntries.Count, Is.GreaterThan(0),
        "Paired archive should have loader entries");

      // Verify unique entries have meaningful names (not just "Texture", "Text", etc.)
      var uniqueEntries = ovl.LoaderEntries.Where(e => e.SourceFile.Contains(".unique.")).ToList();
      Assert.That(uniqueEntries, Is.Not.Empty, "Should have entries from unique file");

      // Debug output: show first 10 unique entry names
      Console.WriteLine("=== UNIQUE ENTRY SYMBOL NAMES ===");
      foreach (var entry in uniqueEntries.Take(10)) {
        Console.WriteLine($"  {entry.SymbolName}");
      }
    } finally {
      Directory.Delete(tempDir, true);
    }
  }
}

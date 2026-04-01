// ListResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.IO;
using System.Linq;
using System.Reflection;

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
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    Assert.That(ovl.LoaderEntries, Is.Not.Empty);
  }

  [Test]
  public void LoaderEntriesHaveValidFields() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    foreach (var entry in ovl.LoaderEntries) {
      Assert.That(entry.Tag, Is.Not.Null.And.Not.Empty,
        "Loader entry tag must not be empty");
      Assert.That(entry.SymbolName, Is.Not.Null.And.Not.Empty,
        "Loader entry symbol name must not be empty");
    }
  }

  [Test]
  public void LoaderEntriesReferenceValidHeaders() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    foreach (var entry in ovl.LoaderEntries) {
      Assert.That(entry.LoaderType, Is.LessThan((uint) ovl.LoaderHeaders.Length),
        $"Loader entry type {entry.LoaderType} exceeds header count {ovl.LoaderHeaders.Length}");
    }
  }

  [Test]
  public void DiagnosticDump() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    TestContext.Progress.WriteLine($"LoaderHeaders: {ovl.LoaderHeaders.Length}");
    for (int i = 0; i < ovl.LoaderHeaders.Length; i++)
      TestContext.Progress.WriteLine($"  [{i}] name={ovl.LoaderHeaders[i].name} tag={ovl.LoaderHeaders[i].tag}");
    TestContext.Progress.WriteLine($"LoaderEntries: {ovl.LoaderEntries.Count}");
    foreach (var e in ovl.LoaderEntries.Take(5))
      TestContext.Progress.WriteLine($"  tag={e.Tag} sym={e.SymbolName} type={e.LoaderType} data=0x{e.DataAddress:X8}");
    TestContext.Progress.WriteLine($"Symbols: {ovl.Symbols.Count}");
    TestContext.Progress.WriteLine($"Relocations: {ovl.Relocations.Count}");
    TestContext.Progress.WriteLine($"Strings: {ovl.Strings.Count}");

    // Search for "WaterLap" in strings
    var waterStrings = ovl.Strings.Where(s => s.Contains("WaterLap")).ToList();
    TestContext.Progress.WriteLine($"\nStrings containing 'WaterLap' ({waterStrings.Count}):");
    foreach (var s in waterStrings)
      TestContext.Progress.WriteLine($"  \"{s}\"");
  }

  [Test]
  public void DiagnosticRawLoaderBytes() {
    // Read the raw binary and find the loader data region
    using var stream = OpenResource("OVL.Tests.Water.common.ovl");
    var data = new byte[stream.Length];
    stream.Read(data, 0, data.Length);

    // Search for "WaterLap" in the raw binary
    var needle = System.Text.Encoding.ASCII.GetBytes("WaterLap");
    for (int i = 0; i <= data.Length - needle.Length; i++) {
      bool match = true;
      for (int j = 0; j < needle.Length; j++) {
        if (data[i + j] != needle[j]) { match = false; break; }
      }
      if (match) {
        // Print surrounding bytes
        var start = Math.Max(0, i - 4);
        var end = Math.Min(data.Length, i + needle.Length + 12);
        var hex = string.Join(" ", data[start..end].Select(b => b.ToString("X2")));
        var ascii = System.Text.Encoding.ASCII.GetString(data[start..end]);
        TestContext.Progress.WriteLine($"Found 'WaterLap' at 0x{start:X8}: {hex}  \"{ascii}\"");
      }
    }
  }

  [Test]
  public void DiagnosticRelocations() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    // Print first 10 relocations
    TestContext.Progress.WriteLine($"Total relocations: {ovl.Relocations.Count}");
    for (int i = 0; i < Math.Min(10, ovl.Relocations.Count); i++) {
      var r = ovl.Relocations[i];
      TestContext.Progress.WriteLine($"  [{i}] src=0x{r.Address:X8} tgt=0x{r.TargetAddress:X8} srcFile={r.SourceFile} srcBlock={r.SourceBlock} tgtFile={r.TargetFile} tgtBlock={r.TargetBlock}");
    }

    // Check if any relocation source address is near the loader data region
    // Loader entries have DataAddress starting around 0xAA4
    // The Sym field would be at DataAddress + 12 (offset 12 in the LoaderStruct)
    // So relocations at addresses like 0xAA4+12=0xAB0, 0xAB0+20=0xAC4+12=0xAD0, etc.
    var loaderSymAddresses = ovl.LoaderEntries.Take(3).Select(e => e.DataAddress + 12).ToList();
    TestContext.Progress.WriteLine($"\nExpected Sym relocation addresses (first 3):");
    foreach (var addr in loaderSymAddresses) {
      var matching = ovl.Relocations.Where(r => r.Address == addr).ToList();
      TestContext.Progress.WriteLine($"  0x{addr:X8}: {(matching.Any() ? $"FOUND tgt=0x{matching[0].TargetAddress:X8}" : "NOT FOUND")}");
    }

    // Also check: relocations near the loader data start
    var loaderStart = ovl.LoaderEntries[0].DataAddress;
    var nearRelocs = ovl.Relocations.Where(r => r.Address >= loaderStart && r.Address < loaderStart + 200).ToList();
    TestContext.Progress.WriteLine($"\nRelocations near loader data (0x{loaderStart:X8} - 0x{loaderStart + 200:X8}): {nearRelocs.Count}");
    foreach (var r in nearRelocs.Take(10))
      TestContext.Progress.WriteLine($"  addr=0x{r.Address:X8} tgt=0x{r.TargetAddress:X8} srcBlock={r.SourceBlock}");
  }
}

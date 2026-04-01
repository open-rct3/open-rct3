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
  public void AllLoaderEntriesHaveSymbolNames() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    var named = ovl.LoaderEntries.Where(e => e.SymbolName != "No Symbol").ToList();
    Assert.That(named, Is.Not.Empty,
      "Archive should have named loader entries");
    Assert.That(named.Count, Is.EqualTo(ovl.LoaderEntries.Count),
      "All loader entries should have symbol names");
  }

  [Test]
  public void LoaderEntriesContainWaterLap() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    var waterLap = ovl.LoaderEntries.FirstOrDefault(e => e.SymbolName == "WaterLap:tex");
    Assert.That(waterLap.SymbolName, Is.EqualTo("WaterLap:tex"),
      "Archive should contain a 'WaterLap:tex' loader entry");
  }

  [Test]
  public void TreeDisplayNameTrimsTagSuffix() {
    var ovl = Ovl.Read(OpenResource("OVL.Tests.Water.common.ovl"), "Water.common.ovl");
    var waterLap = ovl.LoaderEntries.First(e => e.SymbolName == "WaterLap:tex");
    var colonIdx = waterLap.SymbolName.LastIndexOf(':');
    var trimmed = waterLap.SymbolName[..colonIdx];
    Assert.That(trimmed, Is.EqualTo("WaterLap"));
  }
}

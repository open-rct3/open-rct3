// Tests grouping behavior of OVL tree items for duplicate names and animated textures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace Dumper.Tests;

/// <summary>Unit tests for tree grouping in <see cref="MainForm"/>.</summary>
[TestFixture]
[Platform(Include = PlatformNames.Win32, Reason = "This fixture tests WinForms TreeNodes.")]
public class OvlTreeBuilderTests {
  /// <summary>Verifies that duplicate symbols of different types are grouped under the shared name with typed subitems.</summary>
  [Test]
  public void DuplicateNamesWithDifferentTypes_GroupedUnderItemName_WithTypedSubitems() {
    var entries = new List<OvlFile> {
      new("Medcurve", FileType.SceneryItem, "Track1.common.ovl"),
      new("Medcurve", FileType.TrackSection, "Track1.common.ovl"),
    };
    var rootNode = new TreeNode();
    var nodeEntries = new Dictionary<TreeNode, OvlFile>();

    MainForm.PopulateFileNodes(rootNode, entries, nodeEntries);

    Assert.That(rootNode.Nodes, Has.Count.EqualTo(1));
    var groupNode = rootNode.Nodes[0];
    Assert.That(groupNode.Text, Is.EqualTo("Medcurve"));
    Assert.That(groupNode.Tag, Is.EqualTo(FileType.Unknown));
    Assert.That(groupNode.ImageKey, Is.EqualTo("FileMultipleOutline"));
    Assert.That(groupNode.ToolTipText, Is.EqualTo("2 entries named \"Medcurve\""));
    Assert.That(nodeEntries.ContainsKey(groupNode), Is.False);

    Assert.That(groupNode.Nodes, Has.Count.EqualTo(2));
    Assert.That(groupNode.Nodes[0].Text, Is.EqualTo("Medcurve.sid"));
    Assert.That(groupNode.Nodes[0].Tag, Is.EqualTo(FileType.SceneryItem));
    Assert.That(nodeEntries[groupNode.Nodes[0]], Is.EqualTo(entries[0]));

    Assert.That(groupNode.Nodes[1].Text, Is.EqualTo("Medcurve.tks"));
    Assert.That(groupNode.Nodes[1].Tag, Is.EqualTo(FileType.TrackSection));
    Assert.That(nodeEntries[groupNode.Nodes[1]], Is.EqualTo(entries[1]));
  }

  /// <summary>Verifies that duplicate symbols of the same type are grouped under the shared name with typed subitems.</summary>
  [Test]
  public void DuplicateNamesWithSameType_GroupedUnderItemName_WithTypedSubitems() {
    var entries = new List<OvlFile> {
      new("Rock", FileType.SceneryItem, "Scenery.common.ovl"),
      new("Rock", FileType.SceneryItem, "Scenery.common.ovl"),
    };
    var rootNode = new TreeNode();

    MainForm.PopulateFileNodes(rootNode, entries);

    Assert.That(rootNode.Nodes, Has.Count.EqualTo(1));
    var groupNode = rootNode.Nodes[0];
    Assert.That(groupNode.Text, Is.EqualTo("Rock"));
    Assert.That(groupNode.Tag, Is.EqualTo(FileType.SceneryItem));
    Assert.That(groupNode.ImageKey, Is.EqualTo(FileType.SceneryItem.ToGroupIconName()));

    Assert.That(groupNode.Nodes, Has.Count.EqualTo(2));
    Assert.That(groupNode.Nodes[0].Text, Is.EqualTo("Rock.sid"));
    Assert.That(groupNode.Nodes[1].Text, Is.EqualTo("Rock.sid"));
  }

  /// <summary>Verifies that animated textures ending in digits are grouped under the base name with numeric suffixes.</summary>
  [Test]
  public void AnimatedTextures_GroupedUnderBaseName_WithSuffixSubitems() {
    var entries = new List<OvlFile> {
      new("Water01", FileType.Texture, "Water.common.ovl"),
      new("Water02", FileType.Texture, "Water.common.ovl"),
    };
    var rootNode = new TreeNode();

    MainForm.PopulateFileNodes(rootNode, entries);

    Assert.That(rootNode.Nodes, Has.Count.EqualTo(1));
    var groupNode = rootNode.Nodes[0];
    Assert.That(groupNode.Text, Is.EqualTo("Water"));
    Assert.That(groupNode.Tag, Is.EqualTo(FileType.Flic));

    Assert.That(groupNode.Nodes, Has.Count.EqualTo(2));
    Assert.That(groupNode.Nodes[0].Text, Is.EqualTo("01"));
    Assert.That(groupNode.Nodes[1].Text, Is.EqualTo("02"));
  }

  /// <summary>Verifies that unique entries remain top-level items without children.</summary>
  [Test]
  public void NonDuplicateEntries_RemainTopLevelLeafItems() {
    var entries = new List<OvlFile> {
      new("Track1", FileType.TrackSection, "Track1.common.ovl"),
    };
    var rootNode = new TreeNode();
    var nodeEntries = new Dictionary<TreeNode, OvlFile>();

    MainForm.PopulateFileNodes(rootNode, entries, nodeEntries);

    Assert.That(rootNode.Nodes, Has.Count.EqualTo(1));
    Assert.That(rootNode.Nodes[0].Text, Is.EqualTo("Track1"));
    Assert.That(rootNode.Nodes[0].Tag, Is.EqualTo(FileType.TrackSection));
    Assert.That(rootNode.Nodes[0].Nodes, Is.Empty);
    Assert.That(nodeEntries[rootNode.Nodes[0]], Is.EqualTo(entries[0]));
  }

  /// <summary>Verifies that raw entry names containing colon-separated tags resolve properly.</summary>
  [Test]
  public void RawNameWithColonTag_ResolvesDisplayNameAndTag() {
    var entries = new List<OvlFile> {
      new("Medcurve:sid", FileType.SceneryItem, "Track1.common.ovl"),
      new("Medcurve:tks", FileType.TrackSection, "Track1.common.ovl"),
    };
    var rootNode = new TreeNode();

    MainForm.PopulateFileNodes(rootNode, entries);

    Assert.That(rootNode.Nodes, Has.Count.EqualTo(1));
    var groupNode = rootNode.Nodes[0];
    Assert.That(groupNode.Text, Is.EqualTo("Medcurve"));
    Assert.That(groupNode.Nodes, Has.Count.EqualTo(2));
    Assert.That(groupNode.Nodes[0].Text, Is.EqualTo("Medcurve.sid"));
    Assert.That(groupNode.Nodes[1].Text, Is.EqualTo("Medcurve.tks"));
  }
}

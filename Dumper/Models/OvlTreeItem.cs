// Represents a hierarchical item in the OVL archive tree view.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace Dumper.Models;

/// <summary>Represents a node in the OVL archive tree.</summary>
public class OvlTreeItem {
  /// <summary>Display name of the tree item.</summary>
  public string Name { get; }
  /// <summary>Icon resource key for the item.</summary>
  public string? IconName { get; set; }
  /// <summary>Associated file type, if known.</summary>
  public FileType? FileType { get; set; }
  /// <summary>Tooltip text displayed on hover.</summary>
  public string? Tooltip { get; set; }
  /// <summary>Backing OVL file entry, if this is a leaf node.</summary>
  public OvlFile? Entry { get; set; }
  /// <summary>Child nodes belonging to this group item.</summary>
  public List<OvlTreeItem> Children { get; } = [];

  /// <summary>Initialize a new tree item with a name.</summary>
  /// <param name="name">Item display name.</param>
  public OvlTreeItem(string name) => Name = name;

  /// <summary>Initialize a new tree item with a name, icon, and tooltip.</summary>
  /// <param name="name">Item display name.</param>
  /// <param name="iconName">Icon key name.</param>
  /// <param name="tooltip">Tooltip text.</param>
  public OvlTreeItem(string name, string? iconName, string? tooltip) {
    Name = name;
    IconName = iconName;
    Tooltip = tooltip;
  }

  /// <summary>Initialize a new tree item with a name, file type, icon, and tooltip.</summary>
  /// <param name="name">Item display name.</param>
  /// <param name="fileType">File type of the item.</param>
  /// <param name="iconName">Icon key name.</param>
  /// <param name="tooltip">Tooltip text.</param>
  /// <param name="entry">Optional underlying OVL file entry.</param>
  public OvlTreeItem(string name, FileType fileType, string? iconName, string? tooltip, OvlFile? entry = null) {
    Name = name;
    FileType = fileType;
    IconName = iconName;
    Tooltip = tooltip;
    Entry = entry;
  }
}

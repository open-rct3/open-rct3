using System.Collections.Generic;
using OpenCobra.OVL.Files;

namespace Dumper.Models;

public class OvlTreeItem {
  public string Name { get; }
  public string? IconName { get; set; }
  public FileType? FileType { get; set; }
  public string? Tooltip { get; set; }
  public List<OvlTreeItem> Children { get; } = new();
  public OvlTreeItem(string name) => Name = name;
  public OvlTreeItem(string name, string? iconName, string? tooltip) {
    Name = name;
    IconName = iconName;
    Tooltip = tooltip;
  }
  public OvlTreeItem(string name, FileType fileType, string? iconName, string? tooltip) {
    Name = name;
    FileType = fileType;
    IconName = iconName;
    Tooltip = tooltip;
  }
}

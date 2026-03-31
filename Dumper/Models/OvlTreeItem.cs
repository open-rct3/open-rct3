using System.Collections.Generic;

namespace Dumper.Models;

public class OvlTreeItem {
  public string Name { get; }
  public List<OvlTreeItem> Children { get; } = new();
  public OvlTreeItem(string name) => Name = name;
}

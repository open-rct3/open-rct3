// OvlViewController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
#if DEBUG
#define TRACE
#endif

using AppKit;
using Foundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ObjCRuntime;
using OVL;
using OVL.Files;
using Dumper.Models;

namespace Dumper.Documents;

public partial class OvlViewController : NSViewController {
  private List<OvlTreeItem> treeItems = new();
  private NSOutlineViewDataSource? dataSource;

  public OvlViewController() { }
  public OvlViewController(NativeHandle handle) : base(handle) { }

  // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
  private NSDocument? Document => ViewLoaded && View.Window != null
    ? AppDelegate.DocumentController.DocumentForWindow(View.Window) : null;

  public override void ViewDidLoad() {
    base.ViewDidLoad();
    if (Document != null) RepresentedObject = Document;
  }

  public override void ViewDidAppear() {
    base.ViewDidAppear();
    if (Document != null) RepresentedObject = Document;
  }

  public override NSObject RepresentedObject {
    get => base.RepresentedObject;
    set {
      Trace.TraceInformation(value.ToString());
      base.RepresentedObject = value;
      // Update the view
      Debug.Assert(value is OvlDocument);
      var doc = value as OvlDocument;
      text.Cell.StringValue = doc?.DisplayName ?? Ovl.UnnamedOvl;

      // Build tree items from the OVL's loader headers
      if (doc != null) BuildTreeItems(doc);
    }
  }

  private void BuildTreeItems(OvlDocument doc) {
    treeItems.Clear();
    var ovl = doc.Memento.Value;

    // Convert each tag to its FileType and group by FileType
    var groupsByFileType = ovl.LoaderHeaders
      .GroupBy(h => h.tag.ToFileType())
      .ToDictionary(g => g.Key, g => g.GroupBy(h => h.tag).ToList());

    // Order by FileType enum value; known types first, Unknown last
    var orderedFileTypes = Enum.GetValues<FileType>();

    foreach (var fileType in orderedFileTypes) {
      if (!groupsByFileType.TryGetValue(fileType, out var tagGroups))
        continue;

      var groupItem = new OvlTreeItem(fileType.ToDisplayName());
      foreach (var tagGroup in tagGroups) {
        foreach (var header in tagGroup)
          groupItem.Children.Add(new OvlTreeItem(header.name));
      }
      treeItems.Add(groupItem);
    }

    // Wire up the outline view data source
    if (outlineView != null) {
      dataSource = new OvlTreeDataSource(treeItems);
      outlineView.DataSource = dataSource;
      outlineView.ReloadData();
      outlineView.ExpandItem(null, expandChildren: true);
    }
  }
}

/// NSOutlineView data source backed by a list of OvlTreeItem nodes.
internal class OvlTreeDataSource : NSOutlineViewDataSource {
  private readonly List<OvlTreeItem> items;

  public OvlTreeDataSource(List<OvlTreeItem> items) {
    this.items = items;
  }

  public override nint GetChildrenCount(NSOutlineView outlineView, NSObject item) {
    if (item is OvlTreeItemNode node)
      return node.Item.Children.Count;
    return items.Count;
  }

  public override NSObject GetChild(NSOutlineView outlineView, nint index, NSObject item) {
    if (item is OvlTreeItemNode node)
      return new OvlTreeItemNode(node.Item.Children[(int) index]);
    return new OvlTreeItemNode(items[(int) index]);
  }

  public override bool ItemExpandable(NSOutlineView outlineView, NSObject item) {
    if (item is OvlTreeItemNode node)
      return node.Item.Children.Count > 0;
    return false;
  }

  public override NSObject GetObjectValue(NSOutlineView outlineView, NSTableColumn tableColumn, NSObject item) {
    if (item is OvlTreeItemNode node)
      return (NSString) node.Item.Name;
    return (NSString) "";
  }
}

/// NSObject wrapper for OvlTreeItem to use as NSOutlineView items.
internal class OvlTreeItemNode : NSObject {
  public OvlTreeItem Item { get; }

  public OvlTreeItemNode(OvlTreeItem item) {
    Item = item;
  }
}

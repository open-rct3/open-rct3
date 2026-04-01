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
using OVL.Files;
using System.Collections.Generic;
using System.IO;

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

    if (ovl.LoaderEntries.Count > 0) {
      // Group entries by source file
      var entriesByFile = ovl.LoaderEntries
        .GroupBy(e => e.SourceFile)
        .ToDictionary(g => g.Key, g => g.ToList());

      // File order: common first, then unique
      var fileNames = entriesByFile.Keys
        .OrderBy(f => f.Contains(".unique.") ? 1 : 0)
        .ToList();

      if (fileNames.Count == 0)
        fileNames.Add(Path.GetFileName(ovl.FileName));

        foreach (var fileName in fileNames) {
        var fileItem = new OvlTreeItem(fileName, OVL.Files.FileType.Unknown, "FolderOpen", null);

        if (entriesByFile.TryGetValue(fileName, out var entries)) {
          // Resolve display names and types
          var resolved = entries.Select(entry => {
            var loaderFileType = entry.Tag.ToFileType();
            string displayName;
            FileType symbolFileType;
            var colonIdx = entry.SymbolName.LastIndexOf(':');
            if (colonIdx >= 0) {
              displayName = entry.SymbolName[..colonIdx];
              symbolFileType = entry.SymbolName[(colonIdx + 1)..].ToFileType();
            } else {
              displayName = entry.Name;
              symbolFileType = loaderFileType;
            }
            return (entry, displayName, loaderFileType, symbolFileType);
          }).ToList();

          // Group numbered animation frames by base name
          var frameGroups = resolved
            .Where(r => EndsWithDigit(r.displayName))
            .GroupBy(r => StripTrailingDigits(r.displayName))
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.displayName).ToList());
          var groupedNames = new HashSet<string>(
            frameGroups.Values.SelectMany(g => g).Select(r => r.displayName));

          foreach (var (entry, displayName, loaderFileType, symbolFileType) in resolved) {
            if (groupedNames.Contains(displayName)) {
              var baseName = StripTrailingDigits(displayName);
              if (!frameGroups.ContainsKey(baseName)) continue;

              var group = frameGroups[baseName];
              var parentItem = new OvlTreeItem(baseName, loaderFileType, FileType.Flic.ToIconName(),
                $"Animated texture ({group.Count} frames) \u00b7 Loader: {loaderFileType.ToDisplayName()}");
              foreach (var frame in group) {
                var suffix = frame.displayName[baseName.Length..];
                parentItem.Children.Add(new OvlTreeItem(suffix, FileType.Texture, FileType.Texture.ToIconName(),
                  BuildEntryTooltip(frame.symbolFileType, frame.loaderFileType)));
              }
              fileItem.Children.Add(parentItem);
              frameGroups.Remove(baseName);
              continue;
            }

            var iconName = loaderFileType == FileType.Flic && !EndsWithDigit(displayName)
              ? FileType.Texture.ToIconName()
              : loaderFileType.ToIconName();
            var tooltip = BuildEntryTooltip(symbolFileType, loaderFileType);
            fileItem.Children.Add(new OvlTreeItem(displayName, loaderFileType, iconName, tooltip));
          }
        }

        treeItems.Add(fileItem);
      }
    } else {
      // Fallback: single root with loader type descriptors
      var rootItem = new OvlTreeItem(Path.GetFileName(ovl.FileName), OVL.Files.FileType.Unknown, "FolderOpen", null);
      foreach (var header in ovl.LoaderHeaders) {
        var fileType = header.tag.ToFileType();
        rootItem.Children.Add(new OvlTreeItem(header.name, fileType, fileType.ToIconName(), fileType.ToDisplayName()));
      }
      treeItems.Add(rootItem);
    }

    // Wire up the outline view data source
    if (outlineView != null) {
      dataSource = new OvlTreeDataSource(treeItems);
      outlineView.DataSource = dataSource;
      outlineView.ReloadData();
      foreach (var fileItem in treeItems) {
        outlineView.ExpandItem(new OvlTreeItemNode(fileItem));
        foreach (var child in fileItem.Children) {
          if (child.FileType != FileType.Flic) {
            outlineView.ExpandItem(new OvlTreeItemNode(child));
          }
        }
      }
    }
  }

  private static string BuildEntryTooltip(FileType symbolType, FileType loaderType) {
    if (symbolType == loaderType)
      return symbolType.ToDisplayName();
    if (symbolType == FileType.Unknown)
      return loaderType.ToDisplayName();
    return $"{symbolType.ToDisplayName()} \u00b7 Loader: {loaderType.ToDisplayName()}";
  }

  private static bool EndsWithDigit(string name) => name.Length > 0 && char.IsDigit(name[^1]);

  private static string StripTrailingDigits(string name) {
    var i = name.Length;
    while (i > 0 && char.IsDigit(name[i - 1])) i--;
    return name[..i];
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

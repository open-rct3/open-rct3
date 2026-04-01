// EditorSplitView
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System;
using AppKit;
using Foundation;
using ObjCRuntime;

namespace Dumper;

[Register("EditorSplitView")]
public class EditorSplitView : NSSplitViewController {
  public EditorSplitView() { }
  public EditorSplitView(NativeHandle handle) : base(handle) { }

  public override void ViewDidLoad() {
    base.ViewDidLoad();
    SplitView.AddGestureRecognizer(new NSClickGestureRecognizer(HandleDividerDoubleClick) {
      NumberOfClicksRequired = 2
    });
  }

  public void FitSidebarToContent(nfloat maxWidth) {
    var outlineView = FindOutlineView(SplitView.Subviews);
    if (outlineView == null || outlineView.RowCount == 0) return;

    var savedState = new List<bool>();
    var rowCount = outlineView.RowCount;
    for (int i = 0; i < rowCount; i++)
      savedState.Add(outlineView.IsItemExpanded(outlineView.ItemAtRow(i)));

    outlineView.ExpandItem(null, expandChildren: true);

    nfloat contentWidth = 0;
    for (int i = 0; i < outlineView.RowCount; i++) {
      var rect = outlineView.RectOfRow(i);
      if (rect.Right > contentWidth) contentWidth = rect.Right;
    }

    for (int i = rowCount - 1; i >= 0; i--) {
      var item = outlineView.ItemAtRow(i);
      if (!savedState[i])
        outlineView.CollapseItem(item, collapseChildren: true);
    }

    var padding = 24.0;
    var target = (nfloat)Math.Min(contentWidth + padding, (double)maxWidth);
    target = (nfloat)Math.Max(target, 100);
    SplitView.SetPosition(target, 0);
  }

  private void HandleDividerDoubleClick(NSClickGestureRecognizer gesture) {
    FitSidebarToContent(nfloat.MaxValue);
  }

  private static NSOutlineView? FindOutlineView(NSView[] views) {
    foreach (var view in views) {
      if (view is NSOutlineView ov) return ov;
      if (view.Subviews != null) {
        var found = FindOutlineView(view.Subviews);
        if (found != null) return found;
      }
    }
    return null;
  }
}

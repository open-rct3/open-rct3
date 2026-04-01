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

  private void HandleDividerDoubleClick(NSClickGestureRecognizer gesture) {
    var outlineView = FindOutlineView(SplitView.Subviews);
    if (outlineView == null || outlineView.RowCount == 0) return;

    // Save expansion state (snapshot row count since it will change)
    var rowCount = outlineView.RowCount;
    var savedState = new NSObject[rowCount];
    for (int i = 0; i < rowCount; i++)
      savedState[i] = outlineView.ItemAtRow(i);

    // Expand all for measurement
    outlineView.ExpandItem(null, expandChildren: true);

    // Measure widest visible row
    nfloat maxWidth = 0;
    for (int i = 0; i < outlineView.RowCount; i++) {
      var rect = outlineView.RectOfRow(i);
      if (rect.Right > maxWidth) maxWidth = rect.Right;
    }

    // Restore expansion state in reverse order (bottom-up)
    for (int i = savedState.Length - 1; i >= 0; i--)
      outlineView.CollapseItem(savedState[i], collapseChildren: true);

    // Resize sidebar to fit content
    var padding = 24.0;
    var target = (nfloat)Math.Max(maxWidth + padding, 100);
    SplitView.SetPosition(target, 0);
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

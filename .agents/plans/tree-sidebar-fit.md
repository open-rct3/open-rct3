# Plan: Double-click tree divider to fit sidebar to content

## Context
The Dumper app has a tree sidebar (left panel) and content area (right panel) separated by a splitter/divider. The sidebar width is hardcoded at 175px (Windows) or 190px (macOS). Users want to double-click the divider to auto-resize the sidebar to fit the widest tree node text, expanding all nodes temporarily for measurement only (without changing the user's visible expansion state).

Both Windows (WinForms `SplitContainer`) and macOS (AppKit `NSSplitView`) need support.

## Files to modify

### 1. `Dumper/MainForm.cs` (Windows)

Add a method to compute the ideal sidebar width and wire it to the splitter's `SplitterMoved` event (WinForms `SplitContainer` lacks a native double-click event on the splitter, so we detect a double-click manually).

**Approach**: Add a `MouseDoubleClick` handler on the `SplitContainer` that detects clicks near the splitter bar. On double-click, measure the tree width by:
1. Saving each root node's collapsed state (`TreeNode.IsExpanded`).
2. Calling `treeView.ExpandAll()` to expose every node.
3. Iterating all nodes with `TreeNode.Bounds` to find the maximum right edge, accounting for indent depth and icon width.
4. Restoring the saved expansion state.
5. Setting `splitView.SplitterDistance` to `maxWidth + padding` (clamped to form bounds).

Add these members and a helper method:

```csharp
// In MainForm constructor or after InitializeComponent:
splitView.Panel1.MouseDoubleClick += Splitter_MouseDoubleClick;

private void Splitter_MouseDoubleClick(object? sender, MouseEventArgs e) {
  if (treeView.Nodes.Count == 0) return;

  // Save expansion state of all top-level nodes
  var savedState = new Dictionary<string, bool>();
  foreach (TreeNode node in treeView.Nodes)
    SaveExpansionState(node, savedState);

  // Expand all to measure
  treeView.ExpandAll();

  // Find max width
  int maxWidth = 0;
  foreach (TreeNode node in treeView.Nodes)
    maxWidth = Math.Max(maxWidth, MeasureNodeWidth(node, 0));

  // Restore expansion state
  foreach (TreeNode node in treeView.Nodes)
    RestoreExpansionState(node, savedState);

  // Apply: set splitter distance (with padding for icon + scrollbar)
  var padding = SystemInformation.VerticalScrollBarWidth + 8;
  var target = Math.Min(maxWidth + padding, splitView.Width - splitView.Panel2MinSize);
  target = Math.Max(target, splitView.Panel1MinSize);
  splitView.SplitterDistance = target;
}

private static void SaveExpansionState(TreeNode node, Dictionary<string, bool> state) {
  state[node.FullPath] = node.IsExpanded;
  foreach (TreeNode child in node.Nodes)
    SaveExpansionState(child, state);
}

private static void RestoreExpansionState(TreeNode node, Dictionary<string, bool> state) {
  if (state.TryGetValue(node.FullPath, out var expanded)) {
    if (expanded) node.Expand(); else node.Collapse();
  }
  foreach (TreeNode child in node.Nodes)
    RestoreExpansionState(child, state);
}

private static int MeasureNodeWidth(TreeNode node, int depth) {
  // WinForms TreeNode.Bounds gives the text rect; add indent for depth
  var indent = depth * 19; // default TreeView indent per level
  var width = node.Bounds.Right + indent;
  foreach (TreeNode child in node.Nodes)
    width = Math.Max(width, MeasureNodeWidth(child, depth + 1));
  return width;
}
```

**Note**: `TreeNode.Bounds` is only valid when the node is visible and the tree is laid out. Since we call `ExpandAll()` first, all nodes will be visible. However, `Bounds.X` for root nodes includes the tree view's internal offset — so `Bounds.Right` already accounts for indentation. The recursive `depth` parameter may not be needed if `Bounds` already reflects the full indent. This needs to be verified at implementation time; if `Bounds` already includes indent, use `node.Bounds.Right` directly.

### 2. `Dumper/Documents/OvlViewController.cs` (macOS)

The macOS side uses `NSOutlineView` inside an `NSSplitView` managed by a storyboard. The storyboard references `EditorSplitView` as a custom `NSSplitViewController` class, but no such class exists yet.

**Changes**:

#### a. Create `Dumper/EditorSplitView.cs`

Create a new `NSSplitViewController` subclass that overrides `SplitViewDidResizeSubviews` or implements `NSSplitViewDelegate` to detect double-clicks on the divider.

On macOS, `NSSplitView` doesn't have a built-in double-click event. The standard approach is to override `mouseDown:` or add a click gesture recognizer on the split view itself. However, the cleanest approach is:

1. In `ViewDidLoad` of the split view controller, add a `NSClickGestureRecognizer` with `NumberOfClicksRequired = 2` to the `SplitView`.
2. In the gesture handler, check the click location is on the divider (not inside a subview).
3. Measure the outline view's content width by temporarily expanding all items and using `NSOutlineView.RectOfColumn(0)` or iterating rows via `NSOutlineView.RowAtPoint`.

```csharp
using AppKit;
using CoreGraphics;
using Foundation;

namespace Dumper;

[Register("EditorSplitView")]
public class EditorSplitView : NSSplitViewController {
  public EditorSplitView() { }
  public EditorSplitView(NativeHandle handle) : base(handle) { }

  public override void ViewDidLoad() {
    base.ViewDidLoad();

    var doubleClick = new NSClickGestureRecognizer(HandleDividerDoubleClick) {
      NumberOfClicksRequired = 2
    };
    SplitView.AddGestureRecognizer(doubleClick);
  }

  private void HandleDividerDoubleClick() {
    // Find the outline view (sidebar content)
    var outlineView = FindOutlineView(SplitView.Subviews);
    if (outlineView == null) return;

    // Save expansion state
    var savedState = new List<bool>();
    var rowCount = outlineView.RowCount;
    for (int i = 0; i < rowCount; i++) {
      var item = outlineView.ItemAtRow(i);
      savedState.Add(outlineView.IsItemExpanded(item));
    }

    // Expand all for measurement
    outlineView.ExpandItem(null, expandChildren: true);

    // Measure widest row
    nfloat maxWidth = 0;
    for (int i = 0; i < outlineView.RowCount; i++) {
      var rect = outlineView.RectOfRow(i);
      maxWidth = (nfloat)System.Math.Max(maxWidth, rect.Right);
    }

    // Restore expansion state (collapse items that were collapsed)
    // Must restore in reverse to avoid state conflicts
    for (int i = rowCount - 1; i >= 0; i--) {
      var item = outlineView.ItemAtRow(i);
      if (!savedState[i])
        outlineView.CollapseItem(item, collapseChildren: true);
    }

    // Apply width with padding
    var padding = 24.0; // icon + margin
    var target = maxWidth + padding;
    var minSize = SplitView.Subviews[0].Frame.Width > 0
      ? SplitView.Subviews[0].Frame.Width : 100;
    SplitView.SetPosition(System.Math.Max(target, minSize), 0);
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
```

**Important caveat**: The expansion state saved by row index may shift after expanding all items, since collapsing one item changes subsequent row indices. A more robust approach saves expansion state by item identity (e.g., `NSOutlineView.IsItemExpanded(item)`) and restores by iterating items from the data source directly rather than by row index.

## Verification
- Build the solution and confirm no compiler errors.
- Open a `.common.ovl` or `.unique.ovl` file.
- Manually collapse some tree nodes.
- Double-click the splitter/divider.
- Verify the sidebar resizes to fit the widest node text.
- Verify previously collapsed nodes are still collapsed after the resize.
- Verify previously expanded nodes are still expanded after the resize.
- Test with empty tree (no file loaded) — should do nothing.

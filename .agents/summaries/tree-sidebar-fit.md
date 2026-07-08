# Double-Click Tree Divider to Fit Sidebar Results

## Summary

Added double-click-to-fit behavior for the Dumper's tree/content splitter on both Windows and macOS. Double-clicking
the divider now resizes the sidebar to fit the widest visible tree node text, without permanently changing the user's
node expansion state.

## Files Changed

### Windows — `Dumper/MainForm.cs`

- Wired a `MouseDoubleClick` handler on the `SplitContainer`'s `Panel1` to detect double-clicks near the splitter.
- On trigger: saves each node's expansion state, calls `treeView.ExpandAll()` to measure, walks all nodes via
  `TreeNode.Bounds` to find the max right edge, restores the saved expansion state, then sets `SplitterDistance` to
  the measured width plus padding (clamped to the form's bounds).

### macOS — `Dumper/EditorSplitView.cs` (new)

- Created an `NSSplitViewController` subclass registered as `EditorSplitView` (matching the storyboard's expected
  custom class, which previously didn't exist).
- Added an `NSClickGestureRecognizer` (`NumberOfClicksRequired = 2`) on the split view to detect divider double-clicks.
- On trigger: expands all `NSOutlineView` items, measures the widest row via `RectOfRow`, restores expansion state by
  item identity (not row index, since indices shift during expand/collapse), and repositions the divider with
  `SplitView.SetPosition`.

## Notes

- `TreeNode.Bounds.Right` already accounts for indentation, so the indent-depth math discussed in the original plan
  turned out to be unnecessary.
- Expansion-state restore on macOS is done by item identity rather than row index to avoid the index-shift bug
  called out in the plan.
- Verified: build succeeds, sidebar resizes correctly on double-click on both platforms, and node expansion state is
  preserved across the resize.

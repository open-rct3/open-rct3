// WARNING
//
// This file has been generated automatically by Rider IDE
//   to store outlets and actions made in Xcode.
// If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//

namespace Dumper.Documents
{
	[Register ("OvlViewController")]
	partial class OvlViewController
	{
		[Outlet]
		AppKit.NSTextField text { get; set; }

		[Outlet]
		AppKit.NSOutlineView outlineView { get; set; }

		[Outlet]
		AppKit.NSView statusBarView { get; set; }

		[Outlet]
		AppKit.NSTextField ovlCountLabel { get; set; }

		[Outlet]
		AppKit.NSTextField resourceCountLabel { get; set; }

		void ReleaseDesignerOutlets ()
		{
			if (text != null) {
				text.Dispose ();
				text = null;
			}

			if (outlineView != null) {
				outlineView.Dispose ();
				outlineView = null;
			}

			if (statusBarView != null) {
				statusBarView.Dispose ();
				statusBarView = null;
			}

			if (ovlCountLabel != null) {
				ovlCountLabel.Dispose ();
				ovlCountLabel = null;
			}

			if (resourceCountLabel != null) {
				resourceCountLabel.Dispose ();
				resourceCountLabel = null;
			}
		}
	}
}

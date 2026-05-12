// WARNING
//
// This file has been generated automatically by Rider IDE
//   to store outlets and actions made in Xcode.
// If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace OpenRCT3.Platforms.macOS
{
	[Register ("GameViewController")]
	partial class GameViewController
	{
		[Outlet]
		AppKit.NSView game { get; set; }

		[Outlet]
		WebKit.WKWebView inspector { get; set; }

		[Outlet]
		AppKit.NSSplitView splitView { get; set; }

		void ReleaseDesignerOutlets ()
		{
			if (game != null) {
				game.Dispose ();
				game = null;
			}

			if (inspector != null) {
				inspector.Dispose ();
				inspector = null;
			}

			if (splitView != null) {
				splitView.Dispose ();
				splitView = null;
			}

		}
	}
}

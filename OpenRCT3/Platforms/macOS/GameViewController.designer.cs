// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
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
		AppKit.NSView inspector { get; set; }

		[Outlet]
		AppKit.NSSplitView splitView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (splitView != null) {
				splitView.Dispose ();
				splitView = null;
			}

			if (inspector != null) {
				inspector.Dispose ();
				inspector = null;
			}

			if (game != null) {
				game.Dispose ();
				game = null;
			}
		}
	}
}

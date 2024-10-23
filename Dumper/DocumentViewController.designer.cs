// WARNING
//
// This file has been generated automatically by Rider IDE
//   to store outlets and actions made in Xcode.
// If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace Dumper
{
	[Register ("DocumentViewController")]
	partial class DocumentViewController
	{
		[Outlet]
		AppKit.NSTextField text { get; set; }

		void ReleaseDesignerOutlets ()
		{
			if (text != null) {
				text.Dispose ();
				text = null;
			}

		}
	}
}

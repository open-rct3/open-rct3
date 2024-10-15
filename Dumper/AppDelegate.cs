// AppDelegate
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using AppKit;
using Foundation;

namespace Dumper {
  [Register("AppDelegate")]
  public class AppDelegate : NSApplicationDelegate {
    public AppDelegate() {
      // Use our own document controller
      _ = new DocumentController();
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
    public override void DidFinishLaunching(NSNotification notification) {
      NSApplication.SharedApplication.DisableRelaunchOnLogin();
    }

    public override void WillTerminate(NSNotification notification) {
      // Insert code here to tear down your application
    }

    public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) {
      return true;
    }

    public override bool ApplicationShouldOpenUntitledFile(NSApplication sender) {
      // See https://forums.developer.apple.com/forums/thread/91781
      return false;
    }

    public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows) {
      // See https://forums.developer.apple.com/forums/thread/91781
      return false;
    }

    public override bool OpenFile(NSApplication sender, string fileName) {
      NSDocumentController.SharedDocumentController.AddDocument(new Document(fileName, out NSError? err));
      // TODO: Better error handling
      if (err != null) throw new Exception(err.ToString());
      return true;
    }
  }
}

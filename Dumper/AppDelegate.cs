// AppDelegate
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Dumper;

[Register("AppDelegate")]
public partial class AppDelegate : NSApplicationDelegate {
  public static AppDelegate Instance => (AppDelegate) NSApplication.SharedApplication.Delegate;
  public static NSDocumentController DocumentController => NSDocumentController.SharedDocumentController;

  public AppDelegate() {
#if TRACE
    Trace.Listeners.Add(new ConsoleTraceListener());
#endif

    // Use our own document controller
    _ = new DocumentController();
  }

  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
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

  [SuppressMessage("Interoperability", "CA1422:Validate platform compatibility")]
  public override bool OpenFile(NSApplication sender, string fileName) {
    // ReSharper disable once SuggestVarOrType_SimpleTypes
    NSDocumentController.SharedDocumentController
      .OpenDocument(NSUrl.FromFilename(fileName), true, out NSError? err);
    // TODO: Better error handling
    if (err != null) throw new Exception(err.ToString());
    return true;
  }

  [SuppressMessage("Performance", "CA1822:Mark members as static")]
  partial void CloseDocument(NSMenuItem sender) {
    if (NSDocumentController.SharedDocumentController.Documents.Length == 0)
      throw new InvalidOperationException("There are no open documents.");

    NSDocumentController.SharedDocumentController.CurrentDocument.Close();
    if (NSDocumentController.SharedDocumentController.Documents.Length == 0)
      sender.Enabled = false;
  }
}

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

  // Setup app here because `willFinishLaunching` is sent before `application(_:openFile:)`.
  // See https://developer.apple.com/documentation/appkit/nsapplicationdelegate/1428612-application#discussion
  public override void WillFinishLaunching(NSNotification notification) {
    NSApplication.SharedApplication.DisableRelaunchOnLogin();
  }

  public override void DidFinishLaunching(NSNotification notification) { }

  public override void WillTerminate(NSNotification notification) {
    // Insert code here to tear down your application
  }

  public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) {
    return true;
  }

  // See https://forums.developer.apple.com/forums/thread/91781
  public override bool ApplicationShouldOpenUntitledFile(NSApplication sender) {
    return false;
  }

  public override bool ApplicationShouldHandleReopen(NSApplication sender, bool hasVisibleWindows) {
    // See https://forums.developer.apple.com/forums/thread/91781
    return false;
  }

  [SuppressMessage("Interoperability", "CA1422:Validate platform compatibility")]
  public override bool OpenFile(NSApplication sender, string fileName) {
    var opened = new TaskCompletionSource<NSDocument>();
    try {
      DocumentController.OpenDocument(NSUrl.FromFilename(fileName), true,
        (document, _, err) => {
          if (err is null) opened.SetResult(document);
          else opened.SetException(err.ToException());
        });
    } catch (Exception e) {
      Trace.TraceError(e.Message);
      opened.SetException(e);
    }

    opened.Task.Wait(TimeSpan.FromMilliseconds(1500));
    if (opened.Task.IsFaulted) opened.Task.Exception.Flatten().ShowAlert();
    return opened.Task.IsCompletedSuccessfully;
  }

  public override bool OpenTempFile(NSApplication sender, string filename) {
    return false;
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

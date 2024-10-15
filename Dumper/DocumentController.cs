// DocumentController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Diagnostics.CodeAnalysis;

using ObjCRuntime;

namespace Dumper;

// See https://developer.apple.com/documentation/appkit/nsdocumentcontroller
public class DocumentController : NSDocumentController {
  public DocumentController() : base(NSObjectFlag.Empty) { }
  public DocumentController(NativeHandle handle) : base(handle) { }

  // See https://learn.microsoft.com/en-us/dotnet/api/appkit.nsdocumentcontroller.opendocument?view=xamarin-mac-sdk-14
  // See https://stackoverflow.com/a/29709860/1363247
  [SuppressMessage("Interoperability", "CA1422:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
  public override void OpenDocument(NSObject? sender) {
    var dialog = NSOpenPanel.OpenPanel;
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    dialog.DirectoryUrl = new NSUrl(home);
    dialog.AllowsMultipleSelection = false;
    //dialog.TreatsFilePackagesAsDirectories = true;
    dialog.CanCreateDirectories = false;
    dialog.ReleaseWhenClosed();
    dialog.BeginCriticalSheet(NSApplication.SharedApplication.MainWindow, result => {
      if (result != 1) return;
      base.OpenDocument(dialog.Url, true, out NSError err);
    });
  }

  public override void NewDocument(NSObject? sender) {
    base.NewDocument(sender);
  }
}

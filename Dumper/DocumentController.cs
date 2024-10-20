// DocumentController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Diagnostics.CodeAnalysis;

using ObjCRuntime;
using UniformTypeIdentifiers;

namespace Dumper;

// See https://developer.apple.com/documentation/appkit/nsdocumentcontroller
public class DocumentController : NSDocumentController {
  [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
  private readonly UTType ovlContentType = UTType.CreateFromIdentifier("com.open-rct3.ovl")!;

  public DocumentController() : base(NSObjectFlag.Empty) { }
  public DocumentController(NativeHandle handle) : base(handle) { }

  /// <summary>
  /// Prompt the user to open an OVL file.
  /// </summary>
  public void OpenDocument() {
    // ReSharper disable once IntroduceOptionalParameters.Global
    OpenDocument(null);
  }

  // See https://learn.microsoft.com/en-us/dotnet/api/appkit.nsdocumentcontroller.opendocument?view=xamarin-mac-sdk-14
  // See https://stackoverflow.com/a/29709860/1363247
  [SuppressMessage("Interoperability", "CA1422:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
  [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
  public override void OpenDocument(NSObject? sender) {
    var dialog = NSOpenPanel.OpenPanel;
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    dialog.DirectoryUrl = new NSUrl(home);
    dialog.CanCreateDirectories = false;
    dialog.AllowsMultipleSelection = false;
    dialog.AllowedContentTypes = [ovlContentType];
    //dialog.TreatsFilePackagesAsDirectories = true;
    dialog.ReleaseWhenClosed();
    dialog.BeginCriticalSheet(NSApplication.SharedApplication.MainWindow, result => {
      if (result != 1) return;
      base.OpenDocument(dialog.Url, true, out NSError err);
    });
    dialog.BecomeKeyWindow();
    dialog.MakeKeyAndOrderFront(this);
  }

  public override void NewDocument(NSObject? sender) {
    base.NewDocument(sender);
  }
}

// ProjectWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using ObjCRuntime;
using Foundation;
using AppKit;

namespace Dumper;

public partial class ProjectWindow : NSWindowController {
  public ProjectWindow(NativeHandle handle) : base(handle) { }

  public override async void WindowDidLoad() {
    Window.MakeMainWindow();
    Window.MakeKeyAndOrderFront(this);
    NSApplication.SharedApplication.RequestUserAttention(NSRequestUserAttentionType.InformationalRequest);

    await Task.Delay(500);
    // Prompt the user to open an OVL file
    NSDocumentController.SharedDocumentController.OpenDocument(this);

    base.WindowDidLoad();
  }

  [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "This app requires at least macOS 10.15")]
  public override NSDocument? Document {
    get => base.Document; set {
      Debug.Assert(base.Document != null);
      Debug.Assert(base.Document.FileUrl != null);
      this.Window.Subtitle = Path.GetFileName(base.Document.FileUrl.ToString());
      base.Document = value;
    }
  }
}

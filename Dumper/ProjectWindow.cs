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

  public override void WindowDidLoad() {
    base.WindowDidLoad();

    Debug.Assert(Window.Delegate != null);
    ((MainWindowDelegate) Window.Delegate).UpdateSubtitle += (_, e) => { Window.Subtitle = e; };

    Window.MakeMainWindow();
    NSApplication.SharedApplication.RequestUserAttention(NSRequestUserAttentionType.InformationalRequest);
  }

  public override NSDocument? Document {
    get => base.Document; set {
      Debug.Assert(base.Document != null);
      var handler = Window.Delegate as MainWindowDelegate;
      Debug.Assert(Window.Delegate != null && handler != null);
      handler.Subtitle = base.Document.FileUrl?.ToString() ?? "";
      base.Document = value;
    }
  }
}

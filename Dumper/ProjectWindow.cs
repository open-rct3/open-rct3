// ProjectWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using AppKit;
using Dumper.Models;
using Foundation;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using ObjCRuntime;

namespace Dumper;

public partial class ProjectWindow : NSWindowController {
  public ProjectWindow(NativeHandle handle) : base(handle) { }

  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public override void WindowDidLoad() {
    base.WindowDidLoad();

    Debug.Assert(Window.Delegate != null);
    var windowDelegate = (MainWindowDelegate) Window.Delegate;
    Window.Subtitle = windowDelegate.Project.Name;
    windowDelegate.Project.Renamed += (_, name) => {
      Window.Subtitle = name;
    };

    Window.MakeMainWindow();
    NSApplication.SharedApplication.RequestUserAttention(NSRequestUserAttentionType.InformationalRequest);
  }
}

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
using System.Reactive.Linq;
using System.Threading.Tasks;
using Dumper.Documents;
using ObjCRuntime;

namespace Dumper;

public sealed partial class ProjectWindow : NSWindowController {
  public ProjectWindow(NativeHandle handle) : base(handle) {
    Document = new ProjectDocument();
    Renamed = Observable.FromEventPattern<EventHandler<string>, string>(
        h => Project.Renamed += h,
        h => Project.Renamed -= h
      )
      .Select(ev => ev.EventArgs);
  }

  public Project Project {
    get {
      Debug.Assert(Document != null, nameof(Document) + " != null");
      return ((ProjectDocument) Document).Project;
    }
  }
  public readonly IObservable<string> Renamed;

  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public override void WindowDidLoad() {
    base.WindowDidLoad();

    Window.Delegate = new MainWindowDelegate(Project);
    Debug.Assert(Window.Delegate != null);
    var windowDelegate = (MainWindowDelegate) Window.Delegate;

    Window.Subtitle = Project.Name;
    Project.Renamed += (_, name) => {
      Window.Subtitle = name;
    };

    Window.MakeMainWindow();
    NSApplication.SharedApplication.RequestUserAttention(NSRequestUserAttentionType.InformationalRequest);
  }
}

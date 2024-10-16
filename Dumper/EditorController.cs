// EditorController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using ObjCRuntime;
using Foundation;
using AppKit;
using static AppKit.NSPasteboard;

namespace Dumper;

public partial class EditorController : NSViewController {
  public EditorController() : base() { }
  public EditorController(NativeHandle handle) : base(handle) { }

  [SuppressMessage("Interoperability", "CS0618", Justification = "RegisterForDraggedTypes expects a string array")]
  public override void ViewDidLoad() {
    View.RegisterForDraggedTypes( new []{ NSPasteboardTypeFileUrl.ToString() });
  }
}

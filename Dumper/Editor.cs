// Editor
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

public partial class Editor : NSView {
  public Editor() : base() { }
  public Editor(NativeHandle handle) : base(handle) { }

  public override NSDragOperation DraggingEntered(INSDraggingInfo sender) {
    // ReSharper disable once ConvertIfStatementToReturnStatement
    if (IsDroppable(sender)) return NSDragOperation.Generic;

    return base.DraggingEntered(sender);
  }

  public override bool PerformDragOperation(INSDraggingInfo sender) {
    if (!IsDroppable(sender)) return base.PerformDragOperation(sender);
    sender.AnimatesToDestination = true;
    return true;
  }

  [SuppressMessage("Interoperability", "CS0618", Justification = "The string is easier to work with.")]
  private static bool IsDroppable(INSDraggingInfo info) {
    Debug.Assert(NSPasteboardTypeFileUrl != null, nameof(NSPasteboardTypeFileUrl) + " != null");
    return info.DraggingPasteboard.PasteboardItems.Any(
      item => item.Types.Contains(NSPasteboardTypeFileUrl.ToString())
    );
  }
}

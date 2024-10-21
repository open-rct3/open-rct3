// DocumentViewController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
#if DEBUG
#define TRACE
#endif

using System;
using System.Diagnostics;
using ObjCRuntime;
using Foundation;
using AppKit;
using OVL;

namespace Dumper;

// ReSharper disable once UnusedType.Global
public partial class DocumentViewController(NativeHandle handle) : NSViewController(handle) {
  // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
  private NSDocument? Document => ViewLoaded && View.Window != null
    ? AppDelegate.DocumentController.DocumentForWindow(View.Window) : null;

  public override void ViewDidLoad() {
    base.ViewDidLoad();
    if (Document != null) RepresentedObject = Document;
  }

  public override void ViewDidAppear() {
    base.ViewDidAppear();
    if (Document != null) RepresentedObject = Document;
  }

  public override NSObject RepresentedObject {
    get => base.RepresentedObject;
    set {
      Trace.TraceInformation(value.ToString());
      base.RepresentedObject = value;
      // Update the view
      Debug.Assert(value is Document);
      text.Cell.StringValue = (value as Document)?.DisplayName ?? Ovl.UnnamedOvl;
    }
  }
}

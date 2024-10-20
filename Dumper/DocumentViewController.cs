// DocumentViewController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;

using ObjCRuntime;
using Foundation;
using AppKit;

namespace Dumper;

public partial class DocumentViewController : NSViewController {
  public DocumentViewController() { }
  public DocumentViewController(NativeHandle handle) : base(handle) { }

  public override void ViewDidLoad() {
    base.ViewDidLoad();

    // Do any additional setup after loading the view.
  }

  public override NSObject RepresentedObject {
    get {
      return base.RepresentedObject;
    }
    set {
      base.RepresentedObject = value;
      // Update the view, if already loaded.
    }
  }
}

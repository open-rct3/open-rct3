// Sidebar
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
public partial class Sidebar : NSViewController {
  public Sidebar(NativeHandle handle) : base(handle) {
  }
}

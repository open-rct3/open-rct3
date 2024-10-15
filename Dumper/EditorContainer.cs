// EditorContainer
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

public partial class EditorContainer : NSViewController {
  public EditorContainer(NativeHandle handle) : base(handle) { }
}

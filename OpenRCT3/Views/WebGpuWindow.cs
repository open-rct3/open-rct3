// WebGpuWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform;
using System;

namespace OpenRCT3.Views {
  public class WebGpuWindow : Window {
    public WebGpuWindow() {
    }
  }

  public class WebGpuControl : Control, INativeControlHostImpl {
    internal INativeControlHostDestroyableControlHandle CreateDefaultChild(IPlatformHandle parent) { }
    internal INativeControlHostControlTopLevelAttachment CreateNewAttachment(Func<IPlatformHandle, IPlatformHandle> create) { }
    internal INativeControlHostControlTopLevelAttachment CreateNewAttachment(IPlatformHandle handle) { }
    internal bool IsCompatibleWith(IPlatformHandle handle) {
      return false;
    }
  }
}

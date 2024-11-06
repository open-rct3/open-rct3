// AppDelegate
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Platforms.macOS;

[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate {
  public static AppDelegate? Instance => NSApplication.SharedApplication.Delegate as AppDelegate;

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Interoperability",
    "CA1422:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public override void DidFinishLaunching(NSNotification notification) {
    NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
  }

  public override void WillTerminate(NSNotification notification) {
    // TODO: Tear down WebGPU and other unmanaged resources
  }

  public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender) {
    return true;
  }
}

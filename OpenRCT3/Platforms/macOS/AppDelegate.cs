// AppDelegate
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Platforms.macOS;

[Register("AppDelegate")]
public class AppDelegate : NSApplicationDelegate {
  internal readonly NSTreeController inspector = new();

  static AppDelegate? Instance => NSApplication.SharedApplication.Delegate as AppDelegate;

  public override void DidFinishLaunching(NSNotification notification) {
    NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
  }

  public override void WillTerminate(NSNotification notification) {
    // TODO: Tear down WebGPU and other unamanaged resources
  }
}

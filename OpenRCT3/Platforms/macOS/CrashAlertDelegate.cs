// CrashAlertDelegate
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using AppKit;
using Foundation;
using OpenRCT3.Help;

namespace OpenRCT3.Platforms.macOS;

public class CrashAlertDelegate : NSAlertDelegate {
  public override bool ShowHelp(NSAlert alert) {
    var helpUrl = new NSUrl(Wiki.Troubleshooting);
    NSWorkspace.SharedWorkspace.OpenUrl(helpUrl);
    return true;
  }
}

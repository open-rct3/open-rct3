// CrashAlert
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using AppKit;
using Foundation;
using OpenRCT3.Platforms;
using System;

namespace OpenRCT3.Platforms.macOS;

/// <summary>
/// An alert presented to the user when an unhandled exception occurs.
/// </summary>
public class CrashAlert : NSAlert {
  public CrashAlert(Exception e) {
    Delegate = new CrashAlertDelegate();
    ShowsHelp = true;
    // TODO: Write an Apple Help Book for the game
    // TODO: HelpAnchor = "#Troubleshooting";
    MessageText = "OpenRCT3 Has Crashed";
    InformativeText = $"An unhandled exception occurred:\n\n{e.Message}";
    AlertStyle = NSAlertStyle.Critical;

    // Add accessories
    ShowsSuppressionButton = true;
    if (SuppressionButton != null)
      SuppressionButton.Title = "Do not show this error again";
    // TODO: Add an AccessoryView to send feedback?

    var abortBtn = AddButton("Abort");
    abortBtn.HasDestructiveAction = false;
    abortBtn.KeyEquivalent = "\r";

    var ignoreBtn = AddButton("Ignore");
    ignoreBtn.KeyEquivalent = "\u001b";
  }

  /// <summary>
  /// Shows the alert modally, saves suppression state, and exits or ignores
  /// the error based on the user's choice.
  /// </summary>
  /// <remarks>Must be called on the main thread.</remarks>
  public void RunAndHandle() {
    var app = NSApplication.SharedApplication;
    var config = AppConfig.Instance;

    // Queue window activation to run on the next run loop iteration so it
    // fires once the modal window is shown.
    app.BeginInvokeOnMainThread(() => {
      app.ActivateIgnoringOtherApps(true);
      Window.MakeKeyAndOrderFront(Window);
      Window.DefaultButtonCell = Buttons[0].Cell as NSButtonCell;
    });

    var response = RunModal();
    config.SuppressCrashAlerts = SuppressionButton != null && SuppressionButton.State == NSCellStateValue.On;
    config.Save();
    if (response == Convert.ToInt64(NSAlertButtonReturn.First))
      Environment.Exit(1);
  }
}

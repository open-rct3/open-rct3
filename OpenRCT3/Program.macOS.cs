// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.OVL;
using OpenRCT3.Platforms;
using OpenRCT3.Platforms.macOS;
using NLog;
using AppKit;
using System;

namespace OpenRCT3;

internal static class Program {
  private readonly static Logger Logger = LogManager.GetCurrentClassLogger();

  private static void HandleException(Exception? e) {
    if (e == null) return;
    Logger.Fatal(e, "An unhandled exception occurred.");

    var app = NSApplication.SharedApplication;
    app.InvokeOnMainThread(() => {
      // TODO: Extract everything else in this method below to a new NSAlert subclass: OpenRCT3/Platforms/macOS/CrashAlert.cs
      var config = AppConfig.Instance;

      // See https://www.jetbrains.com/help/rider/UsingStatementResourceInitialization.html
      using var alert = new NSAlert();
      alert.Delegate = new CrashAlertDelegate();
      alert.ShowsHelp = true;
      // TODO: Write an Apple Help Book for the game
      // TODO: alert.HelpAnchor = "#Troubleshooting";
      alert.MessageText = "OpenRCT3 Has Crashed";
      alert.InformativeText = $"An unhandled exception occurred:\n\n{e.Message}";
      alert.AlertStyle = NSAlertStyle.Critical;

      // Add accessories
      alert.ShowsSuppressionButton = true;
      alert.SuppressionButton?.Title = "Do not show this error again";
      // TODO: Add an AccessoryView to send feedback?

      var abortBtn = alert.AddButton("Abort");
      abortBtn.HasDestructiveAction = false;
      abortBtn.KeyEquivalent = "\r";

      var restartBtn = alert.AddButton("Ignore");
      restartBtn.KeyEquivalent = "\u001b";

      // Queue window activation to run on the next run loop iteration so it
      // fires once the modal window is shown.
      app.BeginInvokeOnMainThread(() => {
        app.ActivateIgnoringOtherApps(true);
        alert.Window.MakeKeyAndOrderFront(alert.Window);
        alert.Window.DefaultButtonCell = abortBtn.Cell as NSButtonCell;
      });

      var response = alert.RunModal();
      config.SuppressCrashAlerts = alert.SuppressionButton.State == NSCellStateValue.On;
      config.Save();
      if (response == Convert.ToInt64(NSAlertButtonReturn.First))
        Environment.Exit(1);
    });
  }

  private static AppConfig LoadConfigAndFindInstall() {
    var config = AppConfig.Load();
    if (!string.IsNullOrEmpty(config.InstallPath) && InstallFinder.Validate(config.InstallPath))
      return config;

    try {
      config.InstallPath = InstallFinder.Find(config.ExtraPaths);
      config.Save();
      return config;
    } catch (InstallNotFoundException) {
      var picker = new FolderPicker();
      var selectedPath = picker.PickFolder("Select your RCT3 Assets folder");

      if (!string.IsNullOrEmpty(selectedPath) && InstallFinder.Validate(selectedPath)) {
        config = config with { InstallPath = selectedPath };
        config.Save();
        return config;
      }

      // If validation fails or user cancels, we need to exit
      Environment.Exit(1);
      return config;
    }
  }

  [STAThread]
  public static void Main(string[] args) {
    AppDomain.CurrentDomain.UnhandledException += (sender, e) => HandleException(e.ExceptionObject as Exception);

    LogManager.Setup().LoadConfigurationFromFile("nlog.config");
    Logger.Info("Starting OpenRCT3 on macOS...");

    // ‼ This order matters!
    // NSApplication.Init() must be called before any UI elements are created.
    // LoadConfigAndFindInstall may show a dialog to the user.
    NSApplication.Init();
    LoadConfigAndFindInstall();
    InstallFinder.Find(AppConfig.Instance.ExtraPaths);
    NSApplication.SharedApplication.Delegate = new AppDelegate();
    NSApplication.Main(args);
  }
}

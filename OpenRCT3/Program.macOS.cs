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
      var config = AppConfig.Instance;
      var processPath = Environment.ProcessPath;

      // See https://www.jetbrains.com/help/rider/UsingStatementResourceInitialization.html
      using var alert = new NSAlert();
      alert.MessageText = "OpenRCT3 Error";
      alert.InformativeText = $"An unhandled exception occurred:\n\n{e.Message}";
      alert.AlertStyle = NSAlertStyle.Critical;

      // Add accessories
      // FIXME: alert.HelpAnchor = "";
      // alert.ShowsHelp = true;
      // TODO: Wire the status of this checkbox to the app settings
      alert.ShowsSuppressionButton = true;
      alert.SuppressionButton?.Title = "Do not show this error again";
      // TODO: Add an AccessoryView to send feedback?

      var abortBtn = alert.AddButton("Abort");
      abortBtn.HasDestructiveAction = false;
      abortBtn.KeyEquivalent = "\r";

      var restartBtn = alert.AddButton("Ignore");
      restartBtn.KeyEquivalent = "\u001b";

      app.ActivateIgnoringOtherApps(true);
      alert.Window.DefaultButtonCell = abortBtn.Cell as NSButtonCell;
      // FIXME: The window isn't shown at this point, so this doesn't work
      alert.Window.MakeKeyAndOrderFront(alert.Window);

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

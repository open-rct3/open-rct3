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
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  private static void HandleException(Exception? e) {
    if (e == null) return;
    logger.Fatal(e, "An unhandled exception occurred.");

    NSApplication.SharedApplication.InvokeOnMainThread(() => {
      using var alert = new CrashAlert(e);
      alert.RunAndHandle();
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
      logger.Trace("Install not found! Attempting to show folder chooser...");
      using var picker = new FolderPicker();
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
    logger.Info("Starting OpenRCT3 on macOS...");

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

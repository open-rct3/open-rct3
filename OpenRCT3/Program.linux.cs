// Program (Linux)
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using NLog;
using OpenRCT3.Platforms;
using OpenRCT3.Platforms.Linux;

namespace OpenRCT3;

internal static class Program {
  private readonly static Logger Logger = LogManager.GetCurrentClassLogger();

  private static void HandleException(Exception? e) {
    if (e == null) return;
    Logger.Fatal(e, "An unhandled exception occurred.");
    Console.Error.WriteLine($"OpenRCT3 fatal error: {e.Message}");
    Console.Error.WriteLine(e.StackTrace);
  }

  private static AppConfig LoadConfigAndFindInstall() {
    var config = AppConfig.Load();
    if (!string.IsNullOrEmpty(config.InstallPath) && InstallFinder.Validate(config.InstallPath))
      return config;

    try {
      var installPath = InstallFinder.Find(config.ExtraPaths);
      config = config with { InstallPath = installPath };
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

      Environment.Exit(1);
      return config;
    }
  }

  public static void Main(string[] args) {
    AppDomain.CurrentDomain.UnhandledException +=
      (sender, e) => HandleException(e.ExceptionObject as Exception);

    LogManager.Setup().LoadConfigurationFromFile("nlog.config");
    Logger.Info("Starting OpenRCT3 on Linux...");

    LoadConfigAndFindInstall();

    using var window = new MainWindow();
    window.Run();
  }
}

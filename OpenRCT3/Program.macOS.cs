// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms;
using OpenRCT3.Platforms.macOS;
using NLog;

namespace OpenRCT3;

internal static class Program {
  private readonly static Logger Logger = LogManager.GetCurrentClassLogger();

  [STAThread]
  public static void Main(string[] args) {
    LogManager.Setup().LoadConfigurationFromFile("nlog.config");
    Logger.Info("Starting OpenRCT3 on macOS...");

    var config = AppConfig.Load();
    if (string.IsNullOrEmpty(config.InstallPath)) {
      try {
        var installPath = InstallFinder.Find(config.ExtraPaths);
        // ReSharper disable once WithExpressionModifiesAllMembers
        config = config with { InstallPath = installPath };
        config.Save();
      }
      catch (InstallNotFoundException) {
        // TODO: Fallback to folder picker dialog
      }
    }

    NSApplication.Init();
    NSApplication.SharedApplication.Delegate = new AppDelegate();
    NSApplication.Main(args);
  }
}

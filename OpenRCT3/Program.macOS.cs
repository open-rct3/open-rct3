// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms;
using OpenRCT3.Platforms.macOS;

namespace OpenRCT3;

internal static class Program {
  [STAThread]
  public static void Main(string[] args) {
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

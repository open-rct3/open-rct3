// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms;
using OpenRCT3.Platforms.Windows;
using System;
using System.Windows.Forms;
using NLog;

namespace OpenRCT3;

internal sealed class Program {
  private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

  private static AppConfig LoadConfigAndFindInstall() {
    var config = AppConfig.Load();
    if (!string.IsNullOrEmpty(config.InstallPath) && InstallFinder.Validate(config.InstallPath)) {
      return config;
    }

    try {
      var installPath = InstallFinder.Find(config.ExtraPaths);
      config = config with { InstallPath = installPath };
      config.Save();
      return config;
    }
    catch (InstallNotFoundException) {
      Application.EnableVisualStyles();
      var picker = new FolderPicker();
      var selectedPath = picker.PickFolder("Select your RCT3 installation folder");

      if (!string.IsNullOrEmpty(selectedPath) && InstallFinder.Validate(selectedPath)) {
        config = config with { InstallPath = selectedPath };
        config.Save();
        return config;
      }

      Environment.Exit(1);
      return config;
    }
  }

  [STAThread]
  public static void Main(string[] args) {
    LogManager.Setup().LoadConfigurationFromFile("nlog.config");
    Logger.Info("Starting OpenRCT3 on Windows...");

    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    var config = LoadConfigAndFindInstall();
    var game = new Game(config);

    Application.Run(new MainForm());
  }
}

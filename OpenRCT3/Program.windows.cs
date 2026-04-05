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

namespace OpenRCT3;

internal sealed class Program {
  [STAThread]
  public static void Main(string[] args) {
    var config = AppConfig.Load();
    if (string.IsNullOrEmpty(config.InstallPath)) {
      try {
        var installPath = InstallFinder.Find(config.ExtraPaths);
        config = config with { InstallPath = installPath };
        config.Save();
      }
      catch (InstallNotFoundException) {
        // TODO: Fallback to folder picker dialog
      }
    }

    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new MainForm());
  }
}

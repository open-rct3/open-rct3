// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms.macOS;

namespace OpenRCT3;

internal static class Program {
  [STAThread]
  public static void Main(string[] args) {
    NSApplication.Init();
    NSApplication.SharedApplication.Delegate = new AppDelegate();
    NSApplication.Main(args);
  }
}

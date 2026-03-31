// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms.Windows;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System;

namespace OpenRCT3;

internal sealed class Program {
  [STAThread]
  public static void Main(string[] args) {
    var settings = new NativeWindowSettings {
      Title = "OpenRCT3",
      ClientSize = new OpenTK.Mathematics.Vector2i(624, 381),
      MinimumClientSize = new OpenTK.Mathematics.Vector2i(640, 420),
      API = ContextAPI.OpenGL,
      Profile = ContextProfile.Core,
      APIVersion = new Version(4, 0)
    };

    settings.Icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");

    using var window = new MainForm(settings);
    window.Run();
  }
}

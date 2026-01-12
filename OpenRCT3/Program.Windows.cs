// Program
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Platforms.Windows;
using Silk.NET.WebGPU;
using System;
using System.Windows.Forms;

namespace OpenRCT3;

internal sealed class Program {
  [STAThread]
  public static void Main(string[] args) {
    var gpu = WebGPU.GetApi();

    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new MainForm(gpu));
  }
}

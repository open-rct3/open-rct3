// Main
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using AppKit;

namespace Dumper;

internal static class Program {
  static void Main(string[] args) {
    // Use our own document controller
    DocumentController.Init();

    NSApplication.Init();
    NSApplication.Main(args);
  }
}

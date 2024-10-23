// Main
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using AppKit;
using Dumper.Documents;

namespace Dumper;

internal static class Program {
  private static void Main(string[] args) {
    // Use our own document controller
    DocumentController.Init();

    NSApplication.Init();
    NSApplication.Main(args);
  }
}

// Main
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using AppKit;

namespace Dumper
{
  static class MainClass
  {
    static void Main (string [] args)
    {
      NSApplication.Init ();
      NSApplication.Main (args);
    }
  }
}


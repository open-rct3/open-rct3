// FolderPicker (Linux)
//
// Authors:
//   - Nicolas Vyčas Nery <vycasnicolas@gmail.com>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenRCT3.Platforms.Linux;

// Linux folder picker. Tries the standard desktop dialogs in order:
//   1. zenity      (GNOME / GTK)
//   2. kdialog     (KDE / Qt)
// Falls back to a stdin prompt when neither dialog is available, so the app
// stays usable on headless or minimal installs.
public class FolderPicker : IFolderPicker {
  public string? PickFolder(string title, string? initialPath = null) {
    var fallbackInitial = initialPath ??
      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    var zenityArgs = new List<string> {
      "--file-selection",
      "--directory",
      $"--title={title}",
    };
    if (!string.IsNullOrEmpty(initialPath))
      zenityArgs.Add($"--filename={initialPath.TrimEnd('/')}/");
    var picked = TryRun("zenity", zenityArgs);
    if (!string.IsNullOrWhiteSpace(picked)) return picked!.Trim();

    var kdialogArgs = new List<string> {
      "--getexistingdirectory",
      fallbackInitial,
      "--title", title,
    };
    picked = TryRun("kdialog", kdialogArgs);
    if (!string.IsNullOrWhiteSpace(picked)) return picked!.Trim();

    Console.WriteLine();
    Console.WriteLine(title);
    Console.Write("Enter path (blank to cancel): ");
    var line = Console.ReadLine();
    return string.IsNullOrWhiteSpace(line) ? null : line!.Trim();
  }

  private static string? TryRun(string program, IEnumerable<string> args) {
    try {
      var psi = new ProcessStartInfo(program) {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      };
      foreach (var a in args) psi.ArgumentList.Add(a);

      using var proc = Process.Start(psi);
      if (proc == null) return null;

      var output = proc.StandardOutput.ReadToEnd();
      proc.WaitForExit();
      return proc.ExitCode == 0 ? output : null;
    } catch (Exception) {
      // Program not on PATH or otherwise unavailable; signal "no result".
      return null;
    }
  }
}

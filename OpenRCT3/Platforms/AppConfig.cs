// InstallFinder
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.IO;
using System.Text.Json;
using NLog;

namespace OpenRCT3.Platforms;

public record AppConfig {
  /// <summary>
  /// Cached path to the user's RCT3 installation.
  /// </summary>
  public string? InstallPath { get; init; }
  /// <summary>
  /// Extra paths from which to search for an installation of RCT3.
  /// </summary>
  public string[]? ExtraPaths { get; init; }

  private static string ConfigPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "OpenRCT3",
    "config.json"
  );

  public static AppConfig Load() {
    if (!File.Exists(ConfigPath)) return new AppConfig();
    try {
      var json = File.ReadAllText(ConfigPath);
      return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }
    catch (Exception) {
      // TODO: Log an error: "Could not load app config.json"
      return new AppConfig();
    }
  }

  public void Save() {
    var directory = Path.GetDirectoryName(ConfigPath);
    if (directory != null) Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(this);
    File.WriteAllText(ConfigPath, json);
  }
}

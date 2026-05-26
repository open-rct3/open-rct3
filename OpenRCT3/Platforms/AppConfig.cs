// AppConfig
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
  private static AppConfig? instance = null;
  public static AppConfig Instance => instance
    ?? throw new InvalidOperationException("App configuration is not initialized!");

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>
  /// Cached path to the user's RCT3 installation.
  /// </summary>
  public string? InstallPath { get; set; }
  /// <summary>
  /// Extra paths from which to search for an installation of RCT3.
  /// </summary>
  public string[]? ExtraPaths { get; init; }
  /// <summary>
  /// Whether to suppress unhandled exception alert modals.
  /// </summary>
  public bool SuppressCrashAlerts { get; set; }

  private static string ConfigPath => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "OpenRCT3",
    "config.json"
  );

  public static AppConfig Load() {
    if (!File.Exists(ConfigPath)) return instance = new AppConfig();
    try {
      var json = File.ReadAllText(ConfigPath);
      return instance = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    } catch (Exception e) {
      logger.Error(e, "Could not load app config.json");
      return instance = new AppConfig();
    }
  }

  public void Save() {
    var directory = Path.GetDirectoryName(ConfigPath);
    if (directory != null) Directory.CreateDirectory(directory);

    var json = JsonSerializer.Serialize(this);
    File.WriteAllText(ConfigPath, json);
  }
}

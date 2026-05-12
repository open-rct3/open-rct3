// PluginException
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Extism.Sdk;

namespace Dumper.Plugins;

/// <summary>
/// Thrown when a plugin misbehaves or otherwise malfunctions.
/// </summary>
[Serializable]
internal class PluginError(
  IPlugin plugin, string? message, Exception? innerException
) : Exception(message, innerException) {
  public IPlugin Plugin { get; init; } = plugin;
  /// <summary>
  /// Reason plugin execution failed.
  /// </summary>
  public ErrorCode Code { get; internal set; } = ErrorCode.Unknown;
  /// <summary>
  /// An error resolution hint for end-users.
  /// </summary>
  public string? Hint { get; internal set; }

  public PluginError(IPlugin plugin) : this(plugin, null, null) { }
  public PluginError(IPlugin plugin, string? message) : this(plugin, message, null) {}
}

internal enum ErrorCode {
  Unknown,
  OutOfFuel
}

// PluginInfo
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace Dumper.Plugins;

/// <summary>Metadata describing an Extism viewer plugin.</summary>
public sealed class PluginInfo {
  /// <summary>Human-readable display name (e.g. "Texture Viewer").</summary>
  public required string Name { get; init; }
  /// <summary>Semantic version string.</summary>
  public required string Version { get; init; }
  /// <summary>OVL file type tags this plugin can render (e.g. "tex", "snd").</summary>
  public required IReadOnlyList<string> FileTypes { get; init; }
  /// <summary>Filesystem path to the .wasm source.</summary>
  public required string SourcePath { get; init; }
}

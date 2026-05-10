// IViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace Dumper.Plugins;

public interface IPlugin {
  /// <summary>Human-readable display name (e.g. "Texture Viewer").</summary>
  public string Name { get; init; }
  /// <summary>Semantic version string.</summary>
  public string Version { get; init; }
  /// <summary>Filesystem path to the .wasm source.</summary>
  public string SourcePath { get; init; }
  /// <summary>Whether this plugin is currently enabled.</summary>
  bool Enabled { get; set; }
}

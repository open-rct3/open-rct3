// IViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace Dumper.Plugins;

/// <summary>A viewer plugin that can render OVL resource data as HTML.</summary>
public interface IViewerPlugin : IDisposable {
  /// <summary>Whether this plugin is currently enabled.</summary>
  bool Enabled { get; set; }
  /// <summary>Human-readable display name (e.g. "Texture Viewer").</summary>
  public string Name { get; init; }
  /// <summary>Semantic version string.</summary>
  public string Version { get; init; }
  /// <summary>OVL file type tags this plugin can render (e.g. "tex", "snd").</summary>
  public IReadOnlyList<string> FileTypes { get; init; }
  /// <summary>Filesystem path to the .wasm source.</summary>
  public string SourcePath { get; init; }
  /// <summary>OVL file type tags this plugin supports.</summary>
  IReadOnlyList<string> SupportedFileTypes { get; }
  /// <summary>Render raw resource bytes as an HTML fragment.</summary>
  /// <param name="data">Raw resource bytes extracted from the OVL archive.</param>
  /// <returns>HTML string suitable for display in a WebView.</returns>
  string Render(byte[] data);
}

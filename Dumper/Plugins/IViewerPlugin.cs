// IViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;

namespace Dumper.Plugins;

/// <summary>A viewer plugin that can render OVL resource data as HTML.</summary>
public interface IViewerPlugin : IDisposable {
  /// <summary>Whether this plugin is currently enabled.</summary>
  bool Enabled { get; set; }
  /// <summary>Plugin metadata.</summary>
  PluginInfo Info { get; }
  /// <summary>OVL file type tags this plugin supports.</summary>
  System.Collections.Generic.IReadOnlyList<string> SupportedFileTypes { get; }
  /// <summary>Render raw resource bytes as an HTML fragment.</summary>
  /// <param name="data">Raw resource bytes extracted from the OVL archive.</param>
  /// <returns>HTML string suitable for display in a WebView.</returns>
  string Render(byte[] data);
}

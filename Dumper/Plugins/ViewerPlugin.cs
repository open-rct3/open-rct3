// ViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Extism.Sdk;

namespace Dumper.Plugins;

/// <summary>An Extism WASM viewer plugin loaded from a .wasm file.</summary>
sealed class ViewerPlugin : IViewerPlugin {
  private const long DefaultFuelLimit = 50;

  private readonly CompiledPlugin _compiled;
  private Plugin _instance;

  public PluginInfo Info { get; }

  public IReadOnlyList<string> SupportedFileTypes => Info.FileTypes;

  private ViewerPlugin(CompiledPlugin compiled, Plugin instance, PluginInfo info) {
    _compiled = compiled;
    _instance = instance;
    Info = info;
  }

  /// <summary>Load a viewer plugin from a .wasm file path.</summary>
  public static ViewerPlugin Load(string wasmPath) {
    var fullPath = Path.GetFullPath(wasmPath);
    var manifest = new Manifest(new PathWasmSource(fullPath)) {
      // Host functions: none needed for viewers
    };

    var options = new PluginIntializationOptions {
      FuelLimit = DefaultFuelLimit,
      WithWasi = true,
    };

    var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
    var instance = compiled.Instantiate();

    // Read plugin metadata from WASM exports
    var name = SafeCall(instance, "name") ?? Path.GetFileNameWithoutExtension(fullPath);
    var version = SafeCall(instance, "version") ?? "0.0.0";
    var fileTypesJson = SafeCall(instance, "file_types") ?? "[]";

    List<string> fileTypes;
    try {
      fileTypes = JsonSerializer.Deserialize<List<string>>(fileTypesJson) ?? new List<string>();
    } catch (JsonException) {
      fileTypes = new List<string>();
    }

    var info = new PluginInfo {
      Name = name,
      Version = version,
      FileTypes = fileTypes,
      SourcePath = fullPath,
    };

    return new ViewerPlugin(compiled, instance, info);
  }

  /// <summary>Render raw resource bytes as HTML.</summary>
  public string Render(byte[] data) {
    // Re-instantiate if the previous instance was disposed or fuel-exhausted
    try {
      return Encoding.UTF8.GetString(_instance.Call("render", data));
    } catch (ExtismException) {
      // Fuel limit hit or plugin error — re-instantiate and retry once
      _instance.Dispose();
      _instance = _compiled.Instantiate();
      try {
        return Encoding.UTF8.GetString(_instance.Call("render", data));
      } catch (ExtismException ex) {
        return $"<div style='color:#c00;font-family:sans-serif;padding:16px'>" +
               $"<b>Plugin error:</b> {System.Net.WebUtility.HtmlEncode(ex.Message)}</div>";
      }
    }
  }

  private static string? SafeCall(Plugin plugin, string export) {
    try {
      return Encoding.UTF8.GetString(plugin.Call(export, Array.Empty<byte>()));
    } catch {
      return null;
    }
  }

  public void Dispose() {
    _instance.Dispose();
    _compiled.Dispose();
  }
}

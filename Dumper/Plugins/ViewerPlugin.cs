// ViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Text;
using System.Text.Json;
using Extism.Sdk;
using ExtismValType = Extism.Sdk.Native.ExtismValType;
using ExtismVal = Extism.Sdk.Native.ExtismVal;

namespace Dumper.Plugins;

/// <summary>An Extism WASM viewer plugin loaded from a .wasm file.</summary>
sealed class ViewerPlugin : IViewerPlugin {
  private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMilliseconds(500);
  private const int MaxPages = 16;
  private const int MaxHttpPayload = 5120; // 5MB
  private const long FuelLimit = 50;

  private readonly CompiledPlugin plugin;
  private Plugin instance;

  public bool Enabled { get; set; } = true;
  public required string Name { get; init; }
  public required string Version { get; init; }
  public required IReadOnlyList<string> FileTypes { get; init; }
  public required string SourcePath { get; init; }

  public IReadOnlyList<string> SupportedFileTypes => FileTypes;

  private ViewerPlugin(CompiledPlugin compiled, Plugin instance) {
    plugin = compiled;
    this.instance = instance;
  }

  /// <summary>Load a viewer plugin from a file path.</summary>
  public static ViewerPlugin Load(string filePath) {
    var fullPath = Path.GetFullPath(filePath);
    var manifest = new Manifest(new PathWasmSource(fullPath)) {
      AllowedHosts = ["OpenRCT3:Dumper"],
      Timeout = ScriptTimeout,
      MemoryOptions = new MemoryOptions() {
        MaxPages = MaxPages,
        // Extism's built-in key/value store.
        // WASM plugins can read/write across calls via `extism_var_get`/`extism_var_set`
        MaxVarBytes = 256,
        MaxHttpResponseBytes = MaxHttpPayload
      }
    };

    var options = new PluginIntializationOptions { FuelLimit = FuelLimit };
    var compiled = new CompiledPlugin(manifest, CreateHostFunctions(filePath), options);
    var instance = compiled.Instantiate();

    // Read plugin metadata from WASM exports
    var name = SafeCall(instance, "name") ?? Path.GetFileNameWithoutExtension(fullPath);
    var version = SafeCall(instance, "version") ?? "0.0.0";
    var fileTypesJson = SafeCall(instance, "file_types") ?? "[]";

    List<string> fileTypes;
    try {
      fileTypes = JsonSerializer.Deserialize<List<string>>(fileTypesJson) ?? [];
    } catch (JsonException) {
      fileTypes = [];
    }

    return new ViewerPlugin(compiled, instance) {
      Name = name,
      Version = version,
      FileTypes = fileTypes,
      SourcePath = fullPath,
    };
  }

  /// <summary>Render raw resource bytes as HTML.</summary>
  public string Render(byte[] data) {
    // Re-instantiate if the previous instance was disposed or fuel-exhausted
    try {
      return Encoding.UTF8.GetString(instance.Call("render", data));
    } catch (ExtismException) {
      // Fuel limit hit or plugin error — re-instantiate and retry once
      instance.Dispose();
      instance = plugin.Instantiate();
      try {
        return Encoding.UTF8.GetString(instance.Call("render", data));
      } catch (ExtismException ex) {
        return $"<div style='color:#c00;font-family:sans-serif;padding:16px'>" +
               $"<b>Plugin error:</b> {System.Net.WebUtility.HtmlEncode(ex.Message)}</div>";
      }
    }
  }

  private static string? SafeCall(Plugin plugin, string export) {
    try {
      return Encoding.UTF8.GetString(plugin.Call(export, []));
    } catch {
      return null;
    }
  }

  private static HostFunction[] CreateHostFunctions(string file) => [
    new HostFunction(
      "abort",
      [ExtismValType.I32, ExtismValType.I32, ExtismValType.I32, ExtismValType.I32],
      [],
      null,
      (plugin, inputs, _) => {
        // `env::abort` is called by AssemblyScript on assertion failures.
        // Throw so callers can surface the error.
        var msgPtr = inputs[0].v.i32;
        var msgLen = inputs[1].v.i32;
        var line   = inputs[2].v.i32;
        var col    = inputs[3].v.i32;
        var msg = msgLen > 0
          ? Encoding.UTF8.GetString(plugin.ReadBytes(msgPtr)[..msgLen])
          : "abort";
        throw new ExtismException($"Unhandled exception in plugin at {file}({line}:{col}): {msg}");
      }
    ).WithNamespace("env")
  ];

  public void Dispose() {
    instance.Dispose();
    plugin.Dispose();
  }
}

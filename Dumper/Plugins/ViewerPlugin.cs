// ViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Text;
using System.Text.Json;
using Extism.Sdk;
using ExtismValType = Extism.Sdk.Native.ExtismValType;

namespace Dumper.Plugins;

/// <summary>An Extism WASM viewer plugin loaded from a .wasm file.</summary>
sealed class ViewerPlugin : IViewerPlugin {
  private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMilliseconds(500);
  private const int MaxPages = 64;
  private const int MaxHttpPayload = 5120; // 5MB
  // Continually profile existing plugins to determine fuel limit
  private const long FuelLimit = 100_000_000;

  private readonly CompiledPlugin plugin;
  private Plugin instance;

  public bool Enabled { get; set; } = true;
  public string Name { get; init; }
  public string Version { get; init; }
  public IReadOnlyList<string> FileTypes { get; init; }
  public string SourcePath { get; init; }

  public IReadOnlyList<string> SupportedFileTypes => FileTypes;

  private ViewerPlugin(string filePath, CompiledPlugin compiled, Plugin instance) {
    plugin = compiled;
    this.instance = instance;

    // Read plugin metadata from the WASM module
    // FIXME: These calls are returning zero bytes
    Name = SafeCall("name") ?? Path.GetFileNameWithoutExtension(filePath);
    Version = SafeCall("version") ?? "Unknown";
    try {
      var json = SafeCall("file_types") ?? "[]";
      FileTypes = JsonSerializer.Deserialize<List<string>>(json) ?? [];
    } catch (JsonException) {
      FileTypes = [];
    }
    SourcePath = filePath;
  }

  /// <summary>Load a viewer plugin from a file path.</summary>
  public static ViewerPlugin Load(string filePath) {
    var modulePath = Path.GetFullPath(filePath);
    var manifest = new Manifest(new PathWasmSource(modulePath)) {
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

    return new ViewerPlugin(modulePath, compiled, instance);
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
        var msg = ex.Message.Contains("fuel", StringComparison.InvariantCultureIgnoreCase)
          ? "Plugin ran out of fuel."
          : System.Net.WebUtility.HtmlEncode(ex.Message);
        return $"<div style='color:#c00;font-family:sans-serif'><b>Plugin error:</b> {msg}</div>";
      }
    }
  }

  private string? SafeCall(string export) {
    try {
      var result = instance.Call(export, []);
      if (result.Length == 0) return null;
      return Encoding.UTF8.GetString(result);
    } catch (Exception ex) {
      if (ex is ExtismException error && error.Message.Contains("fuel", StringComparison.InvariantCultureIgnoreCase))
        throw new PluginError(this, "Plugin ran out of fuel.", error) { Code = ErrorCode.OutOfFuel };

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

// ViewerPlugin
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Text;
using System.Text.Json;
using Extism.Sdk;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using ExtismValType = Extism.Sdk.Native.ExtismValType;

namespace Dumper.Plugins;

/// <summary>An Extism WASM viewer plugin loaded from a .wasm file.</summary>
sealed class ViewerPlugin : IViewerPlugin {
  private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMilliseconds(500);
  private const int MaxPages = 64;
  private const int MaxHttpPayload = 5120; // 5MB
  // Continually profile existing plugins to determine fuel limit
  private const long FuelLimit = 100_000_000;
  // Sentinel for "not found"/"unresolved" 64-bit host-function returns that are otherwise a
  // 32-bit archive address or Extism memory offset - never a legitimate value of either.
  private const long NotFound = long.MinValue;

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
  /// <param name="getCurrentOvl">
  /// Accessor for the archive currently open in the host UI - a live reference, not a snapshot,
  /// since plugins are loaded once at startup before any archive is open (see
  /// <see cref="PluginManager.CurrentOvl"/>). Read by this plugin's "ovl" host functions on every
  /// call, so a plugin can request pointer resolution/symbol lookup/other-resource reads against
  /// whichever archive is open at the time, without the host having to pre-flatten everything
  /// the plugin might want into the initial `render(bytes)` payload.
  /// </param>
  /// <param name="getCurrentFile">
  /// Accessor for the symbol currently being rendered (see <see cref="PluginManager.CurrentFile"/>)
  /// - lets a plugin ask "current_resource_address" for its own resource's address, needed to
  /// compute field offsets (e.g. a StaticShape's <c>sh[]</c> at <c>shapeAddress + 40</c>) without
  /// widening <see cref="IViewerPlugin.Render"/>'s signature.
  /// </param>
  public static ViewerPlugin Load(string filePath, Func<Ovl?> getCurrentOvl, Func<OvlFile?> getCurrentFile) {
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
    var compiled = new CompiledPlugin(
      manifest, CreateHostFunctions(filePath, getCurrentOvl, getCurrentFile), options
    );
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

  // "ovl" host functions - let any plugin request further OVL data on demand (relocated-pointer
  // resolution, symbol lookup, other-resource reads) against whichever archive is currently open,
  // rather than the host guessing up front what a given plugin might need and pre-flattening it
  // into the initial `render(bytes)` payload. Maps directly onto Ovl's own public API
  // (TryResolveRelocation/TryGetRelocationSource/TryFindSymbol/Find+ReadResource) so decode logic
  // stays centralized in .NET (see StaticShapes.cs's sort-tail ambiguity for why reimplementing
  // format quirks twice, once per language, is worth avoiding) - plugins only walk pointers, they
  // don't reinterpret struct layouts themselves.
  private static HostFunction[] CreateHostFunctions(string file, Func<Ovl?> getCurrentOvl, Func<OvlFile?> getCurrentFile) => [
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
    ).WithNamespace("env"),

    // resolve_pointer(dataPtr: i64) -> i64 offset (NotFound if unresolved)
    // Wraps Ovl.TryResolveRelocation. Writes the resolved block's bytes *from the target offset
    // onward* into plugin memory (not the whole block) so the guest doesn't need a separate
    // "offset within block" concept - `length(offset)` (an Extism PDK built-in) gives it the
    // remaining byte count directly.
    new HostFunction(
      "resolve_pointer",
      [ExtismValType.I64],
      [ExtismValType.I64],
      null,
      (plugin, inputs, outputs) => {
        var ovl = getCurrentOvl();
        var dataPtr = Convert.ToUInt32(inputs[0].v.i64);
        if (ovl == null || !ovl.TryResolveRelocation(dataPtr, out var block, out var offset)) {
          outputs[0].v.i64 = NotFound;
          return;
        }
        var tail = block.AsSpan(Convert.ToInt32(offset)).ToArray();
        outputs[0].v.i64 = plugin.WriteBytes(tail);
      }
    ).WithNamespace("ovl"),

    // get_relocation_source(address: i64) -> i64 rawValue (NotFound if address isn't a listed
    // relocation - i.e. its bytes are unpatched placeholder data, not a real pointer).
    // Wraps Ovl.TryGetRelocationSource.
    new HostFunction(
      "get_relocation_source",
      [ExtismValType.I64],
      [ExtismValType.I64],
      null,
      (_, inputs, outputs) => {
        var ovl = getCurrentOvl();
        var address = Convert.ToUInt32(inputs[0].v.i64);
        outputs[0].v.i64 = ovl != null && ovl.TryGetRelocationSource(address, out var rawValue)
          ? rawValue
          : NotFound;
      }
    ).WithNamespace("ovl"),

    // find_symbol(dataPtr: i64) -> i64 offset (NotFound if no symbol owns that address).
    // Wraps Ovl.TryFindSymbol; writes UTF8 JSON `{"name":"...","tag":"..."}` into plugin memory.
    new HostFunction(
      "find_symbol",
      [ExtismValType.I64],
      [ExtismValType.I64],
      null,
      (plugin, inputs, outputs) => {
        var ovl = getCurrentOvl();
        var dataPtr = Convert.ToUInt32(inputs[0].v.i64);
        if (ovl == null || !ovl.TryFindSymbol(dataPtr, out var symbol)) {
          outputs[0].v.i64 = NotFound;
          return;
        }
        var json = JsonSerializer.Serialize(new { name = symbol.Name, tag = symbol.Type.ToTagString() });
        outputs[0].v.i64 = plugin.WriteBytes(Encoding.UTF8.GetBytes(json));
      }
    ).WithNamespace("ovl"),

    // read_resource(namePtr: i64, nameLen: i64, tagPtr: i64, tagLen: i64) -> i64 offset
    // (NotFound if no matching symbol). Wraps Ovl.Find + Ovl.ReadResource - for fetching e.g. an
    // ftx/txs symbol's own raw bytes once a plugin has its name from find_symbol.
    new HostFunction(
      "read_resource",
      [ExtismValType.I64, ExtismValType.I64, ExtismValType.I64, ExtismValType.I64],
      [ExtismValType.I64],
      null,
      (plugin, inputs, outputs) => {
        var ovl = getCurrentOvl();
        var name = Encoding.UTF8.GetString(plugin.ReadBytes(inputs[0].v.i64)[..Convert.ToInt32(inputs[1].v.i64)]);
        var tag = Encoding.UTF8.GetString(plugin.ReadBytes(inputs[2].v.i64)[..Convert.ToInt32(inputs[3].v.i64)]);
        var fileType = tag.ToFileType();
        var symbol = ovl?.Find(name, fileType);
        var data = symbol != null ? ovl!.ReadResource(symbol) : null;
        outputs[0].v.i64 = data != null ? plugin.WriteBytes(data) : NotFound;
      }
    ).WithNamespace("ovl"),

    // current_resource_address() -> i64 address (NotFound if unavailable). Wraps
    // Ovl.TryGetDataPointer for the symbol currently being rendered (see
    // PluginManager.CurrentFile) - lets a plugin compute its own struct's field offsets
    // (e.g. shapeAddress + 40 for StaticShape.sh) without a widened Render signature.
    new HostFunction(
      "current_resource_address",
      [],
      [ExtismValType.I64],
      null,
      (_, _, outputs) => {
        var ovl = getCurrentOvl();
        var file = getCurrentFile();
        outputs[0].v.i64 = ovl != null && file != null && ovl.TryGetDataPointer(file, out var address)
          ? address
          : NotFound;
      }
    ).WithNamespace("ovl")
  ];

  public void Dispose() {
    instance.Dispose();
    plugin.Dispose();
  }
}

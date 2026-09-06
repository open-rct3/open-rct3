using System;
using System.Text;
using Extism.Sdk;
using HostFunction = Extism.Sdk.HostFunction;
using ExtismValType = Extism.Sdk.Native.ExtismValType;

namespace OvlTestBench.Tests;

public record PluginTest(string Name, Action<string> Test);

public static class PluginTests {
  static HostFunction[] CreateAbortFunctions() {
    var inputTypes = new[] { ExtismValType.I32, ExtismValType.I32, ExtismValType.I32, ExtismValType.I32 };
    return new[] {
      new HostFunction(
        "abort",
        inputTypes,
        [],
        null,
        (plugin, inputs, outputs) => {
          var msgPtr = inputs[0].v.i32;
          var msgLen = inputs[1].v.i32;
          var line = inputs[2].v.i32;
          var col = inputs[3].v.i32;
          var message = msgLen > 0
            ? Encoding.UTF8.GetString(plugin.ReadBytes(msgPtr)[..msgLen])
            : "Abort called by plugin";
          throw new PluginException($"Plugin abort at line {line}, col {col}: {message}");
        }
      ).WithNamespace("env")
    };
  }

  public static readonly PluginTest[] All = [
    new("Compile and Instantiate", wasmPath => {
      var manifest = new Manifest(new PathWasmSource(wasmPath));
      var options = new PluginIntializationOptions { WithWasi = true };
      var compiled = new CompiledPlugin(manifest, CreateAbortFunctions(), options);
      using var instance = compiled.Instantiate();
      Assert.That(compiled != null, "Plugin loaded and instantiated successfully");
      Assert.That(instance.FunctionExists("name"), "Plugin must export name() function");
      Assert.That(instance.FunctionExists("version"), "Plugin must export version() function");
      Assert.That(instance.FunctionExists("file_types"), "Plugin must export file_types() function");
      Assert.That(instance.FunctionExists("render"), "Plugin must export render() function");
    }),
    new("Has Name", wasmPath => {
      var manifest = new Manifest(new PathWasmSource(wasmPath));
      var options = new PluginIntializationOptions { WithWasi = true };
      using var compiled = new CompiledPlugin(manifest, CreateAbortFunctions(), options);
      using var instance = compiled.Instantiate();
      var nameBytes = instance.Call("name", []);
      var name = Encoding.UTF8.GetString(nameBytes);
      Assert.That(name.Length > 0, "name() returned empty string");
    }),
    new("Has Version", wasmPath => {
      var manifest = new Manifest(new PathWasmSource(wasmPath));
      var options = new PluginIntializationOptions { WithWasi = true };
      using var compiled = new CompiledPlugin(manifest, CreateAbortFunctions(), options);
      using var instance = compiled.Instantiate();
      var versionBytes = instance.Call("version", []);
      var version = Encoding.UTF8.GetString(versionBytes);
      Assert.That(version.Length > 0, "version() returned empty string");
    }),
    new("Has File Types", wasmPath => {
      var manifest = new Manifest(new PathWasmSource(wasmPath));
      var options = new PluginIntializationOptions { WithWasi = true };
      using var compiled = new CompiledPlugin(manifest, CreateAbortFunctions(), options);
      using var instance = compiled.Instantiate();
      var jsonBytes = instance.Call("file_types", []);
      var json = Encoding.UTF8.GetString(jsonBytes);
      Assert.That(json.Length > 0, "file_types() returned empty string");
    }),
    new("Renders a View", wasmPath => {
      var manifest = new Manifest(new PathWasmSource(wasmPath));
      var options = new PluginIntializationOptions { WithWasi = true };
      using var compiled = new CompiledPlugin(manifest, CreateAbortFunctions(), options);
      using var instance = compiled.Instantiate();
      var htmlBytes = instance.Call("render", []);
      var html = Encoding.UTF8.GetString(htmlBytes);
      Assert.That(html.Length > 0, "render() returned empty string");
    }),
  ];
}

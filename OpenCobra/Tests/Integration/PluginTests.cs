using System;
using System.Text;
using Extism.Sdk;

namespace OvlTestBench.Tests;

public record PluginTest(string Name, Action<string> Test);

public static class PluginTests
{
    public static readonly PluginTest[] All = [
        new("Loads", wasmPath => {
            var manifest = new Manifest(new PathWasmSource(wasmPath));
            var options = new PluginIntializationOptions { FuelLimit = 50, WithWasi = true };
            var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
            var instance = compiled.Instantiate();
            instance.Dispose();
            compiled.Dispose();
            Assert.That(true, "");
        }),
        new("Has Name", wasmPath => {
            var manifest = new Manifest(new PathWasmSource(wasmPath));
            var options = new PluginIntializationOptions { FuelLimit = 50, WithWasi = true };
            var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
            var instance = compiled.Instantiate();
            var nameBytes = instance.Call("name", Array.Empty<byte>());
            var name = Encoding.UTF8.GetString(nameBytes);
            instance.Dispose();
            compiled.Dispose();
            Assert.That(name.Length > 0, "name() returned empty string");
        }),
        new("Has Version", wasmPath => {
            var manifest = new Manifest(new PathWasmSource(wasmPath));
            var options = new PluginIntializationOptions { FuelLimit = 50, WithWasi = true };
            var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
            var instance = compiled.Instantiate();
            var versionBytes = instance.Call("version", Array.Empty<byte>());
            var version = Encoding.UTF8.GetString(versionBytes);
            instance.Dispose();
            compiled.Dispose();
            Assert.That(version.Length > 0, "version() returned empty string");
        }),
        new("Has File Types", wasmPath => {
            var manifest = new Manifest(new PathWasmSource(wasmPath));
            var options = new PluginIntializationOptions { FuelLimit = 50, WithWasi = true };
            var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
            var instance = compiled.Instantiate();
            var jsonBytes = instance.Call("file_types", Array.Empty<byte>());
            var json = Encoding.UTF8.GetString(jsonBytes);
            instance.Dispose();
            compiled.Dispose();
            Assert.That(json.Length > 0, "file_types() returned empty string");
        }),
        new("Renders", wasmPath => {
            var manifest = new Manifest(new PathWasmSource(wasmPath));
            var options = new PluginIntializationOptions { FuelLimit = 50, WithWasi = true };
            var compiled = new CompiledPlugin(manifest, Array.Empty<HostFunction>(), options);
            var instance = compiled.Instantiate();
            var htmlBytes = instance.Call("render", Array.Empty<byte>());
            var html = Encoding.UTF8.GetString(htmlBytes);
            instance.Dispose();
            compiled.Dispose();
            Assert.That(html.Length > 0, "render() returned empty string");
        }),
    ];
}

# Dumper Plugins

Dumper plugins are [WebAssembly](https://webassembly.org) modules that must export exactly **4 functions**:

1. **`name()`** → string (display name)
2. **`version()`** → string (semantic version)
3. **`file_types()`** → string (JSON array like `'["int"]'`)
4. **`render(bytes)`** → string (HTML output)

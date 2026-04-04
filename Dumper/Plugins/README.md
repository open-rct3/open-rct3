# Dumper Plugins

Dumper plugins are [WebAssembly](https://webassembly.org) modules.

## Plugin ABI

A plugin must export exactly **4 functions**:

1. **`name()`** → string (display name)
2. **`version()`** → string (semantic version)
3. **`file_types()`** → string (JSON array like `'["int"]'`)
4. **`render(bytes)`** → string (HTML output)

### ABI Contracts

Use [XTP Schema](https://docs.xtp.dylibso.com/docs/concepts/xtp-schema)s to declare complex interfaces.

> XTP Bindgen is a system which takes an [XTP Schema](https://docs.xtp.dylibso.com/docs/concepts/xtp-schema) (a minimal, wasm-focused extension to the OpenAPI format) and generates plug-in bindings for any [...] Extism supported PDK [language]. The binding generators themselves are open source and it's easy to modify them or write your own.

[Announcing XTP Bindgen](https://extism.org/blog/announcing-xtp-bindgen) (2024) Accessed April 3, 2026

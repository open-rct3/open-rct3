# AssemblyScript Dumper Plugins Plan

## Context

Following the implementation of the Extism plugin system for OVL resource viewers (see `extism-plugin-viewers.md`), this
plan outlines creating AssemblyScript-based viewer plugins for integer (int) and text (txt) file types.

## Goals

Create Dumper plugins that:

1. Render integer data as a table (decimal, hex, binary) with hex view
2. Render text data with ASCII/UTF-16LE auto-detection and hex view
3. Follow the Extism WASM plugin contract from the parent plan

## Plugin Contract

Every viewer plugin must export:

| Export       | Signature     | Purpose                               |
| ------------ | ------------- | ------------------------------------- |
| `name`       | `() → string` | Display name, e.g. `"Integer Viewer"` |
| `version`    | `() → string` | Semantic version                      |
| `file_types` | `() → string` | JSON array: `["int"]` or `["txt"]`    |
| `render`     | `() → i32`    | Reads input via Host, outputs HTML    |

## Plugin Structure

```
plugins/
├── int-viewer/
│   ├── asconfig.json
│   ├── package.json    # depends on @extism/as-pdk
│   └── index.ts        # Integer viewer implementation
└── txt-viewer/
    ├── asconfig.json
    ├── package.json
    └── index.ts        # Text viewer implementation
```

## Build System

- Use programmatic AssemblyScript compiler API (`asc`)
- Build script: `scripts/build-plugins.ts`
- Output: `bin/plugins/*.wasm`
- Task in `deno.json`: `build:plugins`

## Data Format Assumptions

- **TXT**: Auto-detect ASCII vs UTF-16LE (null-terminated strings)
- **INT**: Raw little-endian u32 array (4 bytes per value)
- Both include hex dump view for debugging

## Implementation Steps

1. Create plugin directory structure
2. Implement `int-viewer/index.ts` with Extism AS-PDK
3. Implement `txt-viewer/index.ts` with Extism AS-PDK
4. Create build script using AssemblyScript compiler
5. Add build task to `deno.json`

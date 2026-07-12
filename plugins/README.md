# OVL Viewer Plugins

Extism-based AssemblyScript plugins for the OVL Dumper that render OVL resource files as HTML in the content panel.

## Overview

Each plugin corresponds to an OVL file type and implements the Extism plugin contract:

- `name()` → display name
- `version()` → semantic version
- `file_types()` → JSON array of supported OVL tags (e.g., `["int"]`)
- `render(bytes)` → raw resource data → HTML fragment

Plugins are discovered at startup from bundled (`bin/plugins/`) and user data directories. The first matching plugin for
each file type is the default viewer.

## Plugin Status

### ✅ Completed (6/11)

| Plugin     | Tag     | Type          | Source                                     |
| ---------- | ------- | ------------- | ------------------------------------------ |
| int-viewer | `"int"` | Integer       | Displays int32 as decimal, hex, and binary |
| txt-viewer | `"txt"` | Text          | Decodes UTF-16LE/ASCII with hex fallback   |
| spl-viewer | `"spl"` | Spline        | Bézier curve metadata and node display     |
| snd-viewer | `"snd"` | Sound         | WAVEFORMATEX audio format metadata         |
| mam-viewer | `"mam"` | Manifold Mesh | Vertex/face counts and bounding box        |
| shs-viewer | `"shs"` | Static Shape  | Bounding box/counts header plus a live per-mesh table (vertex/index counts, support type, sides, resolved `FtxRef`/`TxsRef`) |

**`shs-viewer` scope note**: `StaticShape`'s mesh/effect data and `FtxRef`/`TxsRef` all live
behind relocated pointers into *other* archive blocks - a plugin operating only on its own
resource's raw bytes (`render(bytes)`) can't dereference those. Rather than accept that limit,
Dumper's host now exposes a small set of **"ovl" host functions**
(`resolve_pointer`/`get_relocation_source`/`find_symbol`/`read_resource`/
`current_resource_address` - see `Dumper/Plugins/ViewerPlugin.cs`) that any plugin can call to
request further data from the currently-open archive on demand, wrapped by
`plugins/lib/ovl.ts`'s `Ovl` class for AssemblyScript callers. `shs-viewer` uses this to walk
`sh[]` live and render a real per-mesh table. Struct-layout/decode-quirk knowledge (e.g.
`StaticShapes.cs`'s sort-tail ambiguity) stays centralized in .NET - plugins only walk pointers
via these functions, they don't reinterpret struct layouts themselves. Full byte-level decoding
(vertices, triangles, sort-tail handling) remains `OpenCobra.OVL.Files.StaticShapes.Extract`'s
job, not this plugin's - `shs-viewer` is a summary view, not a full mesh dump.

### 📋 Planned (5/11)

Plugins are planned in priority order based on implementation difficulty (from the
[OVL Decoding plans](.opencode/plans/OVL%20Decoding/)):

| Priority | Plugin     | Tag     | Type                | Complexity     | Status  |
| -------- | ---------- | ------- | ------------------- | -------------- | ------- |
| 3        | ter-viewer | `"ter"` | Terrain             | Easy           | Planned |
| 5        | sid-viewer | `"sid"` | Scenery Item        | Difficult      | Planned |
| 6        | tex-viewer | `"tex"` | Texture             | Very Difficult | Planned |
| —        | ftx-viewer | `"ftx"` | Flexible Texture    | Difficult      | Planned |
| —        | svd-viewer | `"svd"` | Scenery Item Visual | Moderate       | Planned |

## Build

All plugins use AssemblyScript and compile to WebAssembly via the build script:

```bash
deno run --allow-all scripts/build-plugins.ts
```

Output: `bin/plugins/*.wasm` (one per plugin, ~20KB each)

## Plugin Development

### Template Structure

```
plugins/<tag>-viewer/
├── index.ts           # Plugin implementation (AssemblyScript)
├── package.json       # Metadata
└── asconfig.json      # Build configuration
```

### Example: Minimal Plugin

```typescript
import { Host } from "@extism/as-pdk";

export function name(): string {
  return "Example Viewer";
}
export function version(): string {
  return "0.1.0";
}
export function file_types(): string {
  return '["xxx"]';
}

export function render(): i32 {
  let data = Host.input();
  let html = "<h1>Data: " + data.length.toString() + " bytes</h1>";
  Host.outputString(html);
  return 0;
}
```

### Pattern: Hex View Helper

Most plugins include a hex dump for data inspection:

```typescript
function renderHexView(data: Uint8Array): string {
  let html = "<table><tr>";
  for (let i = 0; i < 16 && i < data.length; i++) {
    html += "<td>" + data[i].toString(16).padStart(2, "0") + "</td>";
  }
  html += "</tr></table>";
  return html;
}
```

## References

- [Extism AssemblyScript PDK](https://docs.extism.org/docs/write-a-plugin/as-pdk)
- [OVL Decoding Plans](.opencode/plans/OVL%20Decoding/) — format specifications and implementation difficulty ranking
- [OVL Dumper Plan](.opencode/plans/extism-plugin-viewers.md) — host-side plugin system architecture

## Next Steps

1. **Terrain viewer** (`ter-viewer`) — simple struct with color parameters
2. **Scenery Item viewer** (`sid-viewer`) — complex metadata for full scenery definitions

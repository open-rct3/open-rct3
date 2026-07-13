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

### ✅ Completed (7/11)

| Plugin     | Tag     | Type          | Source                                     |
| ---------- | ------- | ------------- | ------------------------------------------ |
| int-viewer | `"int"` | Integer       | Displays int32 as decimal, hex, and binary |
| txt-viewer | `"txt"` | Text          | Decodes UTF-16LE/ASCII with hex fallback   |
| spl-viewer | `"spl"` | Spline        | Bézier curve metadata and node display     |
| snd-viewer | `"snd"` | Sound         | WAVEFORMATEX audio format metadata         |
| mam-viewer | `"mam"` | Manifold Mesh | Vertex/face counts and bounding box        |
| shs-viewer | `"shs"` | Static Shape  | Bounding box/counts header plus a live per-mesh table (vertex/index counts, support type, sides, resolved `FtxRef`/`TxsRef`) |
| ter-viewer | `"ter"` | Terrain Type  | Metadata table (Version, Addon, Number, Type as enum name, colour swatches) with resolved `TextureRef`/`DescriptionName`/`IconName` symbol names |

**`shs-viewer` scope note**: `StaticShape`'s mesh/effect data and `FtxRef`/`TxsRef` all live
behind relocated pointers into *other* archive blocks - a plugin operating only on its own
resource's raw bytes (`render(bytes)`) can't dereference those. Rather than accept that limit,
Dumper's host now exposes a small set of **"ovl" host functions**
(`resolve_pointer`/`get_relocation_source`/`resolve_symbol_reference`/`find_symbol`/
`read_resource`/`symbol_address`/`current_resource_address` - see
`Dumper/Plugins/ViewerPlugin.cs`) that any plugin can call to request further data from the
currently-open archive on demand, wrapped by `plugins/lib/ovl.ts`'s `Ovl` class for
AssemblyScript callers. `shs-viewer` uses this to walk `sh[]` live and render a real per-mesh
table. Struct-layout/decode-quirk knowledge (e.g. `StaticShapes.cs`'s sort-tail ambiguity) stays
centralized in .NET - plugins only walk pointers via these functions, they don't reinterpret
struct layouts themselves. Full byte-level decoding (vertices, triangles, sort-tail handling)
remains `OpenCobra.OVL.Files.StaticShapes.Extract`'s job, not this plugin's - `shs-viewer` is a
summary view, not a full mesh dump.

**`resolve_symbol_reference`/`symbol_address` note**: `get_relocation_source` (the base
relocation-fixup table) only resolves pointers to *other data within the archive's own blocks* -
it does **not** resolve assignSymbolReference-driven cross-resource fields (`ftx_ref`, `txs_ref`,
`shs_ref`, `svds_ref[i]`, `name_ref`, etc.), which are populated from a separate on-disk
SymbolRefStruct table. `shs-viewer`'s `FtxRef`/`TxsRef` and `ter-viewer`'s `TextureRef` previously
used `get_relocation_source` for these and silently resolved to nothing against real archives;
both now use `resolve_symbol_reference` instead (see `OpenCobra/OVL/OVL.cs`'s
`TryResolveSymbolReference` for the underlying .NET fix). `symbol_address` is the companion for
walking *into* a different resolved resource (e.g. `sid-viewer` reading a linked SVD's own
`lods[]`), since neither `read_resource` (raw bytes only) nor `find_symbol`/
`resolve_symbol_reference` (name/tag only) exposes the target's own archive address.

### 🚧 In progress (1/11)

| Plugin     | Tag     | Type         | Source |
| ---------- | ------- | ------------ | ------ |
| sid-viewer | `"sid"` | Scenery Item | Metadata table (name/icon/group, resolved via the new `resolve_symbol_reference`/`symbol_address` host functions), an SVG placement diagram, and an LOD table (mesh type, distance, resolved shs/fts/txs refs) per linked SVD. **Known issue**: `render()` crashes the Extism JS test harness with `RangeError: Offset is outside the bounds of the DataView` — every piece of the render logic passes in isolation when bisected into standalone minimal plugins, but the crash reproduces once combined in the real file; the exact interaction hasn't been pinned down. The two `render()` tests are `Deno.test.ignore`d in `sid-viewer/index.test.ts` with a note; `name()`/`file_types()` still pass. See `.agents/plans/features/ovl/ovl-scenery-items.md`. |

### 📋 Planned (3/11)

Plugins are planned in priority order based on implementation difficulty (from the
[OVL Decoding plans](.opencode/plans/OVL%20Decoding/)):

| Priority | Plugin     | Tag     | Type                | Complexity     | Status  |
| -------- | ---------- | ------- | ------------------- | -------------- | ------- |
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

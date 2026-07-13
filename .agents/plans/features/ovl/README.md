# OVL Decoding

This directory contains plans for decoding OVL archive file types.

## Plans

| Plan                                                          | OVL Tag         | File Type        | Status      |
| ----------------------------------------------------------- | --------------- | ---------------- | ----------- |
| [ovl-terrain-types.md](./ovl-terrain-types.md)               | `"ter"`         | Terrain           | Completed   |
| [ovl-scenery-items.md](./ovl-scenery-items.md)               | `"sid"`/`"svd"` | Scenery + Visual  | Completed (decoders; sid-viewer plugin has a known render() bug) |

The `tex`/`ftx` texture pipeline and `shs` (StaticShape) decoding are done and moved out of this
directory:
[`ovl-materials-integration.md`](../../../summaries/completed-work/ovl-materials-integration.md) unified
static and animated OVL textures onto one GDK `Texture` type, on top of the separately-fixed
`tex`/`flic`/`btbl` relocation bugs
([`completed-work/ovl-texture-decoding.md`](../../../summaries/completed-work/ovl-texture-decoding.md)). The
texture pipeline is no longer a blocker for anything in this directory or for
[`grass-from-ovl.md`](../../grass-from-ovl.md).
[`ovl-static-shapes.md`](../../../summaries/completed-work/ovl-static-shapes.md) decoded `shs`
entries (`StaticShapes.Extract`, `Ovl.TryFindSymbol`, the `shs-viewer` Dumper plugin, and the
general "ovl" host-function surface future pointer-heavy decoders should reuse — see Dumper
Plugin Requirement below); `ovl-scenery-items.md`'s SHS-symbol dependency (for `svd`'s
`meshtype == 0` case) is unblocked as a result.

## Ranked by Difficulty

### 1. Easy: [ovl-terrain-types.md](./ovl-terrain-types.md)

- **Task**: Decode terrain entries (tag: `"ter"`)
- **Complexity**: ~80 lines of spec
- **Key work**: Simple struct with color parameters and texture references
- **Verdict**: Low complexity, straightforward parsing. Research on what the data means is already done —
  see [`grass-from-ovl.md`](../../../research/grass-from-ovl.md).

### 2. Most Difficult: [ovl-scenery-items.md](./ovl-scenery-items.md)

- **Task**: Decode scenery item entries (tag: `"sid"`) together with the LOD-based visual definitions they
  reference (tag: `"svd"`) — merged into one plan because they're tightly coupled in the real game (every
  `sid` holds `svd` symbol refs; an `svd` has no meaning without its owning `sid`)
- **Key work (SID)**: Extensive metadata (UI, positioning, colors, tiles, sounds, SVDs, parameters), 3 struct
  versions (v0/v1/v2), 40+ unknown fields. `sizeflag` (placement footprint/height-sampling) is confirmed to
  live here, not on `svd` — see [`scenery-placement-registry.md`](../scenery-placement-registry.md).
- **Key work (SVD)**: References StaticShape, BoneShape, or Billboard meshes with distance-based LOD
  switching and animation references; feeds `scenery-placement-registry.md`, which already keys registry
  entries on raw `svd` symbol names
- **Dependencies**: Relocation resolution, symbol reference resolution for TXT/GSI/SVD/SND/SHS/BSH/FTX/TXS/BAN/MAM
- **Verdict**: Most complex, significant unknown fields, plus cross-resource symbol-ref validation between
  SID and SVD

## Recommendation

Start with `ovl-terrain-types.md` for a quick win — under 100 lines of C#, validates the extraction pattern.
It's not on the critical path for grass texturing to *render*: `grass-from-ovl.md` found that each `ter`
entry's `texture_ref` points to a `tex` entry with the same name, and `tex` already decodes cleanly, so the
grass-texture work doesn't wait on this plan to ship. It does, however, resolve that plan's one remaining
open risk — `"Terrain_00" is grass` is currently a guess (first `tex` entry, no `Cliff` prefix, BTBL index 0),
not a verified mapping; decoding `ter` gives an authoritative `TerrainType.texture` reference instead. `grass-
from-ovl.md` proceeds with the guess and confirms visually in the meantime, so this plan is parallel
verification work, not a blocker.

## Reference Source: `rct3-importer`

Plans in this directory cite the `rct3-importer` C++ reference implementation (struct layouts,
allocation order, line numbers) by GitHub URL, but that is **not the only copy** — a local
checkout already exists as a sibling of this repo, at `../../../../../rct3-importer` (i.e.
`rct3-importer/` next to `open-rct3/`). Read struct definitions and `Manager*.cpp` decoders
directly from there (e.g. `../../../../../rct3-importer/RCT3 Importer/include/staticshape.h` and
`.../src/libOVLng/ManagerSHS.cpp` for the `shs` plan) instead of fetching the GitHub URL — it's
faster, works offline, and avoids citing line numbers from a fetch that isn't preserved anywhere
else in this repo. Verify a plan's line-number citations against this local copy before trusting
them in a fresh session, since nothing in `.agents/` vendors or caches the source itself.

## Dumper Plugin Requirement

Every OVL decoder plan in this directory must ship a matching `<tag>-viewer` Extism plugin under
[`plugins/`](../../../../plugins/) — see [`plugins/README.md`](../../../../plugins/README.md) for
the plugin contract (`name`/`version`/`file_types`/`render`) and template structure. This is not
optional follow-up work: a decoder plan's Goals section should include the plugin, and its
Post-Implementation Steps should mark the plugin `✅ Completed` in `plugins/README.md`'s status
table (moving it out of `📋 Planned`). Reference an existing plugin close in complexity to the new
decoder (e.g. `mam-viewer` for vertex/face-count-shaped data) rather than starting from the bare
template.

**Pointer-heavy resource types** (anything relying on relocated pointers for its interesting
data — `svd`, `sid`, `ftx`, and `shs` before it): don't fall back to a header-only/hex-dump-only
viewer just because `render(bytes)` only gets a resource's own raw bytes. `shs-viewer`
([`ovl-static-shapes.md`](../../../summaries/completed-work/ovl-static-shapes.md)) established a
general "ovl" host-function surface for exactly this —
`Dumper/Plugins/ViewerPlugin.cs`'s `resolve_pointer`/`get_relocation_source`/`find_symbol`/
`read_resource`/`current_resource_address`, wrapped for AssemblyScript by `plugins/lib/ovl.ts`'s
`Ovl` class — that lets a plugin request further archive data on demand against whichever archive
is currently open, without the host having to pre-flatten everything a plugin might want. Reuse
these rather than adding new per-type host functions; keep struct-layout/decode-quirk knowledge
centralized in the .NET decoder (plugins should only walk pointers via these functions, not
reinterpret struct layouts themselves — see `StaticShapes.cs`'s sort-tail ambiguity for why that
matters).

## Testing Approach

The `TestRunner`/`OpenCobra/Tests/TestRunner/Tests/Read<Feature>.cs`/`OvlTest[]` pattern these plans originally
specified no longer exists in the codebase. Current convention (see
`completed-work/ovl-materials-integration.md`'s test plan for a live example):

1. NUnit unit tests in `OpenCobra/Tests/OVL/<Feature>Tests.cs` — synthetic struct input, no `RCT3_PATH` needed.
2. Real-archive checks added to `OpenCobra/Tests/Integration/ExtractResources.cs`, gated by `RCT3_PATH`.
3. Run `make test` (unit tests) per `AGENTS.md`; the integration suite is separate and only runs with real
   game data present.

## Production OVLs Discovery

After implementing each decoder, the results shall document:

1. **Which production OVLs contain entries** of that type (tag)
2. **Common vs unique** archive distribution
3. **Sample symbol names** for verification

This is tracked in the `## Production OVLs with Entries` section at the end of each plan and is updated once
scanning/verification is complete.

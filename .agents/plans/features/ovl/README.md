# OVL Decoding

This directory contains plans for decoding OVL archive file types.

## Plans

| Plan                                                          | OVL Tag | File Type | Status      |
| ----------------------------------------------------------- | ------- | --------- | ----------- |
| [ovl-terrain-types.md](./ovl-terrain-types.md)               | `"ter"` | Terrain   | Not started |
| [ovl-static-shapes.md](./ovl-static-shapes.md)               | `"shs"` | Shape     | Not started |
| [ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md) | `"svd"` | Visual    | Not started |
| [ovl-scenery-items.md](./ovl-scenery-items.md)               | `"sid"` | Scenery   | Not started |

The `tex`/`ftx` texture pipeline is done and moved out of this directory:
[`ovl-materials-integration.md`](../../../summaries/completed-work/ovl-materials-integration.md) unified
static and animated OVL textures onto one GDK `Texture` type, on top of the separately-fixed
`tex`/`flic`/`btbl` relocation bugs
([`completed-work/ovl-texture-decoding.md`](../../../summaries/completed-work/ovl-texture-decoding.md)). The
texture pipeline is no longer a blocker for anything in this directory or for
[`grass-texture-from-terrain-ovl.md`](../../grass-texture-from-terrain-ovl.md).

Two other plans that previously lived here were deleted as superseded by real implementations that diverged
from their proposed design: `ovl-flexible-textures.md` (superseded by
[`FlexiTexture.cs`](../../../../OpenCobra/OVL/Files/FlexiTexture.cs), now an input the materials-integration
work above consumed) and `ovl-textures.md` (its proposed `ReadResource`/`ReadBitmapTable` rewrite didn't match
current `OVL.cs`/`BitmapTable.cs`; the real texture bugs were tracked and fixed as
`completed-work/ovl-texture-decoding.md` above; the still-open
[`ovl-resource-relocation.md`](../../../bugs/ovl-resource-relocation.md) covers the separate `mms`/`prt`/
`psi`/`fct` symbol-resolution issue, unaffected by the texture pipeline fix).

## Ranked by Difficulty

### 1. Easy: [ovl-terrain-types.md](./ovl-terrain-types.md)

- **Task**: Decode terrain entries (tag: `"ter"`)
- **Complexity**: ~80 lines of spec
- **Key work**: Simple struct with color parameters and texture references
- **Verdict**: Low complexity, straightforward parsing. Research on what the data means is already done —
  see [`grass-texture-from-terrain-ovl.md`](../../../research/grass-texture-from-terrain-ovl.md).

### 2. Moderately Difficult: [ovl-static-shapes.md](./ovl-static-shapes.md)

- **Task**: Decode static 3D shape entries (tag: `"shs"`)
- **Complexity**: 118 lines of spec
- **Key work**: Two-level struct hierarchy (`StaticShape` → `StaticShapeMesh[]`), vertex/index arrays, symbol refs to
  FTX/TXS
- **Dependencies**: Relocation resolution, symbol reference resolution
- **Verdict**: Multi-level pointer chasing, requires cross-block data access

### 3. More Difficult: [ovl-scenery-items.md](./ovl-scenery-items.md)

- **Task**: Decode scenery item entries (tag: `"sid"`)
- **Complexity**: 149 lines of spec
- **Key work**: Extensive metadata (UI, positioning, colors, tiles, sounds, SVDs, parameters), 3 struct versions
  (v0/v1/v2), 40+ unknown fields. `sizeflag` (placement footprint/height-sampling) is confirmed to live here,
  not on `svd` — see [`scenery-placement-registry.md`](../scenery-placement-registry.md).
- **Dependencies**: Relocation resolution, symbol reference resolution for TXT/GSI/SVD/SND
- **Verdict**: Second-most complex, significant unknown fields

### 4. Also unranked-difficulty: [ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md)

- **Task**: Decode LOD-based visual definitions (tag: `"svd"`) referencing StaticShape, BoneShape, or
  Billboard meshes.
- Feeds [`scenery-placement-registry.md`](../scenery-placement-registry.md), which already keys registry
  entries on raw `svd` symbol names.

## Recommendation

Start with `ovl-terrain-types.md` for a quick win — under 100 lines of C#, validates the extraction pattern.
Note it's no longer on the critical path for grass texturing: `grass-texture-from-terrain-ovl.md` found that
each `ter` entry's `texture_ref` points to a `tex` entry with the same name, and `tex` already decodes
cleanly, so the grass-texture work doesn't wait on this plan. `ter` decoding is still worth doing for its own
sake (proper `TerrainType` metadata), just not blocking.

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

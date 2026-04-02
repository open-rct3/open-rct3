# OVL Decoding

This directory contains plans for decoding OVL archive file types. Six are [ranked below](#implementation-difficulty) by implementation difficulty; five additional plans are documented but unranked here.

## Plans

| Plan                                                         | OVL Tag | File Type | Status  |
| ------------------------------------------------------------ | ------- | --------- | ------- |
| [ovl-integers.md](./ovl-integers.md)                         | `"int"` | Integer   | Done    |
| [ovl-texts.md](./ovl-texts.md)                               | `"txt"` | Text      | Planned |
| [ovl-sounds.md](./ovl-sounds.md)                             | `"snd"` | Sound     | Planned |
| [ovl-splines.md](./ovl-splines.md)                           | `"spl"` | Spline    | Planned |
| [ovl-terrain-types.md](./ovl-terrain-types.md)               | `"ter"` | Terrain   | Planned |
| [ovl-manifold-meshes.md](./ovl-manifold-meshes.md)           | `"mam"` | Mesh      | Planned |
| [ovl-static-shapes.md](./ovl-static-shapes.md)               | `"shs"` | Shape     | Planned |
| [ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md) | `"svd"` | Visual    | Planned |
| [ovl-flexible-textures.md](./ovl-flexible-textures.md)       | `"ftx"` | Texture   | Planned |
| [ovl-scenery-items.md](./ovl-scenery-items.md)               | `"sid"` | Scenery   | Planned |
| [ovl-textures.md](./ovl-textures.md)                         | `"tex"` | Texture   | Planned |

## Implementation Difficulty

### 1. Done: [ovl-integers.md](./ovl-integers.md)

- **Task**: Decode integer entries (tag: `"int"`)
- **Complexity**: 95 lines of spec
- **Key work**: Read 4 bytes, interpret as `int32`
- **Dependencies**: None beyond existing infrastructure
- **Verdict**: Trivial — just reads raw bytes
- **Status**: ✅ Implemented (2026-04-02)

### 2. Easy: [ovl-texts.md](./ovl-texts.md)

- **Task**: Decode text entries (tag: `"txt"`)
- **Complexity**: 71 lines of spec
- **Key work**: Read raw bytes, decode as UTF-16LE until null terminator
- **Dependencies**: `System.Text.Encoding.Unicode`
- **Verdict**: Simple byte decoding, no structures

### 3. Moderately Difficult: [ovl-splines.md](./ovl-splines.md)

- **Task**: Decode spline/Bézier curve entries (tag: `"spl"`)
- **Complexity**: 110 lines of spec
- **Key work**: Parse `Spline` struct with node arrays, control points, segment lengths
- **Dependencies**: Relocation resolution
- **Verdict**: Well-defined structure, straightforward parsing

### 4. Difficult: [ovl-static-shapes.md](./ovl-static-shapes.md)

- **Task**: Decode static 3D shape entries (tag: `"shs"`)
- **Complexity**: 118 lines of spec
- **Key work**: Two-level struct hierarchy (`StaticShape` → `StaticShapeMesh[]`), vertex/index arrays, symbol refs to FTX/TXS
- **Dependencies**: Relocation resolution, symbol reference resolution
- **Verdict**: Multi-level pointer chasing, requires cross-block data access

### 5. More Difficult: [ovl-scenery-items.md](./ovl-scenery-items.md)

- **Task**: Decode scenery item entries (tag: `"sid"`)
- **Complexity**: 149 lines of spec
- **Key work**: Extensive metadata (UI, positioning, colors, tiles, sounds, SVDs, parameters), 3 struct versions (v0/v1/v2), 40+ unknown fields
- **Dependencies**: Relocation resolution, symbol reference resolution for TXT/GSI/SVD/SND
- **Verdict**: Second-most complex, significant unknown fields

## For Context: The Most Difficult Plan

### 6. Most Difficult: [ovl-textures.md](./ovl-textures.md)

- **Task**: Decode texture entries (TEX/FLIC/BTBL system)
- **Complexity**: 386 lines of spec
- **Key work**: 22 texture formats, BTBL vs direct FLIC layouts, mipmap parsing, DXT compression detection
- **Dependencies**: `System.Drawing`, relocation resolution, format-specific block size calculations
- **Why hardest**: Requires understanding two data layouts (BTBL vs FLIC), 22 format codes, compressed/uncompressed paths, mip level extraction
- **Verdict**: Most research-intensive plan, extensive format table, multiple code paths

## Unranked Plans

These plans are documented but not yet ranked:

- **[ovl-flexible-textures.md](./ovl-flexible-textures.md)** — Palette-based animated textures (FTX) with frames, palettes, and alpha channels. Similar complexity to textures plan.
- **[ovl-manifold-meshes.md](./ovl-manifold-meshes.md)** — Raw 3D mesh data (MAM) with vertices and indices. Relatively simple struct, low complexity.
- **[ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md)** — LOD-based visual definitions (SVD) referencing StaticShape, BoneShape, or Billboard meshes.
- **[ovl-sounds.md](./ovl-sounds.md)** — PCM audio data (SND) with WAVEFORMATEX headers and stereo channels.
- **[ovl-terrain-types.md](./ovl-terrain-types.md)** — Terrain definitions (TER) with color parameters and texture references.

## Summary Table

| Rank | Plan                 | Lines | Status  | Key Challenge                |
| ---- | -------------------- | ----- | ------- | ---------------------------- |
| 1    | ovl-integers.md      | 95    | Done    | Trivial byte reading         |
| 2    | ovl-texts.md         | 71+   | Planned | UTF-16LE decoding            |
| 3    | ovl-splines.md       | 110+  | Planned | Bézier curve structs         |
| 4    | ovl-static-shapes.md | 118+  | Planned | Multi-level mesh hierarchy   |
| 5    | ovl-scenery-items.md | 149+  | Planned | Complex metadata, 3 versions |
| 6    | ovl-textures.md      | 386+  | Planned | 22 formats, 2 layouts        |

## Recommendation

Start with `ovl-integers.md` for a quick win, then `ovl-texts.md`. Both can be implemented in under 100 lines of C# and validate the extraction pattern before tackling more complex file types.

## Testing Approach

All tests for OVL decoding implementations are created as new test files in `OpenCobra/Tests/TestRunner/Tests/`, not to NUnit unit tests. Each plan includes a TestRunner test file template following the existing pattern:

1. Create new file: `OpenCobra/Tests/TestRunner/Tests/Read<Feature>.cs`
2. Each file contains a static class with `OvlTest[] All` array
3. Tests use `Assert.That(condition, message)` and `Assert.Result(name)`
4. Tests are registered in the test runner to run against OVL pairs
5. Run the TestRunner before and after implementation to verify

## Production OVLs Discovery

After implementing each decoder, the results shall document:

1. **Which production OVLs contain entries** of that type (tag)
2. **Common vs unique** archive distribution
3. **Sample symbol names** for verification

This is tracked in the `## Production OVLs with Entries` section at the end of each plan and is updated once scanning/verification is complete.

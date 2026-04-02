# OVL Decoding

This directory contains plans for decoding OVL archive file types. Six are [ranked below](#implementation-difficulty) by implementation difficulty; five additional plans are documented but unranked here.

## Plans

| File                                                         | OVL Tag | File Type |
| ------------------------------------------------------------ | ------- | --------- |
| [ovl-integers.md](./ovl-integers.md)                         | `"int"` | Integer   |
| [ovl-texts.md](./ovl-texts.md)                               | `"txt"` | Text      |
| [ovl-sounds.md](./ovl-sounds.md)                             | `"snd"` | Sound     |
| [ovl-splines.md](./ovl-splines.md)                           | `"spl"` | Spline    |
| [ovl-terrain-types.md](./ovl-terrain-types.md)               | `"ter"` | Terrain   |
| [ovl-manifold-meshes.md](./ovl-manifold-meshes.md)           | `"mam"` | Mesh      |
| [ovl-static-shapes.md](./ovl-static-shapes.md)               | `"shs"` | Shape     |
| [ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md) | `"svd"` | Visual    |
| [ovl-flexible-textures.md](./ovl-flexible-textures.md)       | `"ftx"` | Texture   |
| [ovl-scenery-items.md](./ovl-scenery-items.md)               | `"sid"` | Scenery   |
| [ovl-textures.md](./ovl-textures.md)                         | `"tex"` | Texture   |

## Implementation Difficulty

### 1. Easiest: [ovl-integers.md](./ovl-integers.md)

- **Task**: Decode integer entries (tag: `"int"`)
- **Complexity**: 70 lines of spec
- **Key work**: Read 4 bytes, interpret as `int32`
- **Dependencies**: None beyond existing infrastructure
- **Verdict**: Trivial — just reads raw bytes

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

| Rank | Plan                 | Lines | Key Challenge                |
| ---- | -------------------- | ----- | ---------------------------- |
| 1    | ovl-integers.md      | 70    | Trivial byte reading         |
| 2    | ovl-texts.md         | 71    | UTF-16LE decoding            |
| 3    | ovl-splines.md       | 110   | Bézier curve structs         |
| 4    | ovl-static-shapes.md | 118   | Multi-level mesh hierarchy   |
| 5    | ovl-scenery-items.md | 149   | Complex metadata, 3 versions |
| 6    | ovl-textures.md      | 386   | 22 formats, 2 layouts        |

## Recommendation

Start with `ovl-integers.md` for a quick win, then `ovl-texts.md`. Both can be implemented in under 100 lines of C# and validate the extraction pattern before tackling more complex file types.

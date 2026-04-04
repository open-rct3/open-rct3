# OVL Decoding

This directory contains plans for decoding OVL archive file types. Six are documented but not yet ranked by implementation difficulty.

## Plans

| Plan                                                         | OVL Tag | File Type |
| ------------------------------------------------------------ | ------- | --------- |
| [ovl-terrain-types.md](./ovl-terrain-types.md)               | `"ter"` | Terrain   |
| [ovl-static-shapes.md](./ovl-static-shapes.md)               | `"shs"` | Shape     |
| [ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md) | `"svd"` | Visual    |
| [ovl-flexible-textures.md](./ovl-flexible-textures.md)       | `"ftx"` | Texture   |
| [ovl-scenery-items.md](./ovl-scenery-items.md)               | `"sid"` | Scenery   |
| [ovl-textures.md](./ovl-textures.md)                         | `"tex"` | Texture   |

## Ranked by Difficulty

### 1. Easy: [ovl-terrain-types.md](./ovl-terrain-types.md)

- **Task**: Decode terrain entries (tag: `"ter"`)
- **Complexity**: ~80 lines of spec
- **Key work**: Simple struct with color parameters and texture references
- **Verdict**: Low complexity, straightforward parsing

### 2. Moderately Difficult: [ovl-static-shapes.md](./ovl-static-shapes.md)

- **Task**: Decode static 3D shape entries (tag: `"shs"`)
- **Complexity**: 118 lines of spec
- **Key work**: Two-level struct hierarchy (`StaticShape` → `StaticShapeMesh[]`), vertex/index arrays, symbol refs to FTX/TXS
- **Dependencies**: Relocation resolution, symbol reference resolution
- **Verdict**: Multi-level pointer chasing, requires cross-block data access

### 3. More Difficult: [ovl-scenery-items.md](./ovl-scenery-items.md)

- **Task**: Decode scenery item entries (tag: `"sid"`)
- **Complexity**: 149 lines of spec
- **Key work**: Extensive metadata (UI, positioning, colors, tiles, sounds, SVDs, parameters), 3 struct versions (v0/v1/v2), 40+ unknown fields
- **Dependencies**: Relocation resolution, symbol reference resolution for TXT/GSI/SVD/SND
- **Verdict**: Second-most complex, significant unknown fields

### 4. Most Difficult: [ovl-textures.md](./ovl-textures.md)

- **Task**: Decode texture entries (TEX/FLIC/BTBL system)
- **Complexity**: 386 lines of spec
- **Key work**: 22 texture formats, BTBL vs direct FLIC layouts, mipmap parsing, DXT compression detection
- **Dependencies**: `System.Drawing`, relocation resolution, format-specific block size calculations
- **Verdict**: Most research-intensive plan, extensive format table, multiple code paths

## Unranked Plans

These plans are documented but not yet ranked:

- **[ovl-flexible-textures.md](./ovl-flexible-textures.md)** — Palette-based animated textures (FTX) with frames, palettes, and alpha channels. Similar complexity to textures plan.
- **[ovl-scenery-item-visuals.md](./ovl-scenery-item-visuals.md)** — LOD-based visual definitions (SVD) referencing StaticShape, BoneShape, or Billboard meshes.

## Summary Table

| Rank | Plan                 | Lines | Key Challenge                |
| ---- | -------------------- | ----- | ---------------------------- |
| 1    | ovl-terrain-types.md | ~80   | Color parameters, texture refs |
| 2    | ovl-static-shapes.md | 118+  | Multi-level mesh hierarchy   |
| 3    | ovl-scenery-items.md | 149+  | Complex metadata, 3 versions |
| 4    | ovl-textures.md      | 386+  | 22 formats, 2 layouts        |

## Recommendation

Start with `ovl-terrain-types.md` for a quick win. Both can be implemented in under 100 lines of C# and validate the extraction pattern before tackling more complex file types.

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

# Porting libOVL to C\#

This document summarizes the effort to port
[libOVL](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLng), the C++ OVL archive parsing
library from [rct3-importer](https://github.com/chances/rct3-importer), to C# in the [OpenCobra/OVL](../OpenCobra/OVL/)
library.

## Background

The OVL binary format is Frontier's resource archive used by RollerCoaster Tycoon 3. Archives come in pairs: a
**common** file (shared resources) and a **unique** file (archive-specific resources). The binary format contains:

1. **Header** — magic (`FGRK`), version (1, 4, or 5), reference count
2. **Loader headers** — resource type descriptors with a `loader` class, `name`, `type` index, and `tag` (e.g. `"tex"`,
   `"sid"`)
3. **File blocks** — 9 blocks of binary data forming a virtual address space
4. **Relocations** — cross-reference pointers resolving virtual addresses between blocks and across the common/unique
   pair
5. **Post-relocation data** — version-specific trailing metadata

## Changes

> [!NOTE]
> **AI-Assisted Porting:** Significant portions of the analysis, design, and implementation described in this document
> were produced with the assistance of AI tools. All AI-generated code was reviewed, tested, and validated against the
> original C++ reference. See the [AI Usage](#ai-usage) section for details on providers and models used.

### OVL Binary Reading (`OpenCobra/OVL/OVL.cs`)

#### Bug Fixes

The initial prototype code had several bugs relative to the
[C++ reference](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/OVLDump.cpp):

| Bug                                                        | Fix                                                                                                                                    |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| `header.version != 4 \|\| header.version != 5` always true | Changed `\|\|` to `&&`                                                                                                                 |
| Read 2-char "checksum" after relocations (not in format)   | Removed; not present in binary format                                                                                                  |
| Skipped 4 bytes after relocations (bogus)                  | Removed; reference does not skip                                                                                                       |
| No post-relocation unknowns for v4/v5                      | Added per [reference's `ReadFile`](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/OVLDump.cpp#L227) |
| `ReadInt64()` for symbol counts (8 bytes per field)        | Changed to `ReadUInt32()` — fields are `uint` (4 bytes)                                                                                |
| `LoaderHeader.type` stored as `long`                       | Changed to `int` (32-bit, matching `ReadInt32()`)                                                                                      |
| `LoaderHeader.symbolCount` stored as `long`                | Changed to `uint` (32-bit, matching `ReadUInt32()`)                                                                                    |
| `symbolCountOrder` never assigned                          | Now assigned from loop index                                                                                                           |

#### Refactoring

- Added `Debug.Assert` assertions at key parsing boundaries matching libOVL's invariants, especially:
  - Assert stream position before each string read, matching the `c_data < m_data[type] + m_size[type]` invariant from
    the
    [C++ reference](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/OVLDump.cpp#L116)
- Removed unused `FileBlock` internal struct, instance `file`/`fileSize`/`reader` fields, and dead constructors

#### Paired Archive Loading (`Ovl.Load`)

Implemented a `Load(string commonPath)` method that mirrors
[`cOVLDump::Load`](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/OVLDump.cpp#L42):

1. Read both common and unique files via `Read()`
2. Resolve relocations across the combined virtual address space
3. Parse string table (block 0) — mirrors `MakeStrings`
4. Parse symbol table (block 2, sub-block 0) — mirrors `MakeSymbols`
5. Parse loader table (block 2, sub-block 1) — mirrors `MakeLoaders`

#### On-Disk Struct Definitions

Added 32-bit struct equivalents for the C++ types in
[ovlstructs.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/ovlstructs.h), using `uint`
instead of pointer types to remain safe on 64-bit hosts:

- `SymbolStruct` / `SymbolStruct2` — symbol table entries (v1 / v4/v5)
- `LoaderStruct` — loader table entries
- `SymbolRefStruct` / `SymbolRefStruct2` — symbol reference entries (v1 / v4/v5)

### File Type Enumeration (`OpenCobra/OVL/Files/FileTypes.cs`)

Expanded `FileType` from 8 to 29 members, covering all loader tags from
[libOVLng's Manager classes](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLng). Each tag
maps to a `FileType` via `ToFileType()` and a human-readable display name via `ToDisplayName()`.

Key correction: the Flexi-Texture tag is `"ftx"` (per
[`ManagerFTX.h`](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLng/ManagerFTX.h)), not
`"flt"`. The `"flt"` tag is retained as a backwards-compat alias.

| Tag    | FileType          | Display Name        |
| ------ | ----------------- | ------------------- |
| `txt`  | Text              | Text                |
| `int`  | Integer           | Integer Number      |
| `tex`  | Texture           | 2D Texture          |
| `flic` | Flic              | Compressed 2D Image |
| `ftx`  | FlexibleTexture   | Flexi-Texture       |
| `gsi`  | GuiSkinItem       | GUI Skin Item       |
| `sid`  | SceneryItem       | Scenery Item        |
| `btbl` | BitmapTable       | Bitmap Table        |
| `anr`  | AnimatedRide      | Animated Ride       |
| `ban`  | BoneAnim          | Bone Animation      |
| `bsh`  | BoneShape         | Bone Shape          |
| `ced`  | CarriedItemExtra  | Carried Item Extra  |
| `chg`  | ChangingRoom      | Changing Room       |
| `cid`  | CarriedItem       | Carried Item        |
| `mam`  | ManifoldMesh      | Manifold Mesh       |
| `ptd`  | PathType          | Path Type           |
| `qtd`  | QueueType         | Queue Type          |
| `ric`  | RideCar           | Ride Car            |
| `rit`  | RideTrain         | Ride Train          |
| `sat`  | SpecialAttraction | Special Attraction  |
| `shs`  | StaticShape       | Static Shape        |
| `snd`  | Sound             | Sound               |
| `spl`  | Spline            | Spline              |
| `sta`  | Stall             | Stall               |
| `svd`  | SceneryItemVisual | Scenery Item Visual |
| `ter`  | TerrainType       | Terrain Type        |
| `tks`  | TrackSection      | Track Section       |
| `trr`  | TrackedRide       | Tracked Ride        |
| `wai`  | WildAnimalItem    | Wild Animal Item    |

### Dumper Tree View (`Dumper/`)

#### Windows (`MainForm.cs`)

Replaced the stub `LoadOvl()` with tree-view population that:

- Groups `LoaderHeaders` by tag, converts each tag to `FileType` via `ToFileType()`
- Orders groups by `FileType` enum value (Unknown last)
- Uses `ToDisplayName()` for human-readable group labels

#### macOS (`OvlViewController.cs`)

Added `NSOutlineViewDataSource` implementation with the same grouping logic, backed by `OvlTreeItem` / `OvlTreeItemNode`
wrapper types.

## Testing

21 NUnit tests in [`OpenCobra/OVL Tests/ReadArchive.cs`](../OpenCobra/OVL%20Tests/ReadArchive.cs) covering:

- Archive reading for common and unique OVLs
- File type classification
- Loader header field validity (name, tag, type width, symbol count width)
- Tag ASCII validity and length
- Files array length (9 blocks)
- Description and references population

## AI Usage

This porting effort made extensive use of AI tools for code analysis, design, and implementation.

### Providers and Models

- **[Claude](https://www.anthropic.com/claude)** — "Sonnet 4.6" model. Used for initial analysis of the C++ reference
  implementation, architectural design of the C# adaptation, and generating the first drafts of struct definitions and
  post-processing methods.
- **[OpenCode](https://opencode.ai)** — "MiMo V2 Pro Free" and "MiMo V2 Omni Free" models. Used for implementing code
  changes, running builds and tests, iterating on bug fixes, and validating the implementation against the test suite.

### Limitations

AI-generated code required manual review and correction for:

- Integer width mismatches (32-bit vs 64-bit fields in binary format)
- Incorrect tag mappings (e.g. `"bmptbl"` vs actual `"btbl"` from the reference)
- Tree-view grouping logic (reverse mapping direction)
- Missing field assignments (`symbolCountOrder`)

All changes were validated against embedded test OVL archives (`style.common.ovl`, `style.unique.ovl`) via the NUnit
test suite.

## References

- [rct3-importer](https://github.com/chances/rct3-importer) — RCT3 custom scenery importer, original libOVL by
  [belgabor](https://github.com/Belgabor)
- [libOVLng source](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLng) — Manager classes
  defining all loader tags
- [libOVLDump source](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLDump) — Archive
  reading and dumping implementation
- [ovlstructs.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/ovlstructs.h) — On-disk
  struct definitions
- [ovldumperstructs.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/ovldumperstructs.h)
  — In-memory parsed structure types

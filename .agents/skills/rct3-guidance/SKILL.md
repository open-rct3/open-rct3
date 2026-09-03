---
name: rct3-guidance
description: Guidance on RCT3 OVL structure, libOVL porting details, texture/bitmap relationships, and renderer control flow.
---

# RCT3 OVL and Renderer Guidance

This skill provides comprehensive guidance on the OVL archive format, the C# port of the `libOVL` parser, and the application's renderer control flow within the OpenRCT3 codebase.

## Reference Implementations & Working Examples

- **Adapt working examples end-to-end**, rather than inferring or deriving file formats from scratch. Reference implementations (e.g. `rct3-importer`'s `libOVLng`, `rct3tex.cpp`) already solve these formats correctly - port their logic directly instead of reverse-engineering it from raw byte dumps.
- **Read reference implementations FIRST.** Do NOT guess at clumsy changes or trial-and-error before understanding a problem. If a reference implementation exists for the code you're touching, read the relevant source before writing a single line - not after a guess fails.

---

## 1. OVL Archive & Resource Architecture

Detailed binary layout specifications is described in the [Archive Format](../../../docs/ovl/archive-format.md) documentation.

### Coordinate System
RollerCoaster Tycoon 3 and OpenRCT3 use a right-handed **Y-up** coordinate system:
*   **+X**: East / Lateral Right
*   **+Y**: Up / Elevation
*   **+Z**: North / Longitudinal Forward
*   **XZ Plane**: Horizontal ground plane

### Paired OVL Files
OVL archives in RollerCoaster Tycoon 3 are split into paired files:
*   **Common OVL (`.common.ovl`)**: Contains shared resources referenced by multiple archives (typically has string tables).
*   **Unique OVL (`.unique.ovl`)**: Contains archive-specific resources (often has an empty block 0 string table).

When loaded using `Ovl.Load()`, they merge into a **single combined virtual address space**. The unique file's addresses are offset by the common file's relocation base:
```csharp
uniqueReloBase = commonData.ReloOffset
```

### File Structure & Sections
As defined in [Archive Format](../../../docs/ovl/archive-format.md), an OVL binary consists of seven distinct components:
1.  **Header**: Core metadata identifying the magic number (`0x4B524746` / `"FGRK"`), version (1, 4, or 5), and reference count. Version 5 files contain an extended header containing sub-version flags and additional reference metrics.
2.  **External References**: A list of dependant OVL filenames containing a 16-bit length followed by the raw ASCII name.
3.  **Loader Metadata**: Describes the resource type handlers. V5 files include a mapping table of loader indices to their respective symbol counts.
4.  **Block Definitions**: Organizes data into up to 9 types of blocks (types 0 to 8), specifying counts and sizes for each instance.
5.  **Post-Block Metadata**: Version-specific trailing diagnostic or alignment bytes.
6.  **Raw Data**: Sequential data for all defined blocks. Relative offsets operate across the merged block data.
7.  **Relocation Table**: Address mapping references for pointer patching at runtime.

### Relocation Resolution Algorithm
Pointers are patched using a two-level virtual address space resolution:
1.  **File Identification**: System checks if the address is greater than or equal to `uniqueReloBase`. If so, the resource resides in the unique OVL; otherwise, it is in the common OVL.
2.  **Block Discovery**: Once the file is selected, the system scans the accumulated block offsets to find which block type (0–8) and block instance contains the address range `[relative offset, relative offset + size)`.
3.  **Address Calculation**: The absolute pointer is calculated via:
    ```csharp
    block.data + (relocationAddress - block.RelativeOffset)
    ```

### Resource Types
OVL files can store a variety of resource types, identified by their tags, including but not limited to:

| Tag | FileType | Description |
| :--- | :--- | :--- |
| `tex` | Texture | 2D Texture |
| `flic` | Flic | Compressed 2D Image (animated frames) |
| `ftx` / `flt` | FlexibleTexture | Flexi-Texture |
| `btbl` | BitmapTable | Bitmap Table |

### Texture & Bitmap Relationships
*   **Coexistence**: Textures can live in either the common or the unique OVL; there is no enforcement restricting textures to only one file.
*   **Loader Relocations**: Each loader entry uses a `DataAddress` pointing to the combined address space. The relocation system determines which file contains the target block:
    ```csharp
    private bool ResolveAddress(uint address, out OvlType fileType, out int file, out uint block, out uint offset) {
      if (uniqueData != null && address >= uniqueReloBase)
        fileType = OvlType.Unique;
      // ...
    }
    ```
*   **String Table Sharing**: The common OVL's string table (block 0) provides resource names for loaders across both files when loaded as a pair.
*   **Bitmap Tables (`btbl`)**: Bitmap tables are distinct resource types that organize or reference bitmap data rather than act as direct containers for textures.

---

## 2. libOVL C# Porting Reference

### Paired Archive Loading (`Ovl.Load`)
The loader mirrors the original `cOVLDump::Load` logic:
1.  Loads both common and unique OVLs via `Read()`.
2.  Resolves relocations across the virtual address space.
3.  Parses the string table (block 0).
4.  Parses the symbol table (block 2, sub-block 0).
5.  Parses the loader table (block 2, sub-block 1).

### Resource Discovery & Symbol Structure
Resources are registered through symbols in Block Type 2, following exact binary sizes described in [archive-format.md](../../../docs/ovl/archive-format.md):
*   **V1 Symbols (12 bytes)**: Contains `namePtr` (u32), `dataPtr` (u32), and `isPointer` (u32) flag.
*   **V4/V5 Symbols (16 bytes)**: Contains `namePtr` (u32), `dataPtr` (u32), `isPointer` (u16), an unknown field (u16), and a resource hash/size (u32).

To avoid the overhead of C# struct marshalling during high-frequency loads, `OVL.cs` parses these structures directly from the raw byte buffer using byte offsets and `BitConverter`:
```csharp
var namePtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset);
var dataPtr = BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 4);
var size = symbolSize == 16 ? BitConverter.ToUInt32(symbolBlock.Data!, symOffset + 12) : 0u;
```

### Safe 32-bit Struct Equivalents & 64-bit Layout Safety
In C++, OVL resource pointers are stored as 32-bit virtual addresses. On 64-bit systems, raw pointers in C# expand to 64-bit structures, causing data alignment corruption. To ensure safe execution and precise layout mapping on 64-bit hosts, the following struct wrappers and marshaled structures are defined with explicit sequential or explicit layouts and fixed widths:

*   `RelocationPointer` ([RelocationPointer.cs](../../../OpenCobra/OVL/RelocationPointer.cs)): A 4-byte sequential wrapper structure representing a 32-bit virtual address offset.
    ```csharp
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct RelocationPointer {
      public uint Value;
    }
    ```
*   `Tex` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explictly aligned 60-byte structure mapping Frontier's `icontexture.h` layout. Uses `RelocationPointer` fields to secure 32-bit address mappings for `TextureData`, `Flic`, and `ExtraData`.
*   `TexExtra` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explicit 8-byte structure containing back-references (`Tex`) and pointer tables (`Flic`) as `RelocationPointer` structures.
*   `ExtraDataInfoV5` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): A 14-byte sequential structure mapping the version 5 trailing header blocks.
*   `BitmapTable` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): An 8-byte sequential structure parsing bitmap metadata.
*   `Flic` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): A 12-byte sequential structure storing animation/frame pointer locations.
*   `FlicHeader` & `FlicMipHeader` ([Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)): Explicitly sized 16-byte sequential structures ensuring proper mapping of image dimensions, pitch, block size, and formatting enumerations.

### File Type Tags (29 Supported Types)
The `FileType` enumeration spans all loader tags defined by the `Manager` classes:

| Tag | Type | Name |
| :--- | :--- | :--- |
| `txt` | Text | Text |
| `int` | Integer | Integer Number |
| `tex` | Texture | 2D Texture |
| `flic` | Flic | Compressed 2D Image |
| `ftx` | FlexibleTexture | Flexi-Texture (alias `flt`) |
| `gsi` | GuiSkinItem | GUI Skin Item |
| `sid` | SceneryItem | Scenery Item |
| `btbl` | BitmapTable | Bitmap Table |
| `anr` | AnimatedRide | Animated Ride |
| `ban` | BoneAnim | Bone Animation |
| `bsh` | BoneShape | Bone Shape |
| `ced` | CarriedItemExtra | Carried Item Extra |
| `chg` | ChangingRoom | Changing Room |
| `cid` | CarriedItem | Carried Item |
| `mam` | ManifoldMesh | Manifold Mesh |
| `ptd` | PathType | Path |
| `qtd` | QueueType | Queue |
| `ric` | RideCar | Ride Car |
| `rit` | RideTrain | Ride Train |
| `sat` | SpecialAttraction| Special Attraction |
| `shs` | StaticShape | Static Shape |
| `snd` | Sound | Sound |
| `spl` | Spline | Spline |
| `sta` | Stall | Stall |
| `svd` | SceneryItemVisual| Scenery Item Visual |
| `ter` | TerrainType | Terrain |
| `tks` | TrackSection | Track Section |
| `trr` | TrackedRide | Tracked Ride |
| `wai` | WildAnimalItem | Wild Animal Item |

---

## 3. OVL Resource Scanner Tool

**Location**: `.agents/tools/OvlScanner/`

A generic reusable console tool for discovering and enumerating **any** OVL resource type across OVL archives. Useful for production OVL discovery, fixture validation, pre-implementation analysis, and resource type surveys.

### Usage

From repo root, specify resource types via command-line arguments (file type tags):

```bash
# Scan for Spline and TrackSection
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- spl tks

# Scan for Textures
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- tex

# Scan multiple types
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- shs sid svd

# Scan all texture-related types
dotnet run --project .agents/tools/OvlScanner/OvlScanner.csproj -- tex flic ftx btbl
```

**Run without arguments** to see all 29 supported resource types.

### Output

Results are written to `.agents/summaries/ovl-{types}-scan.csv` with columns:
- `file`: relative path to OVL archive
- `type`: OVL file type tag (e.g., `spl`, `tks`, `tex`)
- `count`: number of resources of this type in the file
- `samples`: comma-separated resource names (first 3)

Filename reflects the types scanned (e.g., `ovl-spl-tks-scan.csv` for Spline + TrackSection).

### Scan Locations

**Fixtures** (always scanned):
- `OpenCobra/Tests/Fixtures/OVL/**/*.ovl`

**Production OVLs** (scanned if `RCT3_PATH` environment variable is set):
- `{RCT3_PATH}/Rides/**/*.ovl`
- `{RCT3_PATH}/tracks/**/*.ovl`

Generated CSVs write archive paths with the literal token `${RCT3_PATH}` for anything under the
RCT3 install (repo-relative otherwise), so they stay portable across machines.

### Extra dump modes

- `-- --strings` → `.agents/summaries/ovl-dump-strings.csv`: every symbol name plus every decoded
  `txt` value, one row per symbol.
- `-- --refs` → `.agents/summaries/ovl-dump-refs.csv`: every `Name:Tag` target in each archive's
  SymbolRefStruct table, **including cross-archive references** (e.g. the `tks`/`sid`/`spl` segment
  symbols a coaster's `trr` names, which live in a different `Track*.ovl`). Backed by
  `Ovl.SymbolReferences`.

### Sibling tools

- **`.agents/tools/TrackDataVerifier/`** reads an `spl`/`tks` scan CSV and checks the decoder can
  extract usable rail geometry *through* each named `tks`. For each section it resolves the
  `SplineRefs`, requires left and right rails, and sanity-checks every rail spline. It fails loudly
  where symbol-name checks alone would pass.
- **`.agents/tools/TrackedRideCorrelator/`** decodes every `tracks/` OVL in parallel (PLINQ) and
  matches each `trr`'s referenced `tks` symbols against the archive that *defines* them. It writes
  `ovl-trr-scan.csv` (ride to primary segment archive) and `track-rides.csv` (segment archive to
  rides, with an inferred `addon` column of Vanilla, Soaked, Wild, or blank when no `trr` references
  it).

---

## 4. Track Data: Segment Libraries vs. Constructed Rides

RCT3 track data has two distinct layers. Conflating them produces a disconnected graph of lone
nodes, which is wrong.

### `Track*.ovl` & `TrackBased*.ovl` are segment *palettes*, not rides

Each of these (~79 archives) is the fixed set of reusable track-segment shapes (`Straight`,
`Medcurve`, `Halfloop`, `Stationmiddle`, and so on) that one or more tracked-ride *types* can be
built from. Per segment it holds:

- a `tks` (`TrackSection`) with slope, bank, direction, flags, and **six** `SplineRefs`
  (left, right, join-left, join-right, extra-left, extra-right), all in local segment space with
  no ordering, connectivity, or world placement;
- the `spl` (`Spline`) rails those refs point at;
- a `sid` (`SceneryItem`) plus its `shs` (`StaticShape`) LODs for the visible rendered mesh.

There is no chaining, no start or end, no transform. `Track*.ovl` numbering is internal and
undocumented (no wiki maps `Track11.ovl` to a coaster type).

### `trr` (`TrackedRide`) is the ride-type definition

A ride's own OVL (`tracks/coasters/Corkscrew/`, `tracks/TrackedRides/LogFlume/`, and so on)
contains a single `trr` plus preview and car splines. The link from a `trr` to its segment archive
is **not a string in the ride OVL**. It is a cross-archive SymbolRefStruct table naming the exact
segment `tks`/`sid`/`spl` symbols, which resolve into some numbered `Track*.ovl`. Use
`Ovl.SymbolReferences` (or `TrackedRideCorrelator`) to walk it. `symbolReferenceTargets` alone
drops these, because it only binds same-archive names.

### Constructed rides

Players build actual rides by chaining segments: laying track in-game, loading an RCT3 `.trk`
design, or importing an RCT1 `.TD4` / RCT2 `.TD6` design (dropped into `Documents/RCT3/Coasters/`,
surfaced by the "Import Track Designs From Previous RollerCoaster Tycoon Games" button). Only these
produce a chained, placed graph.

### Where each lives in code

| Concept | OpenCobra.OVL (decode) | OpenRCT3 (model) |
| :-- | :-- | :-- |
| One segment's geometry & metadata | `TrackData.ExtractSplines` / `ExtractTrackSections` yield `Spline` / `TrackSection` DTOs | `TrackSegment` (`OpenRCT3/Rides/TrackLibrary.cs`) |
| A ride type's whole segment palette | (none) | `TrackSegments` (name-keyed, immutable) |
| Every ride type's palettes | (none) | `TrackLibrary : IReadOnlyDictionary<TrackedRide, TrackSegments>` |
| Read one `Track*.ovl` into a palette | (none) | `TrackLibrary.Read(TrackedRide, Ovl)`, which **never returns a `TrackGraph`** |
| A *constructed* ride's chained pieces | (none) | `TrackGraph` / `TrackGraphNode` / `TrackPiece` (`OpenRCT3/Rides/TrackSpline/`) |
| `.trk` / `.TD4` / `.TD6` design import | not started | not started (will build a `TrackGraph` by naming segments from a loaded `TrackLibrary`) |

`OpenRCT3/Simulation/` has no ride representation yet, so this distinction lives in
`OpenRCT3/Rides/`. When rides enter the ECS world it must be preserved there too.

### Known decoder gap

`TrackData` only handles the 140-byte vanilla `TrackSection_V`. On Soaked/Wild-era coaster archives
(`Track1`, `Track10`, `Track11`, and so on) the six `SplineRefs` read as zero at the `_V` offsets,
so most of their sections come back `IsValid == false`. Decoding `TrackSection_S` / `_W` is
deferred (see `TODO.md`).

---

## 5. Renderer Control Flow

The `Renderer.Render` method handles rendering for the `Scene` bound to the global `Game.Instance`.

### Windows paint loop
1.  **Entry Point**: `Program.windows.cs` -> Runs `MainForm`.
2.  **UI Layout**: `MainForm.Designer.cs` -> Instantiates `GLSurface` (OpenGL-enabled control).
3.  **Initialization**: `GLSurface.cs` -> Instantiates and initializes `Renderer` in `OnHandleCreated`.
4.  **Paint/Draw Execution**:
    *   `OnPaint` triggers `OnRenderFrame`.
    *   `OnRenderFrame` executes `_renderer.Render(Game.Instance.Scene)` and swaps buffers.
    *   `OnResize` triggers a repaint by calling `Invalidate()`.

```text
Program.Main()
└── MainForm (WinForms)
    └── GLSurface.OnPaint()
        └── GLSurface.OnRenderFrame()
            └── Renderer.Render(Scene)
```

### macOS paint loop
1.  **Paint/Draw Execution**: `OpenGLLayer.cs` handles Core Animation / AppKit draws.
2.  `DrawInContext` updates camera: `Game.Instance.Scene.UpdateCamera(...)`.
3.  `DrawInContext` calls `_renderer.Render(Game.Instance.Scene)`.

```text
GameViewController
└── OpenGLLayer.DrawInContext()
    └── Renderer.Render(Scene)
```

---

## References

*   [archive-format.md](../../../docs/ovl/archive-format.md): Full binary layout details.
*   [OVL.cs](../../../OpenCobra/OVL/OVL.cs): Core parser, relocations, loading, and table construction.
*   [RelocationPointer.cs](../../../OpenCobra/OVL/RelocationPointer.cs): Fixed-size relocation address wrapper.
*   [FileTypes.cs](../../../OpenCobra/OVL/Files/FileTypes.cs): Expanded `FileType` enum and mapping methods.
*   [ListResources.cs](../../../OpenCobra/OVL%20Tests/ListResources.cs): Contains OVL resource examination tests (e.g., `ExamineWaterOvlBinaries()`).
*   [ReadArchive.cs](../../../OpenCobra/OVL%20Tests/ReadArchive.cs): OVL loader test suite.
*   [GLSurface.cs](../../../OpenRCT3/Platforms/Windows/GLSurface.cs): Windows GLSurface paint/resize pipeline.
*   [OpenGLLayer.cs](../../../OpenRCT3/Platforms/macOS/OpenGLLayer.cs): macOS OpenGLLayer implementation.
*   [OvlScanner Tool](.agents/tools/OvlScanner/): Generic console tool for OVL resource discovery across fixtures and production archives, with `--strings` & `--refs` dump modes.
*   [TrackData.cs](../../../OpenCobra/OVL/Files/TrackData.cs): `spl` & `tks` decoder (`ExtractSplines`, `ExtractTrackSections`), vanilla `TrackSection_V` only.
*   [TrackLibrary.cs](../../../OpenRCT3/Rides/TrackLibrary.cs): `TrackLibrary` / `TrackSegments` / `TrackSegment`, a ride type's segment palette (not a `TrackGraph`).
*   [SplineTypes.cs](../../../OpenRCT3/Rides/TrackSpline/SplineTypes.cs): `TrackGraph` / `TrackPiece`, a *constructed* ride's chained pieces.
*   [TrackedRideCorrelator](.agents/tools/TrackedRideCorrelator/) & [TrackDataVerifier](.agents/tools/TrackDataVerifier/): `trr`-to-segment-archive correlation, and decoder rail-geometry verification.

---
state: design
dependencies:
  - features/track-spline-rendering
---

# Track Piece OVL Decoding

## Context

The track-spline data model provides runtime representation of rail geometry (local-space curves, corkscrews, straights) in `TrackPiece` instances. Real RCT3 tracks are defined in OVL archives using two related file types:

- **Spline** (`spl`): Vector polyline data — individual rail geometry curves with control points and metadata flags
- **TrackSection** (`tks`): Track segment metadata — associates ride trains and cars with their corresponding splines

This work is **parallel to** [track-spline rendering](../track-spline-rendering.md). Rendering integration works independently with procedural test pieces first; once both rendering and OVL decoding are complete, real imported content can be validated in-engine.

### Separation of Concerns

- **OpenCobra.OVL**: Decodes Spline/TrackSection binary entries, validates format and internal consistency, returns DTOs
- **OpenRCT3 (game)**: Converts OVL DTOs to `TrackPiece`/`TrackGraph` instances; handles chaining and world-space positioning

The decoder stays domain-agnostic per the GDK principle — interpretation is the game's responsibility.

## Goals

1. **Decode Spline (`spl`) entries with validation** — extract control points, metadata flags, and validate format correctness and invariants
2. **Decode TrackSection (`tks`) entries with validation** — extract train/car associations, basic track metadata, and validate spline references
3. **Ship `tks-viewer` Dumper plugin** — Extism plugin that visualizes Spline and TrackSection data together (they're co-located in OVL archives and inter-referential). Plugin uses the reusable "ovl" host-function surface (`resolve_pointer`, `get_relocation_source`, `find_symbol`, `read_resource`) established by `shs-viewer` to walk pointers and fetch related archive data on demand
4. **Provide validated OVL DTOs for game integration** — bridge OVL raw data to game code that will build `TrackPiece`/`TrackGraph` instances
5. **Establish test fixtures and validation patterns** — use real OVL content (Yoshi's Adventure Track, etc.) to validate decoder against real data

## Design

### OVL Data Types (OpenCobra.OVL)

Create the following DTO types in `OpenCobra/OVL/Files/`:

**`OvlSpline`** — represents a single Spline (`spl`) entry (from `rct3-importer/include/spline.h`)
- `uint NodeCount` — number of nodes in the spline
- `Vector3[] Nodes` — polyline node positions (local space)
- `Vector3[] ControlPoint1` — per-node control points relative to node position (towards previous node)
- `Vector3[] ControlPoint2` — per-node control points relative to node position (towards next node)
- `bool Cyclic` — true for closed splines, false for open
- `float TotalLength` — sum of segment lengths
- `float InvTotalLength` — reciprocal for fast normalization
- `float[] SegmentLengths` — distance between each node (array length = NodeCount-1 for open, NodeCount for cyclic)
- `byte[] SegmentData` — 14 bytes per segment encoding travel behavior
- `float MaxY` — maximum Y coordinate (height)

**`OvlTrackSection`** — represents a single TrackSection (`tks`) entry with versioning (from `rct3-importer/include/tracksection.h`)
- Base fields (V):
  - `string InternalName` — identifier
  - `uint EntrySlope`, `ExitSlope` — 0=flat, 1-2=medium, 3-4=steep, 5=vertical
  - `uint EntryBank`, `ExitBank` — 0=flat, 1-2=left, 3=inverted-left, 4=inverted, 5-6=right, 7=bank-right
  - `uint EntryFlags`, `ExitFlags` — bitflags for segment entry/exit behavior
  - `uint EntryDirection`, `ExitDirection` — 0=straight, 1=left, 2=right
  - `uint SpecialCurves` — curve type classification
  - `uint[] SplineRefs` — **6 pointers** (left, right, join-left, join-right, extra-left, extra-right)
  - `float TowerRideBase`, `WaterSplash1`, `WaterSplash2`, `ReverserVal`, `ElevatorTopVal` — ride-type metadata
  - Animation union (version-dependent): `-V` has 9 int32s; `-S` (Soaked) adds count + ptr; `-W` (Wild) extends further
  - `uint SpeedCount` & `float[]` speed structs (varies by ride type)
- Soaked extension (Sext):
  - `uint Version` — structure version (2 for Soaked, 3 for Wild)
  - `uint LoopSplineRef` — additional loop spline pointer
  - `uint[] PathRefs` — array of path spline pointers
  - `uint[] RideStationLimits` — array for station count constraints
  - `uint[] SpeedSplines` — array of speed-modifier spline references
  - Many group/constraint fields (`groups_is_at_entry`, `groups_must_have_at_exit`, etc.)
- Wild extension (Wext):
  - `uint SplitterHalf` — which half of track for splitting coaster
  - `uint RotatorType` — tower coaster rotator type
  - Additional ride-specific metadata
- **Track sections are versioned**: plain `TrackSection_V` for vanilla, `TrackSection_S` for Soaked+, `TrackSection_W` for Wild+. Decoder must detect version and parse accordingly.

### Decoder Implementation (OpenCobra.OVL.OVL)

Extend the `OVL` class to add:

```csharp
public IReadOnlyDictionary<uint, OvlSpline> LoadSplines() { /* ... */ }
public IReadOnlyDictionary<uint, OvlTrackSection> LoadTrackSections() { /* ... */ }
```

**Parsing strategy:**
1. Query loader table for `spl` and `tks` resource types
2. For each resource, read binary data and deserialize into DTO
3. Collect into dictionaries keyed by resource ID

**Validation strategy:**
1. **Format validation**: Verify binary layout matches OVL spec (sizes, field alignment)
2. **Referential integrity**: Ensure TrackSection spline references exist in loaded archive
3. **Metadata consistency**: Check for malformed flags, invalid type values, out-of-range heights
4. **Report validation errors**:
   - `System.IO.InvalidDataException` for malformed binary (truncated data, invalid field offsets, format violations)
   - `InvalidOperationException` for referential integrity violations (missing spline references)

### Test Fixtures

Use existing real OVL content in `OpenCobra/Tests/Fixtures/OVL/`:
- **Yoshi's Adventure Track** (`CTR_YoshiAdventureTrack.common/unique.ovl`) — expected to contain Spline/TrackSection entries
- Additional fixtures can be downloaded as needed from the community if edge cases are discovered

**Test strategy:**
1. Load fixture OVLs using the decoder
2. Assert that Spline/TrackSection collections are non-empty
3. Validate referential integrity (all TrackSection.SplineId values exist)
4. Spot-check parsed control points for reasonable bounds (finite, within game coordinate space)

### Game Integration (OpenRCT3)

Create `OpenRCT3/Rides/OVL/TrackImporter.cs`:
```csharp
public class TrackImporter {
  public TrackGraph ImportFromOvl(OVL ovlArchive, uint? trackSectionFilterId = null)
  {
    var splines = ovlArchive.LoadSplines();
    var sections = ovlArchive.LoadTrackSections();
    
    // Validate: all referenced splines exist (already done by decoder)
    foreach (var section in sections.Values)
      if (!splines.ContainsKey(section.SplineId))
        throw new InvalidOperationException($"TrackSection {section.Id} references missing Spline {section.SplineId}");
    
    // Build TrackPiece instances from splines, chain via TrackChaining
    // (implementation deferred pending both decoding and rendering completion)
  }
}
```

This keeps the game code focused on model construction; OVL decoding is purely OpenCobra responsibility.

### Dumper Plugin: tks-viewer (Goal 3)

Create `plugins/tks-viewer/` Extism plugin following the structure in `plugins/README.md`:

**Plugin contract:**
- `name`: "Track Sections"
- `version`: "1.0"
- `file_types`: ["tks"]
- `render(bytes: Uint8Array): void` — visualize TrackSection data and fetch related Spline data via host functions

**Note on Spline ownership:** Not all Spline entries are track-related (some are used for peep pathfinding on flat rides, etc.). The `tks-viewer` registers only for `"tks"` resources and fetches related Splines on-demand via `Ovl.resolve_pointer()`. The existing `spl-viewer` remains as the general Spline viewer for non-track Spline resources.

**Implementation approach** (reference `shs-viewer` for pointer-heavy resources):

1. Parse the input resource as TrackSection DTO (matching decoder binary format, handling version variants V/S/W)
2. Extract TrackSection metadata (slopes, banks, directions, flags, multiple spline references)
3. Use the reusable "ovl" host-function surface (`plugins/lib/ovl.ts`'s `Ovl` class) to:
   - Resolve **all 6 main spline references** (left, right, join-left, join-right, extra-left, extra-right) via `Ovl.resolve_pointer()` and `Ovl.read_resource()`
   - Resolve optional loop-spline and path-splines (if present in extended versions)
   - Fetch and deserialize related Spline DTOs (matching decoder's binary format)
4. Visualize:
   - **TrackSection metadata**: Show slopes, banks, directions, special curves, flags, ride-type specific data (speeds, water effects, etc.) in summary table
   - **Spline data**: Display control points for primary splines (left/right) in two 2D projections (top-down XY view + elevation view along longer axis with Z as height)
   - **Relationships**: Use visual indicators (labels, color coding, layering) showing which splines are left vs. right, join vs. extra, and any optional path/loop splines
5. Interactive elements: Toggle visibility per spline type, show/hide extended versions (Soaked/Wild fields), hover to show detailed metadata including animation and group constraints

**Why reuse the ovl host surface:** Spline/TrackSection pairs are deeply inter-referential and pointer-heavy. Duplicating pointer resolution logic in the plugin would replicate the `.NET` decoder's struct-layout/quirk knowledge. The `Ovl` class centralizes that knowledge; the plugin only walks pointers, as established in `shs-viewer`.

## Gaps and Risks

1. **TrackSection versioning complexity** — TrackSection (`tks`) has three versions (Vanilla/Soaked/Wild) with different struct layouts (`TrackSection_V`, `TrackSection_S`, `TrackSection_W`). The decoder must:
   - Detect which version(s) are present in a given OVL (likely from Version field in `Sext`)
   - Deserialize accordingly (can't use a single DTO for all variants)
   - Handle optional fields and unions (e.g., animation structs are unions based on ride type)
   - This is significantly more complex than the simplified "TrackSection with basic metadata" initially sketched

2. **Multiple spline references per segment** — TrackSection references **6 spline pointers** (left, right, join-left, join-right, extra-left, extra-right) plus optional loop-spline, path-splines, and speed-splines. The initial plan assumed a single `SplineId`. Referential validation must check all pointers, and the game integration layer must understand what each reference means.

3. **Pointer-to-array fields** — Both Spline and TrackSection contain dynamically-allocated arrays (nodes[], lengths[], datas[] for Spline; paths[], speed_splines[], groups[] for TrackSection). OVL relocation must resolve these correctly; off-by-one or alignment errors will corrupt data. Test fixtures are critical.

## Status

**Phase 0 complete** ✅. Production OVL discovery executed: 8,660 OVL files scanned, 390 files with Spline/TrackSection entries identified. Found 18,914 Spline and 7,610 TrackSection entries primarily in Track*.ovl and TrackBased*.ovl files. Generic OVL scanner tool created in `.agents/tools/OvlScanner/` for future resource discovery tasks.

**Ready for Phase 1** (Decoder Implementation). Design ready per Approach 2 (decoder with validation + game-level integration). Actual TrackSection complexity (versioning, multiple spline refs, dynamic arrays) is significantly higher than initial sketch — early Phase 1 design work will refine these uncertainties using actual production data.

## Deferred

- **TrackImporter implementation** — depends on both this decoder and track-spline-rendering completion
- **Full track-geometry validation suite** — comes after both decoding and rendering are complete
- **Real content import pipeline** — separate from decoder itself; will use TrackImporter when ready

## Testing

### Unit Tests (OpenCobra.Tests/OVL/)

Create `SplinesTests.cs`:
- Parsing valid Spline binary data yields correct DTO fields
- Invalid/truncated binary data raises `InvalidOperationException` with context
- Control point bounds validation rejects non-finite values

Create `TrackSectionsTests.cs`:
- Parsing valid TrackSection binary data yields correct DTO fields
- TrackSection.SplineId validation passes for existing references
- Missing spline references are detected during validation

### Integration Tests (OpenCobra.Tests/Integration/)

Add test cases to existing `ExtractResources` class:
- Load Yoshi's Adventure Track fixture OVL
- Extract Spline and TrackSection collections via decoder
- Validate non-empty and referentially consistent
- Spot-check control point values (finite, in reasonable coordinate bounds)

This validates decoder against real RCT3 content before game integration.

### Dumper Plugin Tests (plugins/tks-viewer/)

Create AssemblyScript unit tests validating:
- Plugin correctly parses Spline/TrackSection binary input
- Pointer resolution via `Ovl` host functions returns valid data
- Visualization rendering produces valid HTML/SVG output
- Edge cases: empty collections, circular references (if possible), missing referenced splines

## Implementation

### Phase 0: Production OVL Discovery (Pre-Implementation)

0. **Scan fixtures and production OVLs**:
   - [x] Build throwaway scanner console app (or use OpenCobra test framework) to enumerate Spline/TrackSection entries across all fixture OVLs and RCT3_PATH (if available)
   - [x] Document results in `.agents/summaries/ovl-spl-tks-scan.csv` with columns: `file`, `spl_count`, `tks_count`, `spl_samples`, `tks_samples`
   - [x] Record which archives contain track-related data, distribution (common vs. unique), and a few sample symbol names
   - [x] Note any apparent schema variants or edge cases discovered during scanning (e.g., malformed references, unusual metadata values)
   - [x] These findings will inform decoder design and validation strategy; report them before Phase 1 begins

### Phase 1: OVL Decoder (OpenCobra.OVL) ✅

1. **Define DTO types** (`OpenCobra/OVL/Files/TrackData.cs`):
   - [x] `OvlSpline` — Id, NodeCount, Nodes[], ControlPoints[], Cyclic, Metadata
   - [x] `OvlTrackSection` — Id, InternalName, Slopes, Banks, Directions, SplineRefs[], IsValid

2. **Implement Spline decoder** (`OpenCobra/OVL/Files/TrackData.cs`):
   - [x] Add `ExtractSplines()` method — queries loader for `spl` entries, deserializes to `OvlSpline` DTOs
   - [x] Format validation: verify binary layout matches OVL spec using `SplineBinary` struct
   - [x] Parse control points (vectors), segment data, metadata fields

3. **Implement TrackSection decoder** (`OpenCobra/OVL/Files/TrackData.cs`):
   - [x] Add `ExtractTrackSections()` method — queries loader for `tks` entries, deserializes to `OvlTrackSection` DTOs
   - [x] Format validation: verify binary layout matches OVL spec using `TrackSectionBinary` struct
   - [x] Referential validation: verify all 6 spline references exist (set `IsValid` flag)

4. **Error handling**:
   - [x] Throw `System.IO.InvalidDataException` on malformed binary (truncated data, invalid field offsets, format violations)
   - [x] Throw `InvalidOperationException` on referential integrity violation (missing spline reference)
   - [x] Include resource name and issue description in error messages (following TerrainTypes/Textures pattern)

### Phase 2: Dumper Plugin (plugins/tks-viewer/)

5. **Plugin scaffold**:
   - [x] Create `plugins/tks-viewer/` directory with AssemblyScript source
   - [x] Add `plugins/tks-viewer/index.ts` implementing `render(bytes: Uint8Array): void` export
   - [x] Declare manifest: `name: "Track Section Viewer"`, `version: "0.1.0"`, `file_types: ["tks"]`

6. **Parser layer**:
   - [x] Deserialize input bytes as `OvlTrackSection` (match .NET DTO binary layout)
   - [x] Extract TrackSection metadata (ID, spline reference, train ID, track type, height)

7. **Pointer resolution** (via `Ovl` host-function surface):
   - [x] Import `Ovl` class from `plugins/lib/ovl.ts`
   - [x] Resolve the TrackSection's `SplineId` reference using `Ovl.resolve_pointer()` and `Ovl.read_resource()`
   - [x] Deserialize fetched Spline bytes as `OvlSpline` (matching decoder binary format)

8. **Visualization**:
   - [x] Render Spline control points in two 2D projections, displayed side-by-side:
     - **Top-down view**: XY plane projection (Z ignored), shows lateral track geometry
     - **Elevation view**: Project onto the longer of X or Y axis, with Z as vertical height; shows track profile
   - [x] Display TrackSection metadata in a summary table (type, height, train ID, spline ID)
   - [x] Use visual indicators (e.g., labels, color coding) to show which TrackSection owns which Spline
   - [x] Implement toggle controls to show/hide specific TrackSections or their related Splines

### Phase 3: Testing (OpenCobra.Tests + plugins/tks-viewer/tests/)

9. **SplinesTests.cs**:
   - [ ] Valid spline binary parses to correct DTO fields
   - [ ] Invalid/truncated data raises `System.IO.InvalidDataException`
   - [ ] Control point validation (non-finite values rejected)

10. **TrackSectionsTests.cs**:
    - [ ] Valid track section binary parses correctly
    - [ ] Missing spline reference detected; `IsValid` flag set appropriately
    - [ ] Train ID and track type parsed correctly

11. **ExtractResources.cs** (integration):
    - [ ] Add test cases to existing `ExtractResources` class in `OpenCobra/Tests/Integration/`
    - [ ] Test: Load Yoshi's Adventure Track OVL fixture → `LoadSplines()` returns non-empty collection
    - [ ] Test: Load Yoshi's Adventure Track OVL fixture → `LoadTrackSections()` returns non-empty collection
    - [ ] Test: All TrackSections have valid `IsValid` flags (referential integrity)
    - [ ] Test: Sample control points from fixture are finite and in reasonable bounds

12. **Plugin tests** (`plugins/tks-viewer/tests/`):
    - [ ] Unit tests for binary parsing (synthetic data)
    - [ ] Integration tests for pointer resolution (using fixture OVLs)
    - [ ] Snapshot tests for rendered output HTML/SVG

### Phase 4: Game Integration (OpenRCT3)

13. **TrackImporter scaffold** (`OpenRCT3/Rides/OVL/TrackImporter.cs`):
    - [ ] Stub implementation (parse OVL, validate, defer model construction)
    - [ ] Add XML doc comments describing responsibilities
    - [ ] (Full implementation deferred; depends on both this plan and track-spline-rendering completion)

### Phase 5: Documentation & Verification

14. **Post-Implementation**:
    - [ ] Update `plugins/README.md` — mark `tks-viewer` as ✅ Completed (move from 📋 Planned)
    - [ ] Update `.agents/summaries/ovl-spl-tks-scan.csv` with any additional findings from testing (if schema variants discovered in Phase 0 scanning, document how decoder handles them)
    - [ ] Verify sample symbol names from Phase 0 scan work correctly with the decoder

## References

**OVL Format & Architecture:**
- [`docs/ovl/archive-format.md`](../../../docs/ovl/archive-format.md) — detailed OVL binary layout, symbol tables, relocation resolution
- [`rct3-importer/include/spline.h`](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/spline.h) — Spline struct definition with nodes, control points, segment data
- [`rct3-importer/include/tracksection.h`](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/tracksection.h) — TrackSection struct definitions (V/Sext/Wext versions) with all 6 spline refs, animations, groups, constraints
- [`rct3-importer/src/libOVLDump/`](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLDump) — reference C++ implementation of OVL parsing and relocation

**Local Reference Implementation:**
- Local checkout: `D:\Users\enigm\GitHub\rct3-importer` — use for struct layouts, line-by-line reference when implementing decoders

**Existing OpenCobra Patterns:**
- [`OpenCobra/OVL/Files/TerrainTypes.cs`](../../../OpenCobra/OVL/Files/TerrainTypes.cs) — similar OVL resource decoder with validation; follow its error-handling and DTO patterns
- [`plugins/shs-viewer/`](../../../plugins/shs-viewer/) — pointer-heavy plugin using `Ovl` host functions; reference for tks-viewer implementation

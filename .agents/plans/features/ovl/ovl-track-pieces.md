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

**`OvlSpline`** — represents a single Spline (`spl`) entry
- `uint Id` — unique identifier within the OVL
- `Vector3[] ControlPoints` — polyline vertices (local space)
- `uint Flags` — type/behavior flags from OVL spec
- `float[] Metadata` — additional per-spline data (tension, segment info, etc.) if present in spec

**`OvlTrackSection`** — represents a single TrackSection (`tks`) entry
- `uint Id` — unique identifier within the OVL
- `uint SplineId` — reference to the associated Spline
- `uint TrainId` — which train/car this section belongs to
- `uint TrackType` — track piece type (normal, loop, corkscrew, etc.)
- `float Height` — height or banking metadata
- `bool IsValid` — computed during validation; true if SplineId references an existing spline

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
2. **Referential integrity**: Ensure TrackSection's `SplineId` references an existing Spline ID
3. **Metadata consistency**: Check for malformed flags, invalid type values, out-of-range heights
4. **Report validation errors** via exceptions (`OvlFormatException`) with clear messages and field offsets

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

1. Parse the input resource as TrackSection DTO (matching decoder binary format)
2. Extract TrackSection metadata (train ID, track type, height, spline reference)
3. Use the reusable "ovl" host-function surface (`plugins/lib/ovl.ts`'s `Ovl` class) to:
   - Resolve the spline reference (`SplineId`) via `Ovl.resolve_pointer()` and `Ovl.read_resource()`
   - Fetch and deserialize the related Spline DTO (matching decoder's binary format)
4. Visualize:
   - **TrackSection metadata**: Show type, height, train ID, spline ID in a summary table
   - **Spline data**: Display control points in two 2D projections (top-down XY view + elevation view along longer axis with Z as height)
   - **Relationships**: Use visual indicators (labels, color coding) showing TrackSection-to-Spline ownership
5. Interactive elements: Toggle visibility per TrackSection/Spline, hover to show detailed metadata

**Why reuse the ovl host surface:** Spline/TrackSection pairs are deeply inter-referential and pointer-heavy. Duplicating pointer resolution logic in the plugin would replicate the `.NET` decoder's struct-layout/quirk knowledge. The `Ovl` class centralizes that knowledge; the plugin only walks pointers, as established in `shs-viewer`.

## Status

Design complete. Ready for implementation (Approach 2: decoder with validation + game-level integration).

## Deferred

- **TrackImporter implementation** — depends on both this decoder and track-spline-rendering completion
- **Full track-geometry validation suite** — comes after both decoding and rendering are complete
- **Real content import pipeline** — separate from decoder itself; will use TrackImporter when ready
- **OVL format spec unknowns** — will be discovered during implementation; use rct3-importer reference (local checkout at `D:\Users\enigm\GitHub\rct3-importer`) and reverse-engineering patterns from real fixtures if gaps emerge

## Testing

### Unit Tests (OpenCobra.Tests/OVL/)

Create `SplinesTests.cs`:
- Parsing valid Spline binary data yields correct DTO fields
- Invalid/truncated binary data raises `OvlFormatException` with context
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

## Implementation Steps

### Phase 0: Production OVL Discovery (Pre-Implementation)

0. **Scan fixtures and production OVLs**:
   - [ ] Build throwaway scanner console app (or use OpenCobra test framework) to enumerate Spline/TrackSection entries across all fixture OVLs and RCT3_PATH (if available)
   - [ ] Document results in `.agents/summaries/ovl-spl-tks-scan.csv` with columns: `file`, `spl_count`, `tks_count`, `spl_samples`, `tks_samples`
   - [ ] Record which archives contain track-related data, distribution (common vs. unique), and a few sample symbol names
   - [ ] Note any apparent schema variants or edge cases discovered during scanning (e.g., malformed references, unusual metadata values)
   - [ ] These findings will inform decoder design and validation strategy; report them before Phase 1 begins

### Phase 1: OVL Decoder (OpenCobra.OVL)

1. **Define DTO types** (`OpenCobra/OVL/Files/TrackData.cs`):
   - [ ] `OvlSpline` — Id, ControlPoints[], Flags, Metadata
   - [ ] `OvlTrackSection` — Id, SplineId, TrainId, TrackType, Height, IsValid

2. **Implement Spline decoder** (`OpenCobra/OVL/OVL.cs`):
   - [ ] Add `LoadSplines()` method — queries loader for `spl` entries, deserializes to `OvlSpline` DTOs
   - [ ] Format validation: verify binary layout matches OVL spec
   - [ ] Parse control points (vectors), flags, metadata fields

3. **Implement TrackSection decoder** (`OpenCobra/OVL/OVL.cs`):
   - [ ] Add `LoadTrackSections()` method — queries loader for `tks` entries, deserializes to `OvlTrackSection` DTOs
   - [ ] Format validation: verify binary layout
   - [ ] Referential validation: for each TrackSection, check `SplineId` references an existing Spline ID (set `IsValid` flag)

4. **Error handling**:
   - [ ] Throw `OvlFormatException` on malformed binary (truncated data, invalid field offsets)
   - [ ] Throw on referential integrity violation (missing spline reference)
   - [ ] Include field offset and type information in error messages

### Phase 2: Dumper Plugin (plugins/tks-viewer/)

5. **Plugin scaffold**:
   - [ ] Create `plugins/tks-viewer/` directory with AssemblyScript source
   - [ ] Add `plugin.ts` implementing `render(bytes: Uint8Array): void` export
   - [ ] Declare manifest: `name: "Track Sections"`, `version: "1.0"`, `file_types: ["tks"]`

6. **Parser layer**:
   - [ ] Deserialize input bytes as `OvlTrackSection` (match .NET DTO binary layout)
   - [ ] Extract TrackSection metadata (ID, spline reference, train ID, track type, height)

7. **Pointer resolution** (via `Ovl` host-function surface):
   - [ ] Import `Ovl` class from `plugins/lib/ovl.ts`
   - [ ] Resolve the TrackSection's `SplineId` reference using `Ovl.resolve_pointer()` and `Ovl.read_resource()`
   - [ ] Deserialize fetched Spline bytes as `OvlSpline` (matching decoder binary format)

8. **Visualization**:
   - [ ] Render Spline control points in two 2D projections, displayed side-by-side:
     - **Top-down view**: XY plane projection (Z ignored), shows lateral track geometry
     - **Elevation view**: Project onto the longer of X or Y axis, with Z as vertical height; shows track profile
   - [ ] Display TrackSection metadata in a summary table (type, height, train ID, spline ID)
   - [ ] Use visual indicators (e.g., labels, color coding) to show which TrackSection owns which Spline
   - [ ] Implement toggle controls to show/hide specific TrackSections or their related Splines

### Phase 3: Testing (OpenCobra.Tests + plugins/tks-viewer/tests/)

9. **SplinesTests.cs**:
   - [ ] Valid spline binary parses to correct DTO fields
   - [ ] Invalid/truncated data raises `OvlFormatException`
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

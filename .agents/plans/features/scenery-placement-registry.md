# Plan: Scenery Placement Registry

**Roadmap**: Phase 1, "Render scenery items"

**See also**:
- [`terrain-heightmap.md`](terrain-heightmap.md) — precedent for keying directly off raw OVL data without an
  abstraction layer, and for keeping per-item data owned directly by `Park` rather than a wrapper service.
- `rct3-importer`'s `RCT3 Importer/include/scenery.h` (sibling repo, not part of this checkout) — source of the
  `sizeflag` placement-type enum (`SIZE_FULLTILE` etc.) referenced below.

## Context

Scenery placement (where an object sits, at what rotation, static vs. animated) is a data-model concern
separable from whether the underlying mesh/texture for that object has been resolved yet. This let placement
and object-registry design proceed without depending on the texture decoding work, which was open when this
plan was written and has since been fixed
([`completed-work/ovl-texture-decoding.md`](../../summaries/completed-work/ovl-texture-decoding.md),
[`completed-work/ovl-materials-integration.md`](../../summaries/completed-work/ovl-materials-integration.md)).
The underlying reason this plan's design doesn't depend on that pipeline still holds, though: `svd`
mesh decoding (which is what actually resolves a scenery item's geometry) remains unimplemented — see
[`ovl-scenery-items.md`](ovl/ovl-scenery-items.md) (`svd`'s `meshtype == 0` case is unblocked by
[`shs` decoding](../../summaries/completed-work/ovl-static-shapes.md), but `svd` itself isn't done)
— so the object registry designed here is still ahead of, not blocked by, mesh resolution.

**Scenery is not scalable.** RCT3's scenery catalog has a Small/Medium/Large enum, but that's a catalog
filter/grouping property, not a render-time scale factor — placed scenery is static-mesh geometry (aside from
morph-target animal meshes, which are an animation concern, not a scale concern). The placement record
therefore carries **no scale field at all**, uniform or otherwise. Size variation only comes from picking a
different object definition (e.g. a distinct "large tree" OVL entry has its own mesh), never a runtime
multiplier applied to a shared mesh.

## Goals

- **Object registry**: a lookup from raw OVL symbol name (`svd` key) to a placeable "definition" — footprint,
  category, static/animated flag — decoupled from the still-in-progress `svd`/mesh/texture resolution pipeline.
  - Keys on the raw OVL symbol name directly, no separate internal ID layer. This matches the terrain plan's
    precedent of referencing `TerrainType`/`ter` entries by their OVL-native identity rather than inventing an
    abstraction on top. OVL symbol names are content-addressed by the original game data, not by our tooling,
    so re-extraction is assumed stable.
- **Placement record**: object reference (OVL symbol/`svd` key), anchor grid position, quarter-turn rotation
  (0/90/180/270, per RCT series convention) — no scale field (see Context).
  - **Placement type**: RCT3 stores a `sizeflag` field per `sid`/`svd` entry (`rct3-importer`'s `scenery.h`,
    `SIZE_*` defines) that is the actual driver of footprint shape and height-sampling — not a separate
    single-sample-vs-conforming bool as originally guessed. The registry entry carries this as a
    `Placement` enum with the game's 9 values: `FullTile`, `PathEdgeInner`, `PathEdgeOuter`, `Wall`,
    `Quarter`, `Half`, `PathCenter`, `Corner`, `PathEdgeJoin`. This single enum determines both:
    - **Footprint/snap position** — which sub-tile slot the object occupies (tile center, a quarter/half
      sub-cell, a corner, mounted on a path edge or wall, etc.), superseding the earlier
      anchor-plus-width/height-only model for anything that isn't `FullTile`. `FullTile` (and multi-tile
      objects built from it) still use the width/height + quarter-turn-rotation model described above;
      the other 8 values place at a fixed sub-tile offset derived from the enum value itself, not a
      registry-supplied width/height.
    - **Height sampling** — `FullTile`/`Quarter`/`Half`/`Corner`/`PathCenter` are effectively single-sample
      placements (one terrain-height query at the snapped position). `PathEdgeInner`, `PathEdgeOuter`,
      `PathEdgeJoin`, and `Wall` are all edge-mounted: the object sits on one of the tile's four
      [`Edge`](../../../OpenRCT3/Simulation/Edge.cs)s, selected by the same quarter-turn rotation value the
      placement record already carries, and height comes from that edge's two bounding corners — a per-corner
      terrain query, same shape as the fence/path-edge conforming case, not a distinct "vertical face" concept.
      (An earlier draft of this plan described `Wall` as sampling "off the adjacent vertical face" — that
      confused wall placement with the unrelated cliff-rendering vertical face from
      [`terrain-heightmap.md`](terrain-heightmap.md); corrected here.)
  - **Height**: scenery snaps to terrain height at placement time per the sampling rule implied by
    `Placement` above — the placement record does not store its own Z; it's derived from a terrain height
    query each time it's needed, mirroring how [`terrain-heightmap.md`](terrain-heightmap.md) treats height as
    queryable rather than cached elsewhere. This is one-directional: scenery reads terrain height, but placed
    scenery does **not** cap or constrain later terrain raise/lower edits the way ride footprints do in the
    terrain plan — that constraint is ride-specific, not a general placed-object rule.
- **Static vs. animated distinction**: an `AnimationKind` enum on the registry entry (e.g.
  `None` / `Looping` / `Triggered` / `MorphTarget`), not derived from the underlying OVL `FileType` — the
  registry should be usable before animated rendering exists. Adding the enum now (rather than a plain bool)
  avoids a breaking registry-entry schema change once the GDK animation system needs to distinguish these
  cases. Scope for this plan is the enum value only; no keyframe/timeline or discrete-state (e.g. open/closed
  gate) model — that belongs to the eventual GDK animation system.
- **Ownership**: placed-scenery data lives directly on `Park`, as a collection field, following the terrain
  plan's precedent of keeping per-item/per-tile data owned directly by its parent type rather than wrapping it
  in a separate service (no `SceneryLayer` type).
- Roadmap item 3 (Phase 3) mentions "freeform, off-grid scenery placement" later — the anchor+footprint model
  above is grid-native; extending to off-grid placement later likely means adding a continuous-position
  variant alongside (not replacing) the grid anchor, similar in spirit to how the terrain plan keeps water
  level as an independent value rather than folding it into the corner grid. Not designed further here.

## Resolved

- **`sizeflag` lives on `sid`, not `svd`.** Confirmed directly against `rct3-importer`'s
  `RCT3 Importer/include/scenery.h`: `sizeflag` appears at the same relative struct offset in both `Scenery`
  (line 74) and `SIDData` (line 158) — the latter matching this plan's `sid` terminology — and is absent from
  `SceneryItemData` (line 256, the `svd`-side struct: visual/render params like LOD, sway, brightness, no
  placement-shape field). Caveat: that entire struct block (`scenery.h:14-451`) is wrapped in `#if 0` in the
  importer itself — it's the original reverse-engineer's struct-layout notes, not code the importer actually
  compiles/runs — so treat this as strong evidence, not proof, and reconfirm against real decoded bytes once
  our own `sid` decoder exists. Deferred to decode time (not blocking now): the exact byte offset/decoder wiring
  in `OpenCobra`.
- **`Wall` category scope is unresolved by design, deferred to decode time.** `scenery.h`'s `TYPE_*` list
  includes `TYPE_WALL_MISC` (5) and fence-adjacent categories (`TYPE_KEEP_CLEAR_FENCE` 26, `TYPE_ANIMAL_FENCE`
  45) beyond the `TYPE_FENCE`/`TYPE_WALL_STRAIGHT`/`TYPE_WALL_ROOF`/`TYPE_WALL_CORNER` set this plan originally
  named. `type` and `sizeflag` are independent fields in the struct, though, so which `TYPE_*` values actually
  carry `SIZE_WALL` is data-dependent and can't be settled from the header alone — whoever implements the `sid`
  decoder confirms this against real entries and generalizes `Wall` height-sampling only if a non-wall-shaped
  category turns up using it; hardcode "adjacent vertical face" until then.
- **`FullTile` multi-tile height is a footprint-flatness gate, not an anchor-vs-average choice.** Multi-tile
  `FullTile` objects (and rides, later) query every corner within the object's footprint bounds (OVL data
  supplies scenery/ride bounds — a later concern). If those corners aren't all equal height, placement is
  blocked outright rather than averaged or snapped — mirroring `research/terrain-tools.md`'s "Flatten for Scenery and
  Rides" tool and the same flatness-check shape [`Park.cs:112`](../../../OpenRCT3/Simulation/Park.cs)'s
  `IsAtGradePathPlaceable` already uses for single-tile paths. Once corners agree, "anchor vs. average" is moot
  — both read the same value. This applies to non-terrain-conforming footprint objects generally (flat rides,
  large scenery); the edge-following `Placement` values (`PathEdgeInner`/`PathEdgeOuter`/`PathEdgeJoin`, plus
  fences/flowers) are unaffected and keep their per-corner conforming height query as already scoped above.
- **`AnimationKind` stays the 4-value enum (`None`/`Looping`/`Triggered`/`MorphTarget`), explicitly provisional.**
  `ovl-scenery-items.md`'s notes on `sid` sound references, animation scripts, and flat-ride-specific `ANR`
  animation params hint at categories that may not map cleanly onto `Looping`/`Triggered` once decoded — likely
  candidates for a future 5th kind, not designed further here. Not blocking: the registry only needs the enum
  value today, no keyframe/timeline model.

## Status

Implemented: [`Placement.cs`](../../../OpenRCT3/Simulation/Placement.cs) (9-value enum),
[`AnimationKind.cs`](../../../OpenRCT3/Simulation/AnimationKind.cs) (4-value, provisional),
[`SceneryDefinition.cs`](../../../OpenRCT3/Simulation/SceneryDefinition.cs) (registry entry: `Placement`,
`AnimationKind`, `FootprintWidth`/`FootprintHeight`), [`SceneryRegistry.cs`](../../../OpenRCT3/Simulation/SceneryRegistry.cs)
(OVL-symbol-keyed lookup, independent of `Park`), and [`SceneryPlacement.cs`](../../../OpenRCT3/Simulation/SceneryPlacement.cs)
(placed instance: `ObjectKey`/`TileX`/`TileY`/`Rotation`, no stored Z). `Rotation` reuses
[`Edge`](../../../OpenRCT3/Simulation/Edge.cs) as the quarter-turn value, doubling as the mounted-edge
selector for `Wall`/`PathEdgeInner`/`PathEdgeOuter`/`PathEdgeJoin`.

`Park.SceneryPlacements` holds placed instances (no `SceneryLayer` wrapper, per Goals).
`Park.TryPlaceScenery` enforces the footprint-flatness gate for multi-tile `FullTile` objects (rejects
if any covered tile's corners disagree, rotation-aware footprint swap via a private
`GetRotatedFootprint`/`IsFootprintLevel` pair) and otherwise just validates the object key is registered
and the anchor tile is on-grid. `Park.GetSceneryHeight` implements the height-sampling rule: edge-mounted
`Placement` values return the two corners bounding the `Rotation` edge (via
`Terrain.GetEdgeCornerHeights`); everything else returns the anchor tile's average corner height twice
(matching a single terrain-height query, and equal to any one corner once the flatness gate above holds
for multi-tile objects). Covered by 9 tests in
[`OpenRCT3.Tests/Simulation/SceneryPlacementTests.cs`](../../../OpenRCT3.Tests/Simulation/SceneryPlacementTests.cs),
all passing; full `Simulation` test suite (53 tests) still green.

Not yet done, left for follow-up work: wiring the registry to the real `sid`/`svd` OVL decoder (no
decoder exists yet — see Resolved above), sub-tile snap-position math for `Quarter`/`Half`/`Corner`/
`PathCenter` (currently only height sampling is implemented for these, not exact render position),
and any rendering/mesh-gen consumption of `GetSceneryHeight`.

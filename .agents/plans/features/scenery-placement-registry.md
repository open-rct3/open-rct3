# Plan: Scenery Placement Registry

**Roadmap**: Phase 1, "Render scenery items"

**See also**:
- [`terrain-heightmap.md`](terrain-heightmap.md) — precedent for keying directly off raw OVL data without an
  abstraction layer, and for keeping per-item data owned directly by `Park` rather than a wrapper service.
- `rct3-importer`'s `RCT3 Importer/include/scenery.h` (sibling repo, not part of this checkout) — source of the
  `sizeflag` placement-type enum (`SIZE_FULLTILE` etc.) referenced below.

## Context

Scenery placement (where an object sits, at what rotation, static vs. animated) is a data-model concern
separable from whether the underlying mesh/texture for that object has been resolved yet. This lets placement
and object-registry design proceed without depending on the still-open texture decoding work
([ovl-texture-decoding.md](../../bugs/ovl-texture-decoding.md)).

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
      placements (one terrain-height query at the snapped position). `PathEdgeInner`, `PathEdgeOuter`, and
      `PathEdgeJoin` are the edge-following cases (fences, path-adjacent scenery) that need per-corner terrain
      queries along the tile edge to conform to slope, matching what fences/long-row bushes/flowers visibly
      do in-game. `Wall` samples off the adjacent vertical face, not corner height directly.
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

## Open Questions

- Confirm `sizeflag` lives on the `sid` (not `svd`) struct in the actual OVL data our decoder reads — the
  `rct3-importer` header shows it on the raw C++ struct, but our own OVL-decoding research
  ([ovl-static-shapes.md](../OVL%20Decoding/ovl-static-shapes.md) and related) should be checked for where it
  actually surfaces in the symbols we parse.
  Do any placements outside `TYPE_FENCE`/`TYPE_WALL_STRAIGHT`/`TYPE_WALL_ROOF`/`TYPE_WALL_CORNER`
  in `sceneryold.h`'s `TYPE_*` list use `Wall`, or is it effectively 1:1 with those categories? Affects whether
  `Wall` height-sampling needs to be generalized or can hardcode "adjacent vertical face."
- For `FullTile` multi-tile objects, does Z come from the anchor corner specifically, or a footprint-wide
  average? Minor, but affects multi-tile placement on a sloped tile.
- `AnimationKind.MorphTarget` covers animal meshes per the Context note above — confirm no other OVL scenery
  category needs a distinct kind not yet enumerated (`None` / `Looping` / `Triggered` / `MorphTarget`).

## Status

Not started — stub only. Core data-model shape (no scale field, OVL-symbol-keyed registry, `Placement` enum
driving both footprint/snap-position and height-sampling rule, `AnimationKind` enum, ownership on `Park`) is
settled from discussion, now grounded in `rct3-importer`'s actual `sizeflag`/`SIZE_*` field rather than a
guessed height-conforming bool. Remaining open questions above are confirmatory (locating `sizeflag` in our own
OVL decode path, `Wall`/`FullTile` edge-case sampling detail) and don't block starting implementation.

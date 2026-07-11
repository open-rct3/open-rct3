# Plan: Water Tool Placement (Perimeter Tracing)

**Roadmap**: Phase 1, item 4 — "Render fluctuating terrain" (water tool follow-on)

**See also**:
- [`terrain-tools.md`](../../../research/terrain-tools.md) — RCT3 terrain tool reference; this plan implements the **Water** entry under
  "Other Tools" ("add/remove water at the clicked height").
- [`../terrain-heightmap.md`](../terrain-heightmap.md) — "Water is per-pool" Goals section and its
  Deferred entry "Water pool perimeter tracing (flood-fill placement)", which this plan picks up.

## Context

`WaterPool`/`Park.WaterPools`/`Park.WaterTiles`/`Park.TryPlaceWaterPool` are implemented (see
`terrain-heightmap.md` Status): a pool is a flat height plus an exact tile set plus an `IsOcean` flag,
and terrain edits invalidate whole pools via `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/
`SetTerrainCornerHeight`. That data model is **not** what's missing. What's missing is everything
upstream of it: `TryPlaceWaterPool` takes an already-decided tile set and height — nothing yet decides
those from a single clicked tile the way the in-game Water tool does. This plan designs that
decision step: seed tile in, tile set + height + `isOcean` out, ready to hand to `TryPlaceWaterPool`
unchanged.

## Goals

- **Flood-fill from a seed tile.** Given a clicked tile and a candidate water height (see below), walk
  the connected component of tiles whose terrain is at or below that height, 4-connected (matching how
  `Park.Paths`/`IsAtGradePathPlaceable` reason about individual tiles rather than diagonal adjacency).
  A tile qualifies if all four of its corners are at or below the candidate height — mirrors
  `IsAtGradePathPlaceable`'s min/max-corner style query rather than inventing a new terrain predicate.
- **Candidate height comes from the clicked tile, snapped.** Take the lowest corner of the seed tile,
  then snap up to the nearest 1 m (`Park.AtGradePathMaxRise`, 100 `HeightStep` units) increment — the
  same snap granularity grid-based tools already use for height edits (see `terrain-tools.md`, "Corner Snapping"
  family). This reuses `AtGradePathMaxRise` as the shared snap constant rather than adding a second
  one; if a future tool needs a materially different snap step, split the constant then, not now.
- **Reject tiles already claimed.** A tile already present in `Park.WaterTiles` is not walkable — the
  flood fill stops at existing pools' edges rather than merging into them. Matches
  `TryPlaceWaterPool`'s existing "reject already-covered tiles" check; the flood fill's job is just to
  not walk into that rejection in the first place, so `TryPlaceWaterPool`'s check becomes a defensive
  assert rather than the primary gate.
- **Ocean detection is a byproduct of the same walk.** If the flood fill's frontier ever reaches a tile
  on the OOB-inclusive grid boundary (`Terrain.Width - 1`/`Terrain.Height - 1`/`0`), stop tracing that
  direction and mark the resulting pool `isOcean = true` — per `terrain-heightmap.md`'s "Ocean special
  case". The traced tile set passed to `TryPlaceWaterPool` is still just the bounded set of qualifying
  tiles found before hitting the edge (per that section: rendering, not tile-set membership, is what
  differs for oceans), so no separate "unbounded" code path is needed here.
- **Bounded walk, not unbounded search.** Cap the flood fill at the map's own dimensions (it can't visit
  more tiles than exist) — no separate artificial radius limit is needed beyond that, since a full-map
  low region legitimately becomes one large pool (or an ocean, per above) and that's correct behavior,
  not a runaway.
- **Removal is already solved.** `Park.InvalidateWaterPoolAt` deletes a pool by any covered tile; the
  in-game "remove water" click just needs to call that directly with the clicked tile — no new design
  needed for removal, only for placement.

## Open Questions

- **Per-corner vs per-tile qualifying test.** "All four corners at or below the candidate height" is
  the natural read of `IsAtGradePathPlaceable`'s style, but the in-game tool may key off a coarser
  per-tile average or min-corner test instead. Not re-verified against the reference implementation
  (same caveat as the original per-pool amendment in `terrain-heightmap.md`) — flag for confirmation
  before or during implementation rather than blocking this plan on it.
- **Re-raising the candidate height mid-trace.** If the flood fill discovers a lower tile than the
  seed after the walk has started (concave low regions), does the real tool restart the trace at the
  new lower height, or keep the original seed-derived height and simply exclude tiles below it? Classic
  RCT-style tools generally use the seed height as fixed and only *include* qualifying neighbors, so
  this plan assumes **fixed seed height, no re-lowering mid-walk** — noted as an assumption, not a
  confirmed behavior.
- **Interaction with `IsOcean` and partial map edges.** Does an ocean pool's tracked `Tiles` set stay
  frozen at placement time even if the map's playable area is later resized, or is this a non-issue
  because the map is fixed-size for the lifetime of a park? Assumed fixed-size (no map-resize feature
  exists), deferred if that assumption changes.

## Deferred (out of scope for this plan)

- **UI/input wiring** (tool selection, drag-to-preview, cursor feedback) — belongs to a rendering/input
  plan once one exists; this plan only designs the tile-set/height/`isOcean` decision function that such
  a UI would call into `TryPlaceWaterPool` with.
- **Water rendering** (mesh generation for a pool's surface, ocean-to-horizon extension) — rendering
  concern, same as `terrain-heightmap.md`'s deferred "Blended rendering" entry.

## Status

Not started. Data model this plan builds on (`WaterPool`, `Park.WaterPools`/`WaterTiles`,
`TryPlaceWaterPool`, `InvalidateWaterPoolAt`) is implemented and tested (`WaterPoolTests.cs`); this plan
covers the still-missing flood-fill placement function only.

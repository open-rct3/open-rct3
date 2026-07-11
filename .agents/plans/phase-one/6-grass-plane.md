# Phase 6: Render Grass Terrain

**Superseded by [`grass-texture-from-terrain-ovl.md`](../grass-texture-from-terrain-ovl.md).**

This doc previously tracked Phase 6 as deferred, blocked on the OVL texture-decoding bug. That bug is fixed
(see [`completed-work/ovl-texture-decoding.md`](../../summaries/completed-work/ovl-texture-decoding.md) and
[`completed-work/ovl-materials-integration.md`](../../summaries/completed-work/ovl-materials-integration.md)),
which unblocked a much smaller, concrete follow-up plan than what this doc originally scoped (extending
`TerrainMeshBuilder` to sample per-corner `SurfaceIndex`/blending, a paint tool, etc.). The active plan pulls
a single grass texture (`Terrain_00`) directly out of the now-working `TextureCollection` decode and wires it
into the terrain mesh as `AlbedoTexture` — three small edits, no decoder work needed. Per-corner surface
painting/blending remains future work, tracked in `grass-texture-from-terrain-ovl.md`'s references back to
[`features/terrain-heightmap.md`](../features/terrain-heightmap.md)'s Deferred section, not here.

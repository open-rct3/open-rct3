# Reverse-engineering fixtures

User-authored saves, not third-party downloads — each pair/set differs from `baseline.dat` by
exactly one known, isolated in-game edit, made specifically to reverse-engineer the RCT3 non-OVL
DAT format's terrain and path field layouts by diffing. See
[`rct3-terrain-data-layout.md`](../../../../../.agents/research/rct3-terrain-data-layout.md)
and [`rct3-path-tile-layout.md`](../../../../../.agents/research/rct3-path-tile-layout.md) for what
each capture was used to confirm.

- **`baseline.dat`** — unmodified starting park, common ancestor for every other file here.
- **`01-*.dat`** — terrain edits (corner height, surface paint, water). `01-one-far-corner-up.dat`
  raises the corner of a map-edge tile furthest to the left of the park entrance;
  `01-near-left-corner-up.dat`, `01-near-right-corner-up.dat`, and `01-far-left-corner-up.dat`
  each raise one of the same interior tile's four corners, camera-relative (used together to map
  each of a tile's four stored corner-height fields to a physical corner).
- **`02-*.dat`** — path edits (at-grade tile placement, raised tile placement).

# RollerCoaster Tycoon 3 — Terrain Tool Reference

Based on the official RCT3 manual, cross-referenced with the in-game panel layout.

**Used by**: [`terrain-heightmap.md`](../terrain-heightmap.md) — the Terrain Heightmap Data Model plan, which
derives its 1m corner-height step and grid-vs-freeform tool distinction from this reference.

## The Four Panel Tabs

### A — Grid-Based Tools (bounded to grid squares)
Part of the "Tweak Terrain" tool group. Set the brush size, position the pointer, then drag up or down. At brush size 1, you can drag a single tile's edge or corner directly.

- **Freeform Corner-Pulling** — raises or lowers a highlighted grid area
- **Snap Corners to Neighboring Corners** — smooths terrain previously shaped with freeform corner-pulling
- **Corner Snapping to Scenery** — snaps a highlighted area to the height of nearby placed scenery
- **Corner Snapping to Coasters** — snaps to the height of nearby ride entrances
- **Spray Mode** — raises/lowers progressively the longer you hold the mouse button, with adjustable speed

*This is the "snapped to some unknown increment" tool — grid/corner-based rather than freeform, and it's what auto-aligns to ride/scenery heights.*

### B — Smoothing Tools
Six icons:

1. **Remove Cliffs** — smooths cube-shaped edges into flowing terrain
2. **Create Cliffs** — terraces hilly terrain into cube-shaped edges
3. **Flatten Terrain** — flattens to the height where dragging started
4. **Flatten for Scenery and Rides** — flattens an area suitable for paths/scenery/rides, to the height where dragging began
5. **Flatten Dynamically** — flattens to the height of the grid square currently under the pointer
6. **Averager** — moderates terrain shape between the extremes in the area

### C — Raise Terrain (freeform hills)
- **Hill** — rounded peaks and slopes
- **Mountain** — sharp peaks
- **Mesa** — flat top
- **Ridge** — raised formation with a rounded top

### D — Lower Terrain (freeform depressions)
- **Trough** — narrow, shallow depression
- **Crater** — depression with sloping edges
- **Canyon** — deep, wide, steep-edged

## Other Tools (outside this sub-panel)
- **Pulling** — the basic raise/lower drag tool, closest to classic RCT1/2-style height stepping
- **Terrain Texture** — repaint ground cover (grass, desert, etc.)
- **Water** — add/remove water at the clicked height

## Granularity Notes
- The **diameter spinner** controls brush/area of effect in grid squares.
- Ramps ascend/descend **1 meter** (~3 ft); stairs ascend/descend **2 meters** (~6 ft); terrain contour lines are spaced **2 meters** apart.
- **"Flatten for Scenery and Rides"** and similar tools snap elevation to increments matching ramp/stair step height, so paths can always connect — this is the "unknown increment" for rides/scenery.
- Freeform sculpting tools (Hill/Mountain/Mesa/Ridge/Trough/Crater/Canyon) are **continuous drag-based**, not grid-locked — height varies smoothly with mouse movement rather than snapping between fixed steps.

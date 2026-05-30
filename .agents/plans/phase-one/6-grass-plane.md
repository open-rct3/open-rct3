# Phase 6: Render Grass Plane

Implement the terrain loading and rendering of a flat grass-covered park map.

## Overview

This phase transitions from a simple quad to a full-sized park grid. It involves loading terrain textures from the RCT3 installation and setting up the initial `World` state.

## Tasks

### 1. OVL Texture Loading Improvements
- Add unit tests for `OpenCobra.OVL.Files.Textures` to ensure robust decoding of `TEX`, `FLIC`, and `BTBL` files.
- Add integration tests for `OpenCobra.GDK` to verify ingestion of OVL textures into GDK-compatible formats.

### 2. Terrain Module (`OpenRCT3/Terrain`)
- Create `Terrain` class to represent the park's land.
- Implement `Terrain.Load()` to:
    - Locate `terrain/RCT3/Terrain_RCT3.common.ovl`.
    - Extract the default grass texture (index 0).
    - Initialize a grid representing the park.

### 3. World Integration
- Update `OpenRCT3.Simulation.World` to contain a `Park` and `Terrain`.
- Ensure the `World` is initialized during `Game` startup.

### 4. Rendering the Grid
- Update `Scene` to generate a mesh for the entire park grid (e.g., 128x128 tiles).
- Apply the grass texture to all tiles.

## Technical Details

- **Coordinate System**: Z-up.
    - **X**: West to East (East is +X)
    - **Y**: South to North (North is +Y)
    - **Z**: Down to Up (Up is +Z)
- **World Origin**: (0, 0, 0) at the middle of the South edge of the park map.
- **Tile Size**: 4x4 meters.
- **Map Structure**:
    - **Buildable Area**: 128x128 tiles.
    - **Out-of-Bounds (OOB)**: 5-tile wide strip around the edge.
    - **Total Map Size**: 138x138 tiles ($552\text{m} \times 552\text{m}$).
- **Orientation**: Park entrance is on the South edge (Y-minimum of buildable area).

## Dependencies
- `OpenCobra/OVL/Files/Textures.cs`
- `OpenCobra/GDK/Assets/TextureLoader.cs`
- `OpenRCT3/Simulation/World.cs`

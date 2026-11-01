# Terrain Textures From Loaded Parks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render each loaded park's terrain with the RCT3 terrain texture selected by its saved surface type instead of applying one grass texture to the entire mesh.

**Architecture:** Decode the saved park's per-tile surface type into terrain surface data, resolve the corresponding ground and cliff texture entries from the installed `Terrain_RCT3.common.ovl`, and build renderable terrain surfaces grouped by texture. Keep a flat-color fallback for missing installation data and unknown surface indices. `World` owns the resulting models as one scene group so replacing a loaded park removes all old terrain models and disposes their textures.

**Tech Stack:** C#/.NET, NUnit, OpenCobra DAT and OVL decoders, ImageSharp texture decoding, OpenCobra GDK meshes/materials, OpenGL scene models.

**Spec:** User request: use the proper terrain textures from loaded parks, using `Data/Parks`, `OpenRCT3.Tests/Fixtures/Parks`, `OpenRCT3/Scenario/ParkChooser.cs`, and `OpenRCT3/Simulation/Park.cs` as the loaded-park and fixture context.

## Global Constraints

- Preserve the existing OOB-inclusive terrain dimensions and row-major DAT tile layout.
- Surface type indices from saved parks remain byte values and must not be silently normalized to a guessed default.
- A missing RCT3 installation, missing OVL texture, or unknown surface index must still produce a visible flat-color terrain model.
- Do not remove the existing saved-park fixtures or comments.
- Add unit coverage for every changed code path and run `make test` after source changes.
- Do not run `git add` or `git commit`; the user commits changes.

---

## File Map

- Modify `OpenRCT3/Simulation/Terrain.cs`: retain decoded surface indices, load the terrain texture catalog, expose grouped render models, and own texture lifetime.
- Modify `OpenRCT3/Simulation/TerrainMeshBuilder.cs`: build top and cliff geometry for a selected surface group without losing the current terrain coordinates and normals.
- Create `OpenRCT3/Simulation/TerrainSurface.cs`: define the resolved surface identity and the model/material pair used by the world scene.
- Modify `OpenRCT3/Simulation/World.cs`: add and remove every terrain surface model as one loaded-world unit and dispose the terrain texture catalog through `Terrain`.
- Modify `OpenRCT3.Tests/Simulation/TerrainLoadTests.cs`: verify fixture surface indices and texture-group selection without requiring a full RCT3 installation.
- Modify `OpenRCT3.Tests/Simulation/ParkLoadTests.cs`: verify that loaded fixture parks expose terrain models for their saved surface types and that replacing a park does not retain the prior model set.
- Create `OpenRCT3.Tests/Simulation/TerrainTextureTests.cs`: test OVL entry-to-texture resolution, fallback behavior, and ownership boundaries with deterministic test data.

---

### Task 1: Model terrain surface identity and resolve installed textures

**Files:**
- Create: `OpenRCT3/Simulation/TerrainSurface.cs`
- Modify: `OpenRCT3/Simulation/Terrain.cs:45-137`
- Create: `OpenRCT3.Tests/Simulation/TerrainTextureTests.cs`

**Interfaces:**
- Consumes: decoded `TerrainCorner.SurfaceIndex`, `TerrainTypeEntry.Number`, `TerrainTypeEntry.Name`, `TerrainTypeEntry.Type`, and `OpenCobra.OVL.Files.TextureCollection` entries from `Terrain_RCT3.common.ovl`.
- Produces: an internal resolver with the signature `TerrainSurfaceCatalog Resolve(IReadOnlyList<TerrainTypeEntry> terrainTypes, TextureCollection textures)` and a terrain-owned `IReadOnlyDictionary<byte, TerrainSurface>` keyed by saved surface index.

- [ ] **Step 1: Write failing resolver tests**

Add NUnit tests covering these concrete cases:

```csharp
[Test]
[Description("Maps a saved terrain number to the matching ground texture by terrain entry name")]
public void Resolve_GroundEntry_MapsNumberToTexture() { /* assert index, type, and texture name */ }

[Test]
[Description("Leaves an unknown saved terrain number on the flat-color fallback")]
public void Resolve_UnknownEntry_UsesFallback() { /* assert no texture is returned */ }

[Test]
[Description("Does not select cliff entries as top-surface textures")]
public void Resolve_CliffEntry_IsClassifiedAsCliff() { /* assert surface role */ }
```

Use small in-memory `TerrainTypeEntry` records and decoded texture objects. The tests must not depend on `AppConfig`, a user install, or a fixture path.

- [ ] **Step 2: Run the focused tests and verify failure**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~TerrainTextureTests`.

Expected result: compilation or assertion failures because the catalog and resolver do not exist yet.

- [ ] **Step 3: Implement the catalog and ownership rules**

Create `TerrainSurface` as a readonly record containing the saved `byte Index`, terrain role, optional decoded GDK `Texture`, and a fallback flag. Create `TerrainSurfaceCatalog` as an `IDisposable` collection that owns only textures transferred into the GDK materials. Resolve entries by converting `TerrainTypeEntry.Number` to a byte only when the value is in range, selecting ground entries for top faces, and matching the OVL texture by `TerrainTypeEntry.Name` (using `TextureRef` only when it resolves to a present texture). Keep unresolved entries in the catalog as fallback surfaces rather than throwing.

In `Terrain.Load`, load the existing terrain OVL once, extract `TerrainTypes` and `Textures`, build the catalog, transfer mip-zero images into GDK textures, and call `BuildModel` after the catalog is available. Replace the single grass-only selection rule. Keep a no-installation path that creates an empty catalog and uses `GrassColor`.

- [ ] **Step 4: Run the focused tests and verify success**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~TerrainTextureTests`.

Expected result: PASS, including the unknown-entry fallback and no-installation-compatible behavior.

- [ ] **Step 5: Stop after the diff and summary**

Do not stage or commit changes.

---

### Task 2: Build geometry grouped by saved surface type

**Files:**
- Modify: `OpenRCT3/Simulation/TerrainMeshBuilder.cs:14-131`
- Modify: `OpenRCT3/Simulation/Terrain.cs:88-105`
- Modify: `OpenRCT3/Simulation/TerrainSurface.cs`
- Modify: `OpenRCT3.Tests/Simulation/TerrainTextureTests.cs`
- Modify: `OpenRCT3.Tests/Simulation/TerrainLoadTests.cs`

**Interfaces:**
- Consumes: `TerrainSurfaceCatalog` from Task 1 and the existing terrain corner grid.
- Produces: `Terrain.BuildModels()` returning an `IReadOnlyList<Model>`, with each model containing only tiles whose saved `SurfaceIndex` resolves to that model's surface texture. The existing `GroundModel` property remains as the fallback/primary model for compatibility with current callers and tests.

- [ ] **Step 1: Write failing mesh-group tests**

Add tests that load `01-surface-changed.dat` with a supplied catalog and assert that the changed tile's top-face vertices occur in the model for surface index `0x1F`, while an unchanged neighboring tile occurs in its original surface model. Add a test for an unknown surface index that asserts the tile is present in a flat-color fallback model.

Use `Mesh.Vertices` and `Mesh.Indices` counts or tile-local vertex positions rather than GPU state, so the tests remain headless.

- [ ] **Step 2: Run the focused tests and verify failure**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~TerrainLoadTests|FullyQualifiedName~TerrainTextureTests`.

Expected result: FAIL because all tiles currently share one mesh and `BuildModels()` is not implemented.

- [ ] **Step 3: Implement grouped mesh construction**

Refactor `TerrainMeshBuilder` to accept a surface predicate or surface index and keep the existing top-face UVs, cliff-face winding, normals, and OOB traversal. Build one mesh per surface group, preserving detached-edge geometry for tiles included in that group. Update `Terrain.BuildModel`/`BuildModels` to create one `Model` per resolved surface, use `Textured` when the surface has a texture, and use `Flat` with `GrassColor` for fallback groups. Ensure an empty group is not emitted.

When a park is edited and `BuildModel` is called again, replace the mesh contents and material without duplicating texture ownership.

- [ ] **Step 4: Run the focused tests and verify success**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~TerrainLoadTests|FullyQualifiedName~TerrainTextureTests`.

Expected result: PASS, including surface-painted fixture tiles and fallback tiles.

- [ ] **Step 5: Stop after the diff and summary**

Do not stage or commit changes.

---

### Task 3: Add and remove the complete terrain model set in the world

**Files:**
- Modify: `OpenRCT3/Simulation/World.cs:54-90,148-156`
- Modify: `OpenRCT3.Tests/Simulation/ParkLoadTests.cs`

**Interfaces:**
- Consumes: `Terrain.BuildModels()` and terrain-owned texture disposal from Tasks 1-2.
- Produces: world initialization that registers all terrain meshes needed by debug/picking code, adds every terrain model exactly once, removes every prior terrain model before loading another park, and leaves `Terrain.GroundModel` as the compatibility primary model.

- [ ] **Step 1: Write the failing world lifecycle test**

Add a test that loads an available fixture park, records `Terrain.BuildModels().Count`, loads a second fixture park through `World.Load`, and asserts that the new terrain model set is non-empty and belongs only to the new `Terrain` instance. Keep the existing fixture-availability guards so the test remains valid when optional park files are absent.

- [ ] **Step 2: Run the focused test and verify failure**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~ParkLoadTests`.

Expected result: FAIL or expose that `World` only tracks/removes `currentGroundModel`, leaving additional surface models unmanaged.

- [ ] **Step 3: Implement terrain model-set lifecycle**

Replace `currentGroundModel` with a collection of current terrain models. During initialization, remove all old terrain models from `scene.Models`, register the compatibility mesh plus any additional meshes under stable service keys, then add the new set. During disposal, remove/dispose the model set through the existing scene/resource ownership pattern and let `Terrain.Dispose` release the catalog textures exactly once.

Update debug mesh registration and terrain picking to continue using the primary terrain mesh, since picking uses geometry rather than material selection.

- [ ] **Step 4: Run the focused test and verify success**

Run `dotnet test OpenRCT3.Tests/OpenRCT3.Tests.csproj --filter FullyQualifiedName~ParkLoadTests`.

Expected result: PASS with no stale terrain model references after replacement.

- [ ] **Step 5: Stop after the diff and summary**

Do not stage or commit changes.

---

### Task 4: Run the repository-required verification

**Files:**
- No new files. Review all modified terrain, world, and test files.

**Interfaces:**
- Consumes: completed Tasks 1-3.
- Produces: verified saved-park terrain rendering behavior with regression coverage.

- [ ] **Step 1: Re-read the changed files**

Confirm that `Terrain.Load` still loads both default terrain and park paths, that `ParkChooser` paths remain unchanged, and that the fixture surface index is still copied into every corner of its tile.

- [ ] **Step 2: Run the required unit test command**

Run `make test` from the repository root.

Expected result: PASS. If the environment lacks the installed RCT3 OVL, tests that require it must use deterministic fallback assertions and must not fail solely because the install is unavailable.

- [ ] **Step 3: Review the final diff without mutating Git state**

Inspect the source and test diff with read-only tools. Confirm no fixture, asset, or user file was deleted or overwritten, and confirm no `git add` or `git commit` was run.

- [ ] **Step 4: Stop after the diff and summary**

Report the changed files and the exact `make test` result. Leave committing to the user.

---

## Self-Review

- **Spec coverage:** The plan maps saved park loading through `Terrain.LoadFromSave`, uses the existing park fixtures, resolves installed RCT3 terrain entries, renders per-surface textures, preserves fallback behavior, and updates `World` lifecycle. `ParkChooser.cs` is intentionally not modified because it already supplies the selected `.dat` path to the world load flow.
- **Placeholder scan:** No step depends on TBD, TODO, or unspecified validation. Each code task names files, interfaces, tests, commands, and expected outcomes.
- **Type consistency:** `TerrainSurfaceCatalog` feeds `Terrain.BuildModels()`, and `World.InitializeScene` consumes that model list. `Terrain.GroundModel` remains the primary compatibility model while the collection carries additional surface models.

# Scenario Editor

**Issue**: TBD

**Status**: In progress

## Goal

Establish a basic Scenario Editor with a developer GUI and a placeholder grass-colored terrain mesh, without loading
RCT3 texture files.

## Phase 1: Bootstrap ImGui and Grass Mesh

### 1. Add ImGui Package

Edit [`OpenCobra/GDK/GDK.csproj`](OpenCobra/GDK/GDK.csproj):

```xml
<PackageReference Include="Silk.NET.OpenGL.Extensions.ImGui" Version="2.23.0" />
```

### 2. Refactor for Stateful GPU Caching

Refactor so that `Mesh` and `Material` owns GPU-resident state and `Renderer` is purely immediate:

#### `Mesh.cs`

Add GPU handle fields (`Vao`, `Vbo`, `Ebo`) alongside CPU data.

- On first `Upload()`, allocate buffers and cache GL handles on the `Mesh` instance.
- Subsequent renders reuse cached handles; no re-upload unless data changes.
- Add `bool IsUploaded` flag and an `Upload()` method that the `Scene` or `Renderer` calls once per mesh.

#### `Renderer.cs`

Strip all GL handle management from the render loop.

- Remove shader/VAO/VBO/texture creation and disposal from `Render()`.
- `Render()` becomes a thin immediate-mode dispatcher: bind VAO, draw elements, unbind.
- The `Scene` triggers `Upload()` once per mesh, when it detects changes to the scene's mesh data.
- Retain state in `Mesh` and `Material` only — `Renderer` holds no persistent handles.

**Goal**: `Renderer.Render()` is a stateless draw call; all mutable GPU state lives in `Mesh`, `Material`, etc.

#### Create [`OpenCobra/GDK/ImGui.cs`](OpenCobra/GDK/ImGui.cs):

- Initialize `readonly` ImGui context in the `Scene` constructor
- Hook into OpenGL renderer for `Begin`/`End` frame calls
- Render a simple debug overlay per frame

### 3. Create Scenario Editor Window

Create [`OpenRCT3/Scenario/Editor.cs`](OpenRCT3/Scenario/Editor.cs):

- Minimal window with "Scenario Editor" title with:
  - A row of icon buttons:
    - Save
    - Quit (closes the scenario editor window)
  - A column of labeled buttons:
    - Setup Park
    - Choose Finances
    - Choose Objectives & Challenges

### 4. Extract Terrain Primitives

Create [`OpenCobra/GDK/Meshes/Primitives.cs`](OpenCobra/GDK/Meshes/Primitives.cs):

- Define static **normalized** mesh data for `Plane`, `Cube`, and `Sphere`
- Each primitive is a static method:
  - `Vector4 color = default` parameter for custom color
  - Returns a `Mesh` instance
- Use **counterclockwise** (CCW) winding order, consistent with the industry standard (OpenGL and Direct3D)

### 5. Render Grass-Colored Terrain Mesh

In the existing terrain rendering code:

1. Detect where the `nullbmp` texture is applied
2. Replace it temporarily with a solid grass color `Color.FromArgb(79, 129, 14)`

   **Do not delete the nullbmp logic** — leave it in place for later restoration.

**Color**: `Color.FromArgb(79, 129, 14)` (no hex literals per project rules)

### 6. Update Renderer

Update [`OpenCobra/GDK/Scene.cs`](OpenCobra/GDK/Scene.cs) to call ImGui frame methods.

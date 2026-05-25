# Scenario Editor

**Issue**: TBD

**Status**: In progress

## Goal

Establish a basic Scenario Editor with a developer GUI and a placeholder grass-colored terrain mesh, without loading
RCT3 texture files.

## Phase 1: Bootstrap ImGui and Grass Mesh

### 1. ~~Add ImGui Package~~ (Done)

Edit [`OpenCobra/GDK/GDK.csproj`](../../OpenCobra/GDK/GDK.csproj):

```xml
<PackageReference Include="Silk.NET.OpenGL.Extensions.ImGui" Version="2.23.0" />
```

### 2. ~~Refactor for Stateful GPU Caching~~ (Done)

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

#### Create [`OpenCobra/GDK/ImGui.cs`](../../OpenCobra/GDK/ImGui.cs):

- Initialize `readonly` ImGui context in the `Scene` constructor
- Hook into OpenGL renderer for `Begin`/`End` frame calls
- Render a simple debug overlay per frame

### 3. ~~Create Scenario Editor Window~~ (Done)

Create [`OpenRCT3/Scenario/Editor.cs`](../../OpenRCT3/Scenario/Editor.cs):

- Minimal window with "Scenario Editor" title with:
  - A row of icon buttons:
    - Save
    - Quit (closes the scenario editor window)
  - A column of labeled buttons:
    - Setup Park
    - Choose Finances
    - Choose Objectives & Challenges

### 4. ~~Render Grass-Colored Terrain Mesh~~ (Done)

In the existing terrain rendering code:

- [x] Detect where the `nullbmp` texture is applied
- [x] Replace it temporarily with a solid grass color `Color.FromArgb(79, 129, 14)`

   **Do not delete the nullbmp logic** — leave it in place for later restoration.

> **🐛 Bug**: Color data reaches GPU but window shows wrong color. Suspect `SwapBuffers` or scene update logic.

> **🐛 Bug**: Ground plane sizing breaks on window resize → check resize handling and GL viewport setup.

### 5. ~~Extract Terrain Primitives~~ (Done)

Create [`OpenCobra/GDK/Meshes/Primitives.cs`](../../OpenCobra/GDK/Meshes/Primitives.cs):

- Define static **normalized** mesh data for `Cube` shapes; see `Primitives.Plane`
- Each primitive is a static method:
  - `Vector4 color = default` parameter for custom color
  - Returns a `Mesh` instance
- Use **counterclockwise** (CCW) winding order, consistent with the industry standard (OpenGL and Direct3D)

> **🐛 Bug**: The ground mesh's `Model.Transform` is not being applied in the scene. Suspect either the uniform
> upload (check `Materials.Flat`/`Materials.Textured` shaders) or the shader code itself.

### 6. Add `GLState` Struct

Create [`OpenCobra/GDK/GLState.cs`](../../OpenCobra/GDK/GLState.cs):

A `readonly struct` that captures and restores the OpenGL state machine via a thread-safe concurrent stack. Avoids manual save/restore boilerplate across the render loop and ImGui integration.

**Design:**

```csharp
public readonly struct GLState {
    // Captured state fields (all the bits that change: blending, depth, scissor, VAO, textures, etc.)

    public static GLState Push() { /* save current GL state -> ConcurrentStack, return instance */ }
    public static void Pop()       { /* pop from stack -> restore GL state */ }
}
```

- `Push()` retrieves `GL` via `var gl = Scene.IoC.Resolve<IGL>().Context;`
- Pushes returned `GLState` to a static `ConcurrentStack<GLState>`
- `Pop()` pops from that stack and restores via `GL.CheckError` on every call
- Use `using var guard = GLState.Push();` so the state is restored automatically at the end of the scope (RAII pattern)

**State fields to capture** (match what `ImGuiController.SetupRenderState` / `RenderImDrawData` mutate):

| Field | How to query |
|---|---|
| Active texture | `gl.GetInteger(GLEnum.ActiveTexture)` |
| Current program | `gl.GetInteger(GLEnum.CurrentProgram)` |
| Texture 2D binding | `gl.GetInteger(GLEnum.TextureBinding2D)` |
| Array buffer binding | `gl.GetInteger(GLEnum.ArrayBufferBinding)` |
| Vertex array binding | `gl.GetInteger(GLEnum.VertexArrayBinding)` |
| Scissor box | `gl.GetInteger(GLEnum.ScissorBox, &outSpan)` |
| Blend src/dst/equation (RGB + alpha) | `gl.GetInteger(...)` |
| Enable flags (Blend, CullFace, DepthTest, StencilTest, ScissorTest) | `gl.IsEnabled(...)` |
| Polygon mode (desktop GL only) | `gl.GetInteger(GLEnum.PolygonMode, &span)` |

**Usage pattern:**

```csharp
// Surround a section that changes GL state
using var guard = GLState.Push();
// ... render ImGui, change textures, bind whatever ...
// GC/dispose restores automatically
```

### 7. Update Renderer

Update [`OpenCobra/GDK/Scene.cs`](../../OpenCobra/GDK/Scene.cs) to call ImGui frame methods.

Integrate `ImGuiController` from `Silk.NET.OpenGL.Extensions.ImGui` into the render loop. The official [Silk.NET ImGuiController.cs](https://github.com/dotnet/Silk.NET/blob/main/src/OpenGL/Extensions/Silk.NET.OpenGL.Extensions.ImGui/ImGuiController.cs) provides the reference implementation.

**Key integration points:**

| Phase | What to do | Reference method |
|---|---|---|
| **Init** | Construct `ImGuiController` with `(GL gl, IView view, IInputContext input)`. The controller creates its own ImGui context internally via `ImGuiNET.ImGui.CreateContext()` and compiles its own shaders/textures. | `ImGuiController` constructor |
| **Per-frame (scene update)** | Call `controller.Update(deltaSeconds)` before `Renderer.Render(scene)` — this calls `ImGui.NewFrame()`. | `Update(float)` |
| **Post-render** | Call `controller.Render()` after `Rendered` event fires — this calls `ImGui.Render()` then renders ImDrawData to OpenGL. | `Render()` |

**OpenGL state around ImGui draw calls** (from `SetupRenderState` + `RenderImDrawData`):

- **Enable**: Blend (with `FuncAdd`, `SrcAlpha/OneMinusSrcAlpha` per-channel), Scissor test
- **Disable**: CullFace, DepthTest, StencilTest
- **Rebind VAO**: ImGui creates a transient VAO per frame (`GenVertexArray`/`DeleteVertexArray`) — restore the app's VAO afterwards
- **Blend**: ImGui uses premultiplied alpha; your scene shaders should be compatible or you must disable blending during the ImGui pass if your scene writes to alpha

Change `Scene.Update`:

```csharp
imguiController.Update(delta.Seconds);
Camera.Update(aspectRatio);
```

Change `Game.Render`:

```csharp
// ...
gl.BindVertexArray(0);
gl.UseProgram(0);

// ImGui drawn on top
// Save/restore OpenGL state around ImGui's GL mutations
using var imguiState = GLState.Push();
imguiController.Render();
Rendered?.Invoke(this, EventArgs.Empty);
```

> **Note**: If using multiple GL contexts with ImGui, call `imguiController.MakeCurrent()` before `Update`/`Render` to switch its context.

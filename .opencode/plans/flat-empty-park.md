# Plan: Render a Flat, Empty Park

**Issue**: https://github.com/open-rct3/open-rct3/issues/4

**Cost Estimate**: [.opencode/costs/flat-empty-park.md](../costs/flat-empty-park.md)

## Overview

Implement rendering of a flat, empty park in the native C# OpenRCT3 client using the existing OpenGL foundation via Silk.NET. Work through three stages: solid color → nullbmp texture → actual grass from terrain OVLs.

---

## Phase 1: Scaffold GDK Project (Done)

- **Purpose**: Game Data (GDK = "Game Development Kit" / "Graphics Development Kit")
- **Path**: `OpenCobra/GDK/GDK.csproj`
- **Dependencies**: `OpenCobra/OVL/`
- **Target**: net8.0

**Design Pattern**: Follow the existing `IGraphicsSurface` abstraction pattern — GDK primitives are pure C# data structures without rendering backend dependencies. The `OpenRCT3/OpenGL` layer provides the translation to Silk.NET.

---

## Phase 2: GDK Backend-Agnostic Primitives (Done)

Create rendering-agnostic data structures in `OpenCobra/GDK/`:

| Primitive | Description |
|-----------|-------------|
| **Material** | Rendering properties — albedo texture, normal map, specular, emissive, transparency flags |
| **Mesh** | Geometry — vertex buffer (positions, normals, UVs, colors), index buffer, bounding box |
| **ShaderProgram** | Vertex + fragment source code, uniform/attribute definitions, parameter bindings |

### Resource Disposal Pattern

All GDK resources that map to GPU resources must implement `IDisposable`:

```csharp
public class Texture : IDisposable {
    private bool _disposed;
    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        // Release GPU memory via Silk.NET (GL.DeleteTexture, etc.)
    }
}
```

The `OpenGLRenderer` must dispose all created resources on shutdown to prevent GPU memory leaks.

---

## Phase 3: Detect RCT3 Installation

Add configuration primitives to `OpenRCT3/Platforms/` (instead of separate project):

| Primitive | Description |
|-----------|-------------|
| **Rct3InstallFinder** | Platform-specific RCT3 install path discovery |
| **AppConfig** | JSON config storage (install path, user preferences) |

### Implementation

1. **Path Discovery** (`Rct3InstallFinder.cs`):
   - Follow platform-specific paths from `src/paths.d`:
     - **macOS**: `/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets`
     - **Windows**: `C:\Program Files\RollerCoaster Tycoon 3 Platinum` + variants (Steam, GOG, Platinum)
   - Validate by checking for required files (e.g., `terrain/RCT3/Terrain_RCT3.common.ovl`)
   - **Error handling**: Throw `Rct3InstallNotFoundException` with user-friendly message; fallback to folder picker dialog if validation fails

2. **Configuration Storage** (`AppConfig.cs`):
   - Save discovered path to `config.json` in app data directory
   - Fallback: folder picker dialog if validation fails
   - Load on startup before rendering
   - **Error handling**: Log warnings if config missing; prompt user to locate RCT3

---

## Phase 4: Render Solid Color Plane (Prototype)

> **⚠️ This is a prototype.** The implementation is intentionally minimal to validate the rendering pipeline. Expect significant refactoring in future phases as more features are added. Do not invest in polished error handling, testing, or optimization here — those will be addressed as the prototype matures.

### `OpenRCT3/Platforms/`

1. **Add `IRenderer` interface** (following `IGraphicsSurface` pattern):
   ```csharp
   public interface IRenderer {
     void Initialize(IGraphicsSurface surface);
     void Render(Mesh mesh, Material material, ShaderProgram shader);
     void SetViewport(int width, int height);
   }
   ```

2. **Implement `OpenGLRenderer`** in `OpenRCT3/OpenGL/`:
   - Create vertex buffer for flat quad (2 triangles)
   - Embed simple GLSL vertex/fragment shaders in C#
   - Set perspective projection matrix + view matrix (camera looking down at park)
   - Render quad in `DrawInCGLContext`

3. **Update `GameOpenGLLayer.cs`**:
   - Initialize `OpenGLRenderer` with the GL context
   - Call render each frame

**Camera Setup**:
- Position: Elevated, looking down at ~30-45° angle
- Field of view: ~60°
- Near: 0.1, Far: 1000

**Milestone**: Colored rectangle in 3D perspective view (no textures yet)

---

## Phase 5: Render Textured Plane with nullbmp

### `OpenCobra/GDK/`

1. **Create `plugins/ftx-viewer/`** — New plugin to decode flexi-textures (ftx loader type)

2. **Texture Loader** (`GDK/Textures/TextureLoader.cs`):
   - Load `nullbmp.common.ovl` from RCT3 Assets
   - Parse flexi-textures (see `OVL.cs:252-284` for `FlexiTextureData`)
   - Extract raw pixel data + palette
   - **Error handling**: Throw `TextureLoadException` if OVL not found or invalid

3. **Texture Conversion**:
   - Convert flexi-texture to GDK `Material` with albedo texture
   - Handle palette indexing (indexed color → RGBA)
   - **Decision needed**: Evaluate palette conversion approaches:
     - **Option A**: **KGy SOFT Drawing Libraries** (NuGet: `KGySoft.Drawing`) — provides `Palette` class and `ConvertPixelFormat` method; verify it handles custom palettes (not just system defaults)
     - **Option B**: Manual lookup using `FlexiTextureData.palette` — no extra deps, full control, requires more code
     - **Option C**: **ImageSharp** — modern, but requires quantization (designed for RGBA→indexed, not reverse)
   - **Recommendation**: Prototype both KGySoft and manual lookup; choose based on code complexity and maintainability

### `OpenRCT3/OpenGL/`

4. **Update `OpenGLRenderer`**:
   - Bind texture to GL via Silk.NET
   - Enable texturing in shader
   - Render quad with texture coordinates

**Assets**:
- Path: `/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets/nullbmp.common.ovl`
- Size: 6,081 bytes

**Milestone**: Plane textured with nullbmp (placeholder/blank texture)

---

## Phase 5b: Prototype Palette Conversion (Decision Prototype)

Before full nullbmp/grass rendering, prototype palette handling:

1. **Load `nullbmp.common.ovl`** and extract `FlexiTextureData` (palette + pixel data)
2. **Test KGySoft approach**: Use `KGySoft.Drawing.Palette` with custom palette bytes; verify `ConvertPixelFormat` produces correct RGBA
3. **Test manual approach**: Write simple index→palette lookup; compare output to KGySoft
4. **Decision**: Select approach based on code clarity and correctness
5. **Document findings** in code comments for future maintainers

---

## Phase 6: Render Grass from Terrain OVL

### `OpenCobra/GDK/`

1. **Identify Grass Resources**:
   - Load `terrain/RCT3/Terrain_RCT3.common.ovl` (1,191,981 bytes)
   - Search for terrain texture resources (flexi-textures with "grass" or similar names)
   - Per user: "grass" is in the terrain OVLs, likely indexed by terrain type loader

2. **Load Grass Texture**:
   - Extract the default grass flexi-texture
   - Convert to GDK `Material` with albedo texture

### `OpenRCT3/OpenGL/`

3. **Update `OpenGLRenderer`**:
   - Replace nullbmp with grass texture

**Assets**:
- Path: `/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets/terrain/RCT3/Terrain_RCT3.common.ovl`
- Paired with: `Terrain_RCT3.unique.ovl` (31,037 bytes)

**Milestone**: Flat plane textured with RCT3's default grass

---

## Architecture Summary

```
OpenCobra/
├── GDK/                     # NEW: Backend-agnostic primitives
│   ├── Materials/
│   │   └── Material.cs      # Albedo, normal, specular, etc.
│   ├── Meshes/
│   │   └── MeshData.cs     # Vertices, indices, bounds
│   ├── Shaders/
│   │   └── ShaderProgram.cs # Source + uniforms
│   └── Textures/
│       └── TextureLoader.cs # Load flexi-textures from OVL
└── OVL/                     # EXISTING: OVL parsing

OpenRCT3/
├── Platforms/
│   ├── IGraphicsSurface.cs  # EXISTING: Graphics surface abstraction
│   ├── Rct3InstallFinder.cs # NEW: RCT3 path discovery
│   ├── AppConfig.cs         # NEW: App configuration
│   ├── IRenderer.cs         # NEW: Rendering interface
│   └── OpenGL/
│       └── OpenGLRenderer.cs # NEW: GDK → GL translation
└── Platforms/macOS/
    └── GameOpenGLLayer.cs   # Update to use IRenderer

plugins/
└── ftx-viewer/             # NEW: Decode flexi-textures
```

---

## Dependencies

- **Silk.NET.OpenGL** — Already in `OpenRCT3/OpenRCT3.csproj` (v2.22.0)
- **OVL Library** — Already exists in `OpenCobra/OVL/OVL.csproj`
- **KGySoft.Drawing** — Add to `OpenCobra/GDK/GDK.csproj` for palette-to-RGBA conversion

---

## Testing

Add GDK primitive tests to `OpenCobra/Tests/`:

1. **Material Tests**: Verify properties serialize/deserialize correctly
2. **Mesh Tests**: Validate vertex buffer creation, bounding box calculation
3. **ShaderProgram Tests**: Validate uniform/attribute bindings
4. **TextureLoader Tests**: Test palette loading from `FlexiTextureData`

Example NUnit test pattern (see `ListResources.cs`):
```csharp
using NUnit.Framework;
using OpenCobra.GDK.Materials;

[TestFixture]
public class MaterialTests {
  [SetUp]
  public void SetUp() { }

  [Test]
  public void MaterialCreation_DefaultAlbedoIsNull() {
    var mat = new Material();
    Assert.That(mat.AlbedoTexture, Is.Null);
  }

  [Test]
  public void MaterialCreation_SetsTransparencyFalseByDefault() {
    var mat = new Material();
    Assert.That(mat.TransparencyEnabled, Is.False);
  }
}
```

Add a new NUnit test project `OpenCobra/Tests/GDK/GDKTests.csproj` for GDK data structure unit tests.

Extend existing `IntegrationTests.csproj` in future work.

---

## Staged Tasks

Each phase is a separate task:

1. [x] **Scaffold GDK project** — Create `OpenCobra/GDK/`, Material, Mesh, ShaderProgram primitives
2. [x] **RCT3 installation detection** — Add Rct3InstallFinder, AppConfig to `OpenRCT3/Platforms/`
3. [ ] **Render solid color plane** — Quad in perspective with solid color
4. [ ] **Create ftx-viewer plugin** — Decode flexi-textures
5. [ ] **Prototype palette conversion** — Test KGySoft vs manual lookup for OVL texture palettes
6. [ ] **Render nullbmp texture** — Plane with nullbmp texture
7. [ ] **Render grass texture** — Plane with grass from terrain OVL

---

## References

- **Issue**: https://github.com/open-rct3/open-rct3/issues/4
- **Path lookup**: `src/paths.d`
- **OVL structures**: `OpenCobra/OVL/OVL.cs` (FlexiTextureData at line 252)
- **File types**: `OpenCobra/OVL/Files/FileTypes.cs` (FlexibleTexture = "ftx")
- **Existing abstractions**: `OpenRCT3/Platforms/IGraphicsSurface.cs`, `IWindow.cs`

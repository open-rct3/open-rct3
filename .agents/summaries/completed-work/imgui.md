# Integrate ImGui

## Bugs

1. **🐛 Color wrong**: Color data reaches GPU but window shows wrong color. Suspect `SwapBuffers` or scene update logic.
2. **🐛 Resize**: Ground plane sizing breaks on window resize — GL viewport setup may not be updated.
3. **🐛 Transform not applied**: Ground mesh's `Model.Transform` not applied. Check `Materials.Flat`/`Materials.Textured` shaders or uniform upload.

## Completed Items

| # | Item | Status | Notes |
|---|------|--------|-------|
| 1 | Add ImGui Package | ✅ Done | Uses `Hexa.NET.ImGui` 2.2.9 + `Hexa.NET.ImGui.Backends` 1.0.18 (differs from plan's `Silk.NET.OpenGL.Extensions.ImGui`) |
| 2 | Refactor for Stateful GPU Caching | ✅ Done | `Mesh.cs` owns VAO/VBO/EBO; `Renderer.Render()` is stateless dispatcher |
| 3 | Create ImGui.cs | ✅ Done | Implemented as `OpenCobra/GDK/GUI/Controller.cs` with equivalent methods: `Update(double)`, `StartFrame()`, `Render()` |
| 4 | Create Scenario Editor Window | ✅ Done | `OpenRCT3/Scenario/Editor.cs` with Save, Quit, Setup Park, Choose Finances, Choose Objectives buttons |
| 5 | Render Grass-Colored Terrain Mesh | ✅ Done | `Game.cs` creates plane with `Color.FromArgb(79, 129, 14).ToGl()`; bugs remain (see below) |
| 6 | Add GLState Struct | ✅ Done | Full implementation with `ConcurrentStack<GLState>`, `Push()`/`Pop()`, all state fields captured |
| 7 | Update Renderer | ✅ Done | `Scene.Update` calls `gui.Update()` before `Camera.Update`; `Renderer.Render` uses `GLState.Push()` around GUI |

## Incomplete Items

| # | Item | Status | Notes |
|---|------|--------|-------|
| 5b | Extract Terrain Primitives | ⚠️ Partial | `Primitives.cs` only has `Plane`; no `Cube` primitive defined |

## Design Divergence

- **ImGui Package**: Codebase uses `Hexa.NET.ImGui` + `ImGuiImplOpenGL3` backend. These are incompatible implementations.
- **File Layout**: ImGui integration is in `OpenCobra/GDK/GUI/Controller.cs` rather than `OpenCobra/GDK/ImGui.cs`.

## Questions

1. `Rendered?.Invoke(this, EventArgs.Empty)` appears in plan's renderer snippet but I don't see this event declared on `IRenderer` or invoked anywhere. Is this needed?
2. Should `Primitives.cs` include `Cube` primitives as the plan suggests, or is `Plane` sufficient for terrain?
3. `GLState.IsCoreProfile` has a FIXME comment about feeling incorrect — should this be verified?

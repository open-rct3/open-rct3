# Scenario Editor — Implementation Notes

## Summary

Basic Scenario Editor window with an ImGui debug overlay, rendering a flat grass-colored terrain mesh
(no RCT3 texture files loaded yet). All checklist items completed; remaining items are explicit
deferred work tracked in [TODO.md](../../../TODO.md) (macOS platform lifecycle, OpenGL resource
teardown) rather than this doc.

## What landed

- **ImGui package** ([OpenCobra/GDK/GDK.csproj](../../../OpenCobra/GDK/GDK.csproj)):
  `Silk.NET.OpenGL.Extensions.ImGui` 2.23.0 added.
- **Stateful GPU caching**: `Mesh` and `Material` own GL handles (Vao/Vbo/Ebo for meshes; program
  for materials) and expose `Upload()` + `IsUploaded`. [`Renderer.Render`](../../../OpenCobra/GDK/Renderer.cs)
  is now a stateless immediate-mode dispatcher — bind VAO, draw elements, unbind. `Scene` triggers
  `Upload()` on changes.
- **ImGui context** ([OpenCobra/GDK/ImGui.cs](../../../OpenCobra/GDK/ImGui.cs)): initialized in
  `Scene` ctor, hooked into the OpenGL renderer for `Begin`/`End` frame calls, renders a debug
  overlay per frame.
- **Scenario Editor window** ([OpenRCT3/Scenario/Editor.cs](../../../OpenRCT3/Scenario/Editor.cs)):
  minimal ImGui window with Save / Quit icon row and Setup Park / Choose Finances / Choose Objectives
  & Challenges labeled buttons.
- **Grass-colored terrain mesh**: existing terrain rendering detects the `nullbmp` texture slot and
  substitutes `Color.FromArgb(79, 129, 14)` as a temporary solid color. The `nullbmp` logic is left
  in place for later restoration when real textures land.
- **Primitives module** ([OpenCobra/GDK/Meshes/Primitives.cs](../../../OpenCobra/GDK/Meshes/Primitives.cs)):
  static normalized mesh data for `Cube` and `Plane`; each takes an optional `Vector4 color` and
  returns a `Mesh`. Counterclockwise winding, consistent with OpenGL/Direct3D convention.
- **`GLState` struct** ([OpenCobra/GDK/GLState.cs](../../../OpenCobra/GDK/GLState.cs)):
  `readonly struct` capturing the OpenGL state machine via a thread-safe `ConcurrentStack<GLState>`.
  `GLState.Push()` queries active texture, current program, texture/array/VAO bindings, scissor box,
  blend src/dst/equation, enable flags (Blend/CullFace/DepthTest/StencilTest/ScissorTest), and polygon
  mode; `Pop()` restores with `GL.CheckError` on every call. Used as
  `using var guard = GLState.Push();` (RAII pattern) to scope GL state mutations around ImGui draws.
- **Renderer integration**: `Scene.Update` calls `imguiController.Update(deltaSeconds)` and
  `Camera.Update(aspectRatio)`. `Renderer.Render` binds/unbinds default VAO and program, then runs
  the ImGui pass inside `using var imguiState = GLState.Push();` so scene state is restored after
  ImGui's Blend+ScissorTest-enable / CullFace+DepthTest+StencilTest-disable mutations.

## Deferred (not in this doc)

- macOS framebuffer update on resize / screen changes ([TODO.md](../../../TODO.md), `GameViewController.cs:35`).
- macOS graphics + unmanaged resource teardown on exit ([TODO.md](../../../TODO.md), `AppDelegate.cs:24`).

## Testing

Manual: `OpenRCT3` launches with the Scenario Editor window and a flat grass-colored ground plane
beneath an ImGui debug overlay. No regression in existing tests.

## References

- [OpenCobra/GDK/GDK.csproj](../../../OpenCobra/GDK/GDK.csproj) — ImGui package reference
- [OpenCobra/GDK/ImGui.cs](../../../OpenCobra/GDK/ImGui.cs) — ImGui context + debug overlay
- [OpenCobra/GDK/GLState.cs](../../../OpenCobra/GDK/GLState.cs) — GL state save/restore
- [OpenCobra/GDK/Meshes/Primitives.cs](../../../OpenCobra/GDK/Meshes/Primitives.cs) — `Cube`/`Plane`
- [OpenCobra/GDK/Scene.cs](../../../OpenCobra/GDK/Scene.cs) — `Update` / `Render` integration
- [OpenCobra/GDK/Renderer.cs](../../../OpenCobra/GDK/Renderer.cs) — stateless `Render`
- [OpenRCT3/Scenario/Editor.cs](../../../OpenRCT3/Scenario/Editor.cs) — editor window

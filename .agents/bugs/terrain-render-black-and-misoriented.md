# Bug: Terrain renders solid black, and its silhouette reads as mis-oriented ("upside down")

## Status: Open — one real bug fixed along the way, but the reported symptom is unresolved

## Summary

After wiring up `Camera.Frame()` to point the camera at the loaded park (see
`.agents/plans/features/terrain-heightmap.md` and `OpenRCT3/Game.cs`), the terrain mesh finally renders
on screen instead of being entirely outside the view frustum. But what renders is wrong in two ways:

1. **Solid black**, regardless of the vertex color fed into `TerrainMeshBuilder.Build`. Swapping the
   intended grass green (`Color.FromArgb(79, 129, 14)`) for unmistakable magenta
   (`Color.FromArgb(255, 0, 255)`) produced **identical** output — confirmed via direct pixel sampling
   of the captured screenshot: `R=0 G=0 B=0 A=255` at multiple points inside the shape, both before and
   after the color swap.
2. **The silhouette looks wrong** — user's words: "looks like I'm looking up at it from below," "looks bad, like it's y-flipped in screen space." The rendered shape is a black parallelogram/diamond occupying a
   large portion of the window.

Screenshots (all captured via `.claude/skills/drive-native-app`, see that skill for how to reproduce):
- Before any fix: solid black parallelogram filling most of the window, clipped at the frame edges.
- After the camera-framing-distance fix (below): a smaller, fully-contained black diamond — geometry
  now fits within the viewport per the regression tests, but still solid black and still visually
  reads as wrong to the user.
- After the GLSL core-profile fix (below): **no visible change** — still the same solid black diamond,
  same apparent size/position. This is the confusing part: a real, independently-justified bug was
  fixed, and it had zero visible effect.

## What was investigated and ruled out

### Vertex color data plumbing — ruled out
- `TerrainMeshBuilder.AddTopFace`/`AddCliffFace` correctly set `Color = color` on every emitted
  `Vertex` (confirmed by reading the source, unchanged from before this investigation).
- `ColorExtensions.ToGl()` correctly normalizes `System.Drawing.Color` to a `Vector4` (checked by hand:
  `Color.FromArgb(79, 129, 14)` → `(0.31, 0.51, 0.05, 1.0)`, clearly non-black).
- `Mesh.Upload`'s `a_Color` attribute pointer offset (32 bytes into the 48-byte `Vertex` struct) matches
  `Vertex`'s actual field layout (`Position` 0–11, `Normal` 12–23, `TexCoord` 24–31, `Color` 32–47) —
  confirmed by re-reading `Vertex.cs`'s `[StructLayout(Pack = 1)]` fields in order.
- **The magenta swap test is the strongest evidence here**: if any of the above were subtly wrong, a
  drastically different input color should have produced *some* visible difference. It produced none —
  the exact same `(0,0,0)` pixels. This points away from "wrong data was uploaded" and toward "the
  fragment shader's color output isn't taking effect at all," which is what led to the next finding.

### Camera.cs math — verified correct in isolation, but this verification method has a real gap (see below)
- `Camera.CreatePerspectiveFieldOfViewGL` (replacing `Matrix4x4.CreatePerspectiveFieldOfView`, which
  targets Direct3D's `[0,1]` NDC-z range instead of OpenGL's `[-1,1]`) was verified by direct computation:
  a point at exactly the near-plane distance maps to NDC z = −1, a point at the far-plane distance maps
  to NDC z = +1. Covered by `OpenCobra.Tests/GDK/CameraTests.cs`.
- `Camera.Frame(target, distance)` was verified to place the eye at the correct distance and direction
  from the target, and a subsequent `Update()` projects the target back to screen center. Covered by the
  same test file.
- `Park`-bounds framing (center + diagonal distance, with a `1.8×` safety margin added after the diamond
  rotation's near-corner foreshortening was found to clip the naive `distance = diagonal` framing) was
  verified to keep all four corners of the *actual rendered mesh* (including the 5-tile/20m OOB border,
  not just `Park.BuildableBounds`) within `[-1, 1]` NDC for both the default 128×128 map and a smaller
  16×16 map. Covered by `OpenRCT3.Tests/Simulation/CameraFramingTests.cs`.
- **The gap**: all of the above verification uses `System.Numerics.Vector4.Transform(vector, matrix)` in
  C#. This is a *different code path* from what the GPU actually does:
  `Matrix4x4` → `MatrixExtensions.ToGl()` (row-major→column-major transpose for GLSL's column-vector
  convention) → `glUniformMatrix4fv` → GLSL `u_ViewProj * u_Model * vec4(a_Position, 1.0)`. The C#-side
  math being self-consistent does not prove the GPU-side math produces the same result. This gap was
  never actually closed — see "Suggested next steps."
- A concrete red flag from this gap: a throwaway diagnostic test (`CameraTests.ZZZ_Diagnostic`, since
  removed) computed the *expected* NDC coordinates of the mesh corners for the real default-map framing
  and got small values close to screen center (roughly ±0.3 to ±0.5 in both axes after the margin fix).
  The actual on-screen shape in every screenshot is visibly much larger than that — closer to filling
  half to most of the window. **This discrepancy between calculated and observed NDC extent was noted
  but never resolved**, and is probably the single most important lead for whoever picks this back up.

### GLSL profile mismatch — found, fixed, but did not resolve the symptom
`OpenRCT3/Platforms/SurfaceSettings.cs` creates the GL context with
`ContextProfileMask.CoreProfileBit | ContextFlagMask.ForwardCompatibleBit` (confirmed live: the log
reports `"Created OpenGL context: OpenGL v4.1 Core profile"`). But `OpenCobra/GDK/Materials/Material.cs`
(`Flat` and `Textured`) both declared `#version 120` shaders using `attribute`/`varying` and
(in the fragment shader) `gl_FragColor` — all removed entirely from core-profile GLSL. Mixing
`#version 120` compatibility syntax with a genuinely forward-compatible core context is a known
cross-driver footgun: some drivers compile and link it anyway (explaining why no shader/program error
ever appeared in the logs — `CheckShaderError`/`CheckProgramError` never fired), but the deprecated
built-ins can silently fail to wire up to the actual draw buffer.

This was fixed: both materials were rewritten to `#version 410 core` (matching the actual 4.1 context),
`attribute`→`in`, `varying`→`out`/`in`, `gl_FragColor`→an explicit `out vec4 FragColor`, and (in
`Textured`) `texture2D`→`texture` (also core-removed) plus a small dead-code cleanup
(`v_TexCoord = v_TexCoord;` before the real assignment, a leftover no-op). The build succeeds, both
`.NET` test suites (14 + 43 tests) still pass.

**This was a real, independently-justified bug** — the shader/context profile mismatch is exactly the
kind of thing that should be fixed regardless of whether it's the terrain's problem — but rebuilding and
re-running after this fix produced **no visible change whatsoever**: same solid black diamond, same
apparent size and position, pixel-identical as far as a screenshot comparison shows. This means either:
- the GLSL fix wasn't the (sole) cause of the black color, and something else is also/instead
  responsible, or
- the running executable wasn't actually picking up the rebuilt `GDK.dll` (not verified — see next
  steps), or
- there's a second, independent bug with the same visible symptom (fully black fragments).

## Open questions / suggested next steps

1. **Close the C#-math-vs-GPU-render gap.** Nothing in this investigation ever confirmed that
   `ToGl()`'s transpose actually produces the matrix the GPU uses, end to end. A good next step: render
   a single, trivially-verifiable test triangle (e.g. one vertex at the origin, bright red, with an
   identity view-projection) and confirm its on-screen pixel position/color matches expectation exactly,
   before trusting any more C#-side matrix math against the real terrain mesh.
2. **Confirm the rebuilt DLL was actually loaded.** After the GLSL fix, the app was rebuilt via
   `dotnet build OpenCobra/GDK/GDK.csproj` and the `drive-native-app` skill's `Build`/`Launch` actions,
   but the *lack* of any visible change is surprising enough that it's worth explicitly verifying
   (e.g. checking `GDK.dll`'s last-write timestamp against the fix, or adding a temporary log line with
   a version marker printed at startup) rather than assuming the new shader source was actually in play.
3. **Turn on the GL debug callback and confirm it's actually active.** `GLExtensions.HookupDebugCallback`
   (`OpenRCT3/OpenGL/GLExtensions.cs`) only registers if `GL_ARB_debug_output`/`GL_KHR_debug` are
   present, and logs at `LogLevel.Debug` — no such messages appeared in any log capture during this
   investigation, across either the `#version 120` or `#version 410 core` shader versions. Confirm the
   callback is actually registered and firing at all (e.g. by deliberately triggering a known GL error)
   before trusting its silence as "no problems."
4. **Reconcile the calculated-vs-observed NDC size discrepancy** described above — this is the most
   concrete, falsifiable lead. If the actual rendered shape is really larger in NDC than the C# math
   predicts, something in the GPU-side pipeline (matrix upload, `ToGl()`, or the shader itself) is not
   doing what the C# side assumes.
5. Screenshot capture during this investigation was made unreliable by the fact that this was the
   developer's live, actively-used desktop (other applications stealing foreground/focus mid-capture,
   one capture round even grabbing an unrelated window's content instead of the game). Whoever continues
   this should get a clean, dedicated screenshot before drawing conclusions, and should be skeptical of
   any single screenshot without cross-checking the window title/process ID actually matches.

## How this was found

User-reported: "The camera math feels wrong at runtime," later clarified after a screenshot to be
specifically about mis-orientation/mis-perspective of the rendered terrain, and separately, "the color
bug is obviously a problem with data hand-off to the GPU and maybe even the GLSL shader logic." Both the
GLSL profile bug and the camera-framing-distance bug (see `.agents/plans/features/terrain-heightmap.md`
for the latter's fix) were found via this investigation, but neither fix resolved the user-visible
symptom in the end. Filed as an open bug rather than continuing to guess, per user request.

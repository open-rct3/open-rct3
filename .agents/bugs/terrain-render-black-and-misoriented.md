# Bug: Terrain renders solid black, and its silhouette reads as mis-oriented ("upside down")

## Status: Geometry fixed and verified. Color bug still open — deeply investigated, root cause not yet confirmed.

Two independent, real bugs in the camera/projection pipeline have been found, fixed, covered by
regression tests, and visually confirmed fixed. A third, separate bug (the fragment color is always
solid black) remains open despite an extensive investigation that ruled out every part of the data
pipeline except one remaining suspect (see "Leading suspect" below).

## Summary

After wiring up `Camera.Frame()` to point the camera at the loaded park (see
`.agents/plans/features/terrain-heightmap.md` and `OpenRCT3/Game.cs`), the terrain mesh renders on
screen instead of being entirely outside the view frustum. What renders was wrong in three
independent ways, two of which are now fixed:

1. ~~**Mis-shaped/mis-scaled silhouette**~~ — **FIXED**, see "Fix 1: `MatrixExtensions.ToGl()` transpose
   bug" below.
2. ~~**Invisible/clipped entirely at some map sizes**~~ — **FIXED**, see "Fix 2: far clip plane didn't
   scale with framing distance" below.
3. **Solid black regardless of vertex color** — **STILL OPEN**. Swapping the intended grass green
   (`Color.FromArgb(79, 129, 14)`) for unmistakable magenta (`Color.FromArgb(255, 0, 255)`) produces
   **identical** output — confirmed via direct pixel sampling: `R=0 G=0 B=0 A=255` at multiple points
   inside the shape, regardless of vertex color, GLSL shader version, or (now) camera math.

After both geometry fixes, a clean screenshot shows a well-formed, correctly-sized, correctly-centered
black diamond — the shape itself now looks right, it's just entirely black instead of grass-green.

## Fix 1: `MatrixExtensions.ToGl()` transpose bug — FIXED

`OpenRCT3/OpenGL/MatrixExtensions.cs` converts a `System.Numerics.Matrix4x4` to the `float[16]` handed
to `glUniformMatrix4fv`. The original implementation shuffled indices in a way that *looked* like a
transpose but wasn't one end-to-end:

```csharp
// BEFORE (buggy)
public static float[] ToGl(this Matrix4x4 matrix) => [
  matrix.M11, matrix.M21, matrix.M31, matrix.M41,
  matrix.M12, matrix.M22, matrix.M32, matrix.M42,
  matrix.M13, matrix.M23, matrix.M33, matrix.M43,
  matrix.M14, matrix.M24, matrix.M34, matrix.M44
];
```

**The two conventions in play:**
- `System.Numerics.Matrix4x4` (inherited from XNA/Direct3D) is a **row-vector** matrix: points are
  transformed as `v' = v * M`, and translation lives in the *last row* (`M41`/`M42`/`M43`) — confirmed
  by `Matrix4x4.CreateTranslation`, which sets exactly those fields.
- GLSL's `mat4 * vec4` is a **column-vector** operation: `v' = M * v`, which requires translation in the
  *last column*. `glUniformMatrix4fv`'s `transpose` parameter, when `GL_FALSE`, tells GL "this data is
  already column-major, use it as-is" — it does **not** perform a transpose for you.

Converting a row-vector matrix for a column-vector shader requires an actual **transpose** (swap
`M[i][j]` and `M[j][i]`), not merely a relabeling of storage order. The old code's index shuffle was a
no-op in disguise: row-major storage of `M` and column-major storage of `Mᵀ` are byte-identical, and the
old index order was actually the row-major flatten of `Mᵀ` — which equals the column-major flatten of
`M`. So it uploaded `M` itself, unchanged, not `Mᵀ`.

**Concrete proof** (worked by hand with a pure translation matrix `T`, `T.M41=tx` etc.): the old code fed
GL a matrix that put `tx/ty/tz` in the row that produces the **w** component, not x/y/z. Translation
never moved the vertex position at all — it leaked into `w`, the term the perspective divide
(`clip.xyz / clip.w`) uses to scale the *whole point*, scaling x/y/z by an amount that varies per-vertex.
That's exactly the kind of corruption that produces a shape that's the wrong size, skewed, and reads as
"flipped."

**The fix**, now applied:

```csharp
// AFTER (fixed)
public static float[] ToGl(this Matrix4x4 matrix) => [
  matrix.M11, matrix.M12, matrix.M13, matrix.M14,
  matrix.M21, matrix.M22, matrix.M23, matrix.M24,
  matrix.M31, matrix.M32, matrix.M33, matrix.M34,
  matrix.M41, matrix.M42, matrix.M43, matrix.M44
];
```

Natural field order, no index shuffle. Column-major-reading this array reconstructs `Mᵀ`, which is what
a column-vector shader needs to reproduce the same `v' = v*M` result the C#-side math already assumed.

**Why this was never caught:** every regression test that existed before this fix
(`CameraTests.cs`, `CameraFramingTests.cs`) verified the view/projection math using
`Vector4.Transform`/`Matrix4x4` directly in C# — none of them ever called `ToGl()`, which had zero direct
test coverage. **Now fixed**: `OpenRCT3.Tests/OpenGL/MatrixExtensionsTests.cs` calls `ToGl()` directly
and asserts on the raw `float[16]` output (translation lands in the last column, not the last row), and
`OpenCobra/Tests/GDK/CameraTests.cs` has a companion test
(`Update_TargetProjectsNearScreenCenter_UnderColumnVectorConvention`) that manually transposes
`Camera.Value` and re-derives the NDC result under column-vector semantics, since the GDK project can't
reference the game's `MatrixExtensions` directly.

**Verified visually**: after this fix (and Fix 2 below), the terrain renders as a correctly-shaped,
correctly-proportioned diamond, matching what the camera-framing math predicts. This closes the
"calculated-vs-observed NDC size discrepancy" that the original investigation flagged as its top lead.

## Fix 2: far clip plane didn't scale with framing distance — FIXED

Independently of the transpose bug, `Camera.Update()` hardcoded the projection's clip planes at
`nearPlaneDistance: 0.1f, farPlaneDistance: 1000f`. For the default 128×128 map,
`Game.cs`'s `Scene.Camera.Frame(parkCenter, parkDiagonal * 1.8)` places the eye **1303 units** from the
target — already past the 1000-unit far plane. The entire terrain mesh sat beyond the far clip plane and
should have been clipped away entirely.

Under the (then-still-present) transpose bug, the projection matrix's clip-space z/w relationship wasn't
correctly wired up to the GPU at all, so proper near/far clipping never actually happened — it
accidentally let a garbled, mis-shaped diamond through anyway. Once Fix 1 was applied on its own, GL
started clipping correctly, and the terrain briefly went **completely invisible** (this was the
"now the terrain is invisible" checkpoint mid-investigation) — confirming the far-plane bug was real,
not a fix regression.

**The fix**, now applied in `OpenCobra/GDK/Camera.cs`:
- Near plane: **1cm** (`0.01f`), matching the engine's world-space unit (`Park.TileSize = 4.0f` meters
  per tile, i.e. 1 world unit = 1 meter).
- Far plane: derived from the *current* eye-to-target distance (`Vector3.Distance(Eye, Target) * 2`)
  instead of a fixed constant. Since `Frame()` already picks a distance sized to the actual park being
  framed, the far plane now automatically scales with map size instead of being tuned to one specific
  map. The 2× margin leaves headroom for a future cube-mapped skybox drawn just outside a park's total
  (OOB-inclusive) bounds — a planned enhancement — without needing to be revisited when that lands.

**Test coverage added:**
- `OpenCobra/Tests/GDK/CameraTests.cs`: `Update_NearPlaneIsOneCentimeter` and
  `Update_FarPlaneScalesWithFramingDistance` (the latter sweeps several very different framing distances
  and proves the far plane is always exactly `2×` that distance).
- `OpenRCT3.Tests/Simulation/CameraFramingTests.cs`: the existing framing tests only ever checked NDC
  X/Y were in `[-1, 1]` — **never NDC Z**, which is exactly the gap that let the far-plane bug ship
  unnoticed (a point behind the far plane still produces plausible-looking X/Y ratios from
  `Vector4.Transform` in C#; only real GPU clipping — or an explicit Z-range check — catches it). Both
  existing tests now also assert NDC Z is in range, and a new
  `FramedCamera_KeepsLargerCustomParkRenderedMeshCornersOnScreen` test proves the fix scales up to a much
  larger map (512×512), not just the default.

**Verified visually**: terrain renders fully on-screen, correctly sized, at the default map size.

## Fix 3 candidate (unrelated, applied opportunistically): GLSL core-profile mismatch

Carried over from before this session, already fixed and unchanged: `OpenCobra/GDK/Materials/Material.cs`
(`Flat`/`Textured`) now use `#version 410 core` (`in`/`out`, explicit `FragColor`, `texture()` instead of
`texture2D()`) instead of `#version 120` (`attribute`/`varying`/`gl_FragColor`), matching the
`CoreProfileBit | ForwardCompatibleBit` context `SurfaceSettings.cs` actually creates. This is a real,
independently-justified fix (mixing compatibility-profile GLSL syntax with a forward-compatible core
context is a known cross-driver footgun), but on its own it produced **no visible change** to the
black-color symptom — see below.

## Still open: solid black fragment color

With both geometry bugs fixed, the terrain renders as a correctly-shaped, correctly-scaled diamond —
and it is **still solid black**, exactly as before either fix. This rules out the geometry bugs as the
cause of the color symptom (they were real, but always a separate issue that happened to be masked by
the same visual "wrong diamond" complaint).

### What was re-verified from scratch, with the geometry now correct

Using a series of temporary diagnostic builds (shader edits, NLog instrumentation in `Mesh.cs` and
`Renderer.cs`, direct GPU buffer readback) — all removed again before this fix; the working tree is
clean:

1. **CPU-side vertex data is correct.** `TerrainMeshBuilder` sets `Vertex.Color` correctly on every
   vertex; logging `vertices[0].Color` right before upload showed `<1, 0, 1, 1>` (magenta) as expected.
2. **The GPU buffer actually received the correct bytes.** `glGetBufferSubData` reading back 16 bytes at
   offset 32 (where `Vertex.Color` should live in the 48-byte struct) *from the GPU-resident buffer,
   after upload* returned `<1, 0, 1, 1>` — the correct magenta, byte-for-byte. This rules out any bug in
   `BufferData`, struct layout/packing, or `Marshal.SizeOf`.
3. **The vertex attribute is correctly bound, at both upload time and actual draw time.** Querying
   `glGetAttribLocation`, `GL_VERTEX_ATTRIB_ARRAY_ENABLED`, `GL_VERTEX_ATTRIB_ARRAY_BUFFER_BINDING`, the
   currently-bound VAO (`GL_VERTEX_ARRAY_BINDING`), and the currently-bound program
   (`GL_CURRENT_PROGRAM`) — both immediately after `Mesh.Upload` finishes *and* immediately before the
   real `gl.DrawElements` call in `Renderer.Render` — showed fully consistent, correct state every time:
   `a_Color` at location 1, enabled, bound to the correct VBO, with the correct program active. Nothing
   changes between upload and draw.
4. **The fragment shader genuinely receives `(0, 0, 0, ~1)` for `v_Color`, not some other value.** A
   diagnostic build with `FragColor = v_Color + vec4(0.5, 0.0, 0.0, 0.0)` (keeping `v_Color` referenced,
   so the compiler can't dead-code-eliminate it) produced pixels reading exactly `(127, 0, 0, 255)` —
   i.e. `v_Color` really is `(0, 0, 0, ~1)` inside the fragment shader, not some garbage/uninitialized
   value, not the correct magenta misread as something else.
5. **`gl.CheckError` is live and firing** (it's gated `[Conditional("DEBUG")]`, and `DEBUG` is defined by
   default for Debug-configuration builds — confirmed, not assumed) — and it never caught a GL error at
   any of the checkpoints above.
6. **A GLSL compiler quirk was found, but doesn't explain the bug**: this driver's GLSL compiler
   aggressively dead-code-eliminates a vertex attribute if the only thing consuming it is provably a
   compile-time-zero expression (e.g. `a_Color * 0.0`) — even though that's not strictly IEEE-754-correct
   in general (NaN/Infinity inputs). This was discovered when a diagnostic shader crashed the app
   (`Debug.Assert(colLoc >= 0)` firing → `Environment.FailFast`, confirmed via a Windows Event Viewer
   `CLR20r3`/`FailFast` entry, since a hard native-style crash bypasses NLog entirely). This elimination
   behavior is real but **doesn't apply to the actual shader**, which does a plain, unconditional
   `v_Color = a_Color;` passthrough with no arithmetic to fold — and indeed `colLoc` was found as `1`
   (not eliminated) in every non-crashing test.

So: every single thing that can be inspected from the CPU/API side — the source data, the uploaded GPU
bytes, the attribute binding state at the exact moment of the draw call — is provably correct, and yet
the shader-visible value is wrong. That is a very unusual combination.

### Leading suspect: `CastFrom<From>.To<To>` is an unsound bit-reinterpretation cast

`OpenCobra/GDK/Memory/Cast.cs`:

```csharp
public struct CastFrom<From> where From : unmanaged {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static To To<To>(From value) where To : unmanaged => Unsafe.As<From, To>(ref value);
}
```

`Unsafe.As<TFrom, TTo>(ref TFrom source)` reinterprets a **memory address** as a different type — it does
not perform a numeric conversion. When `TTo` is larger than `TFrom` (e.g. `int` → `nint`, 4 bytes → 8
bytes on x64, which is exactly how every vertex attribute *offset* is cast in `Mesh.cs` —
`CastFrom<int>.To<nint>(32)` and friends), the dereference reads **past the end of the source value's
storage**, picking up whatever bytes happen to follow it (stack garbage, padding, an adjacent local).
This is unsound and is a completely different operation from a normal explicit/implicit numeric cast
`(nint)value`.

By contrast, `CastFrom<int>.To<uint>(...)` — used for attribute *locations*, e.g. `colLoc`/`posLoc` — is
a **same-size** cast (`int` and `uint` are both 4 bytes), which is a legitimate, safe reinterpretation.
This distinction matters: it means every *location* cast in the codebase has been silently correct all
along (consistent with every diagnostic above showing correct, consistent attribute locations), while
every *offset* cast (`int` → `nint`, used for the `glVertexAttribPointer` byte-offset parameter) is
running through code with genuine undefined behavior.

**This was not conclusively proven as the cause before the investigation had to stop.** A direct
diagnostic (logging `CastFrom<int>.To<nint>(32)` to see what it actually evaluates to) was added, but the
capture kept getting tangled up with an unrelated app crash from the dead-code-elimination quirk above
(triggered by an earlier, different diagnostic shader edit) and inconsistent/stale log-file reads, and a
clean value was never actually captured before cleanup. It's entirely plausible the JIT happens to
optimize away the unsoundness for compile-time-constant arguments like the literal `32`/`0`/`12`/`24`
used at every call site in `Mesh.cs` (turning `Unsafe.As<int, nint>` into a normal widening move) — which
would make this a real, serious bug in general but *not* the explanation for this specific symptom. That
needs to be checked, not assumed either way.

## Suggested next steps

1. **Get a clean read on `CastFrom<int>.To<nint>(32)`'s actual value**, isolated from any other
   diagnostic change (no shader edits in the same build — those risk triggering the unrelated
   dead-code-elimination crash and burning the run). A minimal repro: a standalone unit test (no GL
   context needed) that calls `CastFrom<int>.To<nint>(32)` and asserts it equals `32`. If it doesn't, that
   settles it. This is a five-minute check that got lost in a much longer live-app diagnostic session and
   should be done first, before touching the running app again.
2. **Fix `CastFrom` regardless of whether it's this bug.** `Unsafe.As` between differently-sized types
   is unsound and should not be used for numeric conversions like `int → nint`. Either add a size-check
   `Debug.Assert(Unsafe.SizeOf<From>() == Unsafe.SizeOf<To>())` inside `CastFrom.To<To>` to fail loudly on
   misuse, or (simpler) stop using it for the offset casts in `Mesh.cs` and use a normal `(nint)offset`
   cast there instead, reserving `CastFrom` for genuine same-size reinterprets like the location casts.
3. **If fixing `CastFrom`'s offset usage doesn't resolve the color bug**, the remaining unexplored
   surface is narrow: everything upstream of `glVertexAttribPointer`'s offset parameter has been
   verified correct, and everything about the shader source/compilation has been verified correct except
   the compiler's dead-code-elimination quirk (item 6 above) — which doesn't apply to the real passthrough
   shader as written, but is worth double-checking doesn't apply in some less obvious way (e.g. whole
   -program link-time analysis noticing `v_Color`'s only consumer, `FragColor = v_Color;`, and somehow
   still folding something). Turning on the GL debug callback (`GLExtensions.HookupDebugCallback`) and
   confirming it's actually registered and firing (never verified in any prior session — see below)
   remains a good independent check.
4. **Turn on the GL debug callback and confirm it's actually active.** `GLExtensions.HookupDebugCallback`
   (`OpenRCT3/OpenGL/GLExtensions.cs`) only registers if `GL_ARB_debug_output`/`GL_KHR_debug` are
   present, and logs at `LogLevel.Debug` — no such messages have appeared in any log capture across this
   entire investigation. Confirm the callback is actually registered and firing at all (e.g. by
   deliberately triggering a known GL error) before continuing to trust its silence as "no problems."
5. Screenshot/log capture during this investigation was made unreliable more than once: other windows
   stealing focus mid-capture, and — during the color investigation — an app crash (`Debug.Assert` →
   `Environment.FailFast`) that bypassed NLog entirely and required checking Windows Event Viewer
   (`Get-WinEvent -FilterHashtable @{LogName='Application'}`, event ID 1025) to actually see the crash
   stack trace, since the NLog file simply stopped mid-line with no error recorded. Whoever continues
   this should keep that Event Viewer check in their toolkit for any future silent app-exit during this
   investigation, and should re-verify the specific PID/timestamp of any log or screenshot before trusting
   it, since multiple stale/archived log files exist side by side
   (`%APPDATA%\OpenRCT3\logs\app.<timestamp>.log`) and it's easy to read a stale one.
6. Unrelated but worth knowing: `.claude/skills/drive-native-app/scripts/AppDriver.ps1` had a real bug
   (`GetCurrentThreadId` declared against `user32.dll` instead of `kernel32.dll`), which intermittently
   broke `ForceForeground` and thus `Screenshot`/focus-dependent actions. Fixed during this session; if a
   fresh checkout of the skill script reintroduces it, the symptom is
   `EntryPointNotFoundException: Unable to find an entry point named 'GetCurrentThreadId' in DLL 'user32.dll'`.

## How this was found

User-reported: "The camera math feels wrong at runtime," later clarified after a screenshot to be
specifically about mis-orientation/mis-perspective of the rendered terrain, and separately, "the color
bug is obviously a problem with data hand-off to the GPU and maybe even the GLSL shader logic."

The `ToGl()` transpose bug (Fix 1) was deduced analytically by working through the row-vector vs.
column-vector convention mismatch by hand, then confirmed by new direct tests of `ToGl()`'s output — the
first tests in the project to ever exercise that function. Applying it alone made the terrain render
correctly-shaped but revealed the far-plane bug (Fix 2), which was diagnosed by computing the actual
framing distance for the default map (1303 units) against the hardcoded far plane (1000 units) — a
five-line calculation that had never been done before, because no prior test ever checked NDC Z, only
NDC X/Y. Both fixes were then verified visually via `.claude/skills/drive-native-app`, closing out the
"mis-orientation" half of this bug.

The color bug's investigation (this session) went much deeper than any prior attempt — GPU buffer
readback, live attribute-state introspection at the actual draw call, and a fragment-shader diagnostic
that proved `v_Color` really does evaluate to black at runtime — and surfaced a genuine, previously
unknown unsoundness in `CastFrom<From>.To<To>` as the leading suspect. It was filed as still-open rather
than continuing to guess, since confirming or ruling out that suspect needs a clean, isolated test that
the live-app diagnostic session (with its crash-and-stale-log complications) wasn't able to produce
before time ran out on this session.

# Bug: Terrain renders solid black, and its silhouette reads as mis-oriented ("upside down")

## Status: RESOLVED. All three symptoms fixed, tested, and visually verified.

## Fix 1: `MatrixExtensions.ToGl()` transpose bug — FIXED

`System.Numerics.Matrix4x4` is row-vector (`v' = v*M`, translation in `M41/M42/M43`); GLSL's `mat4 * vec4`
is column-vector (`v' = M*v`, translation must be in the last column). `ToGl()` needs to upload the actual
**transpose**. The old code shuffled indices to *look* like a transpose but wasn't one — row-major storage
of `M` and column-major storage of `Mᵀ` are byte-identical, and the old index order was the row-major
flatten of `Mᵀ`, which equals the column-major flatten of `M`. Net effect: it uploaded `M` unchanged, so
translation leaked into clip-space `w` instead of x/y/z, scaling each vertex by a position-dependent
amount — producing a mis-shaped, mis-scaled, "flipped"-looking silhouette.

**Fix**: emit fields in natural order (no shuffle) — column-major-reading that array reconstructs `Mᵀ`.

```csharp
public static float[] ToGl(this Matrix4x4 matrix) => [
  matrix.M11, matrix.M12, matrix.M13, matrix.M14,
  matrix.M21, matrix.M22, matrix.M23, matrix.M24,
  matrix.M31, matrix.M32, matrix.M33, matrix.M34,
  matrix.M41, matrix.M42, matrix.M43, matrix.M44
];
```

Never caught before because no test called `ToGl()` directly — every test verified C#-side math only
(`Vector4.Transform`). Now covered: `OpenRCT3.Tests/OpenGL/MatrixExtensionsTests.cs` asserts on the raw
`float[16]` output; `OpenCobra/Tests/GDK/CameraTests.cs` has a companion GDK-side test since that project
can't reference `MatrixExtensions`. Verified visually — terrain now renders as a correctly-shaped,
correctly-proportioned diamond.

## Fix 2: far clip plane didn't scale with framing distance — FIXED

`Camera.Update()` hardcoded `farPlaneDistance: 1000f`. For the default 128×128 map, `Game.cs` frames the
camera at `parkDiagonal * 1.8` ≈ **1303 units** — already past the far plane, so the entire mesh should've
been clipped away. It wasn't, because Fix 1's bug also broke clip-space z/w, so real clipping never
happened. Fixing Fix 1 alone made the terrain briefly disappear entirely, confirming this second bug.

**Fix** (`OpenCobra/GDK/Camera.cs`): near = **1cm** (`0.01f`, matches the engine's world unit — 1 unit = 1
meter); far = `Vector3.Distance(Eye, Target) * 2`, so it auto-scales with whatever distance `Frame()` was
given instead of a map-size-specific constant. The 2× margin leaves room for a future skybox drawn outside
a park's total bounds.

Added `Update_NearPlaneIsOneCentimeter` / `Update_FarPlaneScalesWithFramingDistance` (GDK) and NDC-Z
assertions to the existing framing tests (game) — those tests previously only checked NDC X/Y, which is
exactly the gap that let this ship (a point behind the far plane still produces plausible X/Y ratios from
`Vector4.Transform`; only Z or real GPU clipping catches it). Verified visually.

## Fix 3 (unrelated, pre-existing): GLSL core-profile mismatch

`Material.cs` shaders were `#version 120` (`attribute`/`varying`/`gl_FragColor`) against a
`CoreProfileBit | ForwardCompatibleBit` context. Fixed to `#version 410 core` in an earlier session. Real,
independently-justified fix, but produced no visible change to the black-color symptom on its own.

## Fix 4: solid black fragment color — FIXED

With geometry corrected (Fixes 1–2), the terrain was a well-formed diamond — still **solid black**,
unaffected by vertex color (magenta swap test: identical `(0,0,0,255)` output). Ruled out, via live GPU
introspection (temporary diagnostics, since removed):

- **CPU vertex data**: correct (`vertices[0].Color` logged as magenta before upload).
- **GPU buffer contents**: `glGetBufferSubData` at offset 32 post-upload reads back correct magenta,
  byte-for-byte. Rules out `BufferData`, struct layout, `Marshal.SizeOf`.
- **Attribute binding**, checked both right after `Mesh.Upload` and immediately before the real
  `gl.DrawElements`: `a_Color` at location 1, enabled, bound to the correct VBO, correct program active,
  in both places. Nothing changes between upload and draw.
- **Fragment shader input itself**: `FragColor = v_Color + vec4(0.5,0,0,0)` (kept `v_Color` referenced so
  it can't be dead-code-eliminated) rendered exactly `(127,0,0,255)` — `v_Color` genuinely is `(0,0,0,~1)`
  at runtime, not garbage, not a misread of the real color.
- `gl.CheckError` is live (`DEBUG` is defined for Debug builds) and never caught a GL error at any
  checkpoint.
- Found but doesn't explain it: this driver's GLSL compiler dead-code-eliminates an attribute if its only
  consumer is a compile-time-zero expression (e.g. `a_Color * 0.0`) — crashes `Debug.Assert(colLoc >= 0)`
  → `Environment.FailFast` (visible only via Windows Event Viewer, event ID 1025 — NLog never sees it).
  Doesn't apply to the real shader's unconditional `v_Color = a_Color;` passthrough (`colLoc` was found
  fine there).

### Root cause, confirmed: `CastFrom<From>.To<To>` (`OpenCobra/GDK/Memory/Cast.cs`)

```csharp
public struct CastFrom<From> where From : unmanaged {
  public static To To<To>(From value) where To : unmanaged => Unsafe.As<From, To>(ref value);
}
```

`Unsafe.As` reinterprets an address, it doesn't numerically convert. When `To` is larger than `From` —
exactly the case for every vertex attribute *offset* cast in `Mesh.cs`, `CastFrom<int>.To<nint>(32)` (4
bytes → 8 bytes on x64) — the read goes past the source value's 4-byte storage into whatever 4 bytes of
stack garbage follow it. `CastFrom<int>.To<uint>(...)`, used for attribute *locations*, is same-size and
fine — which matches every diagnostic above showing correct locations while never checking the offset
value itself.

**Confirmed** with an isolated console repro (no GL context, mirroring `Mesh.Upload`'s exact call
sequence — same casts, same branches):

```
offset(0)  = 0             (expect 0  — correct, by luck)
offset(32) = 876173328416  (expect 32 — garbage)
```

`CastFrom<int>.To<nint>(32)` does **not** evaluate to 32. It's deterministic within a run (stable across
repeated calls) but has nothing to do with the intended value — it's whatever 4 bytes of stack memory
happen to sit adjacent to that particular call site's `int` parameter. `offset(0)` happening to come back
as exactly `0` is coincidence (that call site's adjacent stack bytes happen to be zero), which is why
`a_Position` (offset 0) has rendered correctly all along while `a_Color` (offset 32) never has.

This directly explains the black terrain: `glVertexAttribPointer`'s offset parameter for `a_Color` gets a
huge, garbage byte offset — hundreds of gigabytes past the VBO's actual data. The GPU driver reads
out-of-bounds vertex data for every `a_Color` fetch, and (consistent with common driver behavior for
invalid/unmapped attribute fetches) returns `(0, 0, 0, 1)` — exactly the observed color, exactly why the
alpha channel always came back as 1.

**Fix, applied in `OpenCobra/GDK/Memory/Cast.cs`**: rewrote `CastFrom` to do a real numeric conversion via
.NET's generic-math interfaces instead of bit-reinterpretation, constraining both type parameters to
`IBinaryInteger<T>` (satisfied by `int`, `uint`, `nint`, `long`, etc.) and converting with
`To.CreateChecked(value)`:

```csharp
public readonly struct CastFrom<From> where From : IBinaryInteger<From> {
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static To To<To>(From value) where To : IBinaryInteger<To> => To.CreateChecked(value);
}
```

`CreateChecked` throws `OverflowException` if a value genuinely doesn't fit (e.g. negative → unsigned,
out-of-range narrowing) instead of silently corrupting it — matching the file's own "safe cast" intent.
This fixes every existing call site automatically, including `Mesh.cs`'s offset casts — no changes needed
there, since the bug was entirely inside `CastFrom` itself.

Covered by `OpenCobra/Tests/GDK/Memory/CastTests.cs`: widening (`int→nint`, the exact bug — asserts
`CastFrom<int>.To<nint>(32) == 32`), same-size (`int→uint`), narrowing within range, narrowing/negative
out of range (asserts `OverflowException`), and identity casts.

**Verified end-to-end**: rebuilt and launched the app — the terrain now renders the correct grass color,
pixel-sampled as exactly `R=79 G=129 B=14 A=255`, matching `Color.FromArgb(79, 129, 14)` byte-for-byte.

## Carried-over notes

- `.claude/skills/drive-native-app/scripts/AppDriver.ps1` had `GetCurrentThreadId` declared against
  `user32.dll` instead of `kernel32.dll`, intermittently breaking `Screenshot`. Fixed this session.
- `GLExtensions.HookupDebugCallback`'s GL debug callback was never confirmed to actually be registered
  and firing during any of this investigation (it logs at `LogLevel.Debug` and none ever appeared) — not
  load-bearing for this bug, but worth a look if a future GL issue's error logging seems suspiciously
  quiet.

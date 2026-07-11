# Wire OVL Textures into GDK — Complete

## Summary

Unified OVL texture decoding onto a single GDK `Texture` type. Both `tex` (static) and `ftx` (flexi/animated)
sources now flow through one `TextureLoader.LoadTexture` entry point into one GPU-resident asset shape — no
more parallel `AnimatedTexture`/`FlexiTextureList`/three-loader-method split.

## What landed

- **`OpenCobra.OVL.Files.TextureCollection`** ([`TextureDecoding.cs`](../../../OpenCobra/OVL/Files/TextureDecoding.cs))
  is the canonical "named group of decoded frames" model, with an `Fps` member for animated collections.
  `Textures.Extract` and `FlexiTextureList.Load` both return this type — no special-case flexi branch left in
  `Extract`.
- **`OVL.Files.Texture`** gained a `Recolorable` field so flexi frames carry their per-frame recolor flags
  through the same type as static `tex` entries.
- **GDK `Texture`** ([`Texture.cs`](../../../OpenCobra/GDK/Materials/Texture.cs)) is now frame-based:
  `IReadOnlyList<MipChain> Frames` (a static texture has one `MipChain` with its full mip chain; a flexi
  texture has N single-resolution `MipChain`s, one per frame) plus an optional `Animation` record
  (`Fps`/`FrameWidth`/`FrameHeight`/`FrameCount`). `Pixels` stays as a convenience alias for
  `Frames[0].Mips[0]`, so existing callers still compile. `Upload()` pushes every frame's full mip chain via
  per-level `TexImage2D`. `AnimatedTexture` is deleted entirely — this was a hard break across `Model.cs`,
  `Primitives.cs`, `Game.cs`, `Terrain.cs`, `Renderer.cs`, all updated in the same change.
- **`TextureLoader.LoadTexture(string ovlPath, string name)` / `LoadTexture(Ovl ovl, OvlFile file)`** is the
  single public entry point for both `FileType.Texture` and `FileType.FlexibleTexture` sources.
  `LoadFlexiTexture`/`LoadAnimatedTexture` are gone. Internally, a private `ToGl` helper **copies** every mip
  image (`Image.Clone()`) before handing it to the GDK `Texture` — this is the fix for a real
  double-free risk: `Flic.Read`'s `WithName` does a `MemberwiseClone` that shares `Image<Rgba32>` instances
  with the source bitmap-table entry, so without the copy, disposing a GDK `Texture` could dispose an OVL
  `Texture` still in use elsewhere.
- **`FlexiTextureList.Load` split into two layers** (a decision made during implementation, not in the
  original plan): `Load` does relocation resolution only and hands resolved per-frame byte spans to an
  `internal FlexiFrameData` record; a new `internal static TextureCollection Parse(...)` owns the actual
  decode (palette conversion, `Texture` construction, single-vs-multi-frame `"#i"` naming). This exists so
  unit tests can exercise real decode logic via synthetic `FlexiFrameData` without replicating `Ovl`'s
  relocation-table resolution.

## Testing

`OpenCobra/Tests/OVL/TexturesTests.cs` covers: copy-on-conversion ownership (GDK and OVL textures own
independent `Image<Rgba32>` instances; double-`Dispose()` is idempotent), `ToGl` conversion from synthetic
`OVL.Files.Texture` input (mip counts, `Recolorable` plumbing, `MipLevels[0] == null` throws), and — via the
`Parse` split — frame count/`Fps`/per-frame `Recolorable`/naming for the real flexi decode path, plus
`LoadTexture`'s `Frames.Count`/`Animation` fields for a flexi source. `ExtractResources.cs`'s two
`FlexiTextureList.Load` call sites were updated to the `TextureCollection` shape.

## Known non-regression

The `mms`/`prt`/`psi`/`fct` symbol-resolution issue tracked in
[`ovl-resource-relocation.md`](../../bugs/ovl-resource-relocation.md) predates this work and is unaffected —
`LoadTexture` returns whatever pixels the decoder resolves, correct or not, for those tag families. Not a
regression introduced here; still open separately.

## Downstream unblock

This work, combined with the separately-fixed `tex`/`flic`/`btbl` relocation bugs (see
[`ovl-texture-decoding.md`](ovl-texture-decoding.md)), is what
[`grass-texture-from-terrain-ovl.md`](../../plans/grass-texture-from-terrain-ovl.md) now builds on to wire a
real decoded grass texture into the terrain mesh — previously blocked, now just three small follow-up edits.

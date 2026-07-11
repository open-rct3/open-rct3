# Wire OVL Textures into GDK

## Goals

Wire the OVL decoder's `Texture` (decoded mip chain, `Format`, `Style`, `Recolorable`) into the
GPU-resident `OpenCobra.GDK.Materials.Texture`, and collapse the parallel "list of frames"
types — `OVL.Files.FlexiTextureList`, `OVL.Files.AnimatedTexture`, and the three
`TextureLoader` entry points — into one GDK `Texture` that holds every frame in a single
asset.

A GDK `Texture` is a single GPU-resident asset. Non-animated textures hold one frame (its
mip chain). Animated textures hold N frames (each a single-resolution image, since RCT3 flexi
textures are single-resolution). The renderer doesn't iterate frame instances — it reads the
metadata on the single `Texture` and uploads the data in one go.

`OpenCobra.GDK.Assets.TextureLoader` is the only place that knows how to translate OVL
texture data into a GDK `Texture`. It exposes a single `LoadTexture` entry point that works
for both `tex` and `ftx` sources.

## Background

- `GDK.Materials.Texture` is the existing GPU-side contract: `Name`, `Width`, `Height`,
  `Pixels : Image<Rgba32>`, `Handle : uint`, `Upload()`. Callers (renderer, ImGui browser,
  test bench) bind to that contract.
- `Textures.Extract` already does the "one flexi → many `Texture` entries" expansion at
  `OpenCobra/OVL/Files/Textures.cs:104-118`. Unifying onto `TextureCollection` formalizes
  that pattern as the canonical model rather than a special case.
- The OVL `tex`/`flic`/`btbl` paths are not recolorable and pass `Recolorable.None`;
  flexi frames pass the per-frame `recolorable` value. `Flic.cs:53` and `BitmapTable.cs:60`
  call sites stay on the default and need no change.

## Implementation

1. **`OpenCobra/OVL/Files/TextureDecoding.cs`** — extend `TextureCollection`:
   - Add `public uint Fps { get; init; }` (default `0`).
   - Add a public constructor
     `TextureCollection(IEnumerable<Texture> textures, uint fps = 0)` keyed on
     `texture.Name`.
   - Make `Add` / `AddRange` public so the GDK loader can build collections directly.

2. **`OpenCobra/OVL/Files/TextureDecoding.cs`** — extend `OVL.Files.Texture`:
   - Add a primary-ctor param `Recolorable recolorable = Recolorable.None`. Field
     `public readonly Recolorable Recolorable = recolorable;`.
   - Keep `Style` and the existing `Format` / `MipLevels` fields.

3. **`OpenCobra/OVL/Files/FlexiTexture.cs`** — `FlexiTextureList.Load` returns a
   `TextureCollection`:
   - Decode each frame as today (palette, indexed pixels, alpha, `PaletteConverter`).
   - For each frame, build an `OVL.Files.Texture` passing `recolorable` to the new ctor
     argument. Single-frame ftx keeps the symbol's own name; multi-frame gets a `"#i"`
     suffix (same rule as `Textures.Extract`).
   - Return `new TextureCollection(frames, fps)`.
   - Delete the `FlexiTexture` record struct and the `FlexiTextureList` record struct. The
     `FlexiTexture.cs` file is kept for the `FlexiTextureList.Load` entry point or merged
     into `TextureDecoding.cs` — whichever keeps the diff smaller.

4. **`OpenCobra/OVL/Files/Textures.cs`** — `Extract` no longer needs a special flexi
   branch. Replace the `Parallel.ForEach(ftxFiles, ...)` block at `Textures.cs:107-125`
   with:
   ```csharp
   var ftxCollections = ftxFiles
     .AsParallel()
     .Select(file => FlexiTextureList.Load(ovl, file));
   foreach (var collection in ftxCollections)
     foreach (var texture in collection)
       bag.Add(texture);
   ```
   Update the comment at `Textures.cs:103-106` to reflect the new model.

5. **`OpenCobra/OVL/Files/TextureDecoding.cs`** — add
   `public static IReadOnlyDictionary<uint, Texture[]> BuildBitmapTablesByFlicAddress(Ovl ovl)`.
   It implements the `LoaderEntriesInOrder` walk currently inline in
   `Textures.Extract` (`OpenCobra/OVL/Files/Textures.cs:60-78`).

6. **`OpenCobra/OVL/Files/Textures.cs`** — call the helper from step 5 and delete the
   inlined walk. No behavior change for `Extract`.

7. **`OpenCobra/GDK/Materials/Texture.cs`** — replace the `Pixels` shape with a frame
   array of `MipChain` record structs:
   ```csharp
   public record struct MipChain(IReadOnlyList<Image<Rgba32>> Mips);
   ```
   ```csharp
   public IReadOnlyList<MipChain> Frames { get; } = frames;
   ```
   - A static `tex` has one `MipChain` with `Mips = [mip0, mip1, mip2, ...]`.
   - A flexi has N `MipChain`s, one per frame, each with `Mips = [frameN]`
     (single-resolution).
   - Keep `Image<Rgba32> Pixels` as a convenience alias for `Frames[0].Mips[0]` (the
     existing public surface).
   - Add `TextureFormat Format { get; }` (default `A8R8G8B8`).
   - Add `Animation? Animation { get; }` (default `null`):
     ```csharp
     public record struct Animation(
       uint Fps, int FrameWidth, int FrameHeight, int FrameCount
     );
     ```
     `FrameCount == 1` and `Fps == 0` for static. `FrameCount == Frames.Count` for
     animated. The renderer reads `Animation` and `Frames` together — never iterates
     separate `Texture` instances.
   - Keep the existing primary ctor
     `(string name, int width, int height, Image<Rgba32> texture, Recolorable recolorable = 0)`
     so any in-tree callers that build a `Texture` directly still compile. Internally
     it stores `Frames = [new MipChain([texture])]` and `Animation = null`.

8. **`OpenCobra/GDK/Materials/Texture.cs`** — extend `Upload()` to push every frame's
   mip chain:
   - Generate a single `Handle`.
   - For each `MipChain` and each mip within it, call `gl.TexImage2D` with the
     appropriate `level`, `ImageWidth = max(1, W >> level)`, `ImageHeight = max(1, H >> level)`.
     The renderer's job is to map `Frames` + `Animation` onto a GL texture layout
     (sprite-sheet, array texture, etc.); `Upload()` here is the minimal per-mip
     loop that places pixels. Keep the existing `TexParameter` calls.
   - This also retires the `// FIXME: SAFELY upload texture pixels to GPU!` comment.

9. **`OpenCobra/GDK/Materials/Texture.cs`** — `Dispose()` walks every image in every
   `MipChain.Mips` and disposes each. The GDK `Texture` owns the images it was given
   at construction (see step 11 — the conversion copies them), so the naive "dispose
   every owned image" semantics is correct here. `IDisposable.Dispose()` is idempotent
   via the existing `disposed` flag.

10. **`OpenCobra/GDK/Materials/Texture.cs`** — delete `AnimatedTexture` entirely. The
    single-`Texture` shape plus the `Animation` member is the only grouping primitive
    the renderer needs. Steps 11a–11d update the in-tree consumers as part of the
    same change so `OpenCobra.sln` keeps compiling.

11. **`OpenCobra/GDK/Assets/TextureLoader.cs`** — unify into a single `LoadTexture`
    entry point that returns one GDK `Texture` regardless of OVL source, and
    **copy images during conversion** to sidestep the `WithName` shared-`MipLevels`
    double-free risk:
    - Delete `LoadFlexiTexture` and `LoadAnimatedTexture` (collapse into `LoadTexture`).
    - `LoadTexture(string ovlPath, string name)` and `LoadTexture(Ovl ovl, OvlFile file)`:
      - Find the `OvlFile` by `name` (try `FileType.Texture` first, then
        `FileType.FlexibleTexture`; if both resolve, log a warning and keep the first).
      - For `FileType.Texture`: call `TextureDecoding.ReadTexture(...)` (after
        `BuildBitmapTablesByFlicAddress`), then convert via the helper from step 11a.
      - For `FileType.FlexibleTexture`: call `FlexiTextureList.Load`, then convert via
        the helper from step 11a, which also sets the `Animation` member.
    - The OVL→GDK conversion is the only place that knows how to translate an
      `OVL.Files.Texture` into a GDK `Texture`. Callers don't see the conversion;
      `LoadTexture` is the only public surface.

    **11a.** Add a private `ToGl(OVL.Files.Texture, Animation? = null)` helper that
    **copies** the source's mip images into fresh `Image<Rgba32>` instances before
    handing them to the GDK `Texture`. The copy is the mitigation for the `WithName`
    shared-`MipLevels` risk: every GDK `Texture` owns its images outright, and
    disposing the GDK `Texture` never collides with a still-live OVL `Texture`. The
    implementation:
    ```csharp
    private static Texture ToGl(OVL.Files.Texture src, Animation? animation = null) {
      var mips = new Image<Rgba32>[src.MipLevels.Length];
      for (var i = 0; i < mips.Length; i++) {
        var srcMip = src.MipLevels[i]
          ?? throw new InvalidOperationException($"Texture '{src.Name}' has no decoded mip {i}");
        // Image.Clone shares the buffer but is an independent disposable instance.
        mips[i] = srcMip.Clone();
      }
      var frames = new[] { new MipChain(mips) };
      return new Texture(src.Name, src.Width, src.Height, mips[0], src.Recolorable) {
        Format = src.Format,
        Frames = frames,
        Animation = animation,
      };
    }
    ```
    Use `Image.Clone()` (which produces an independent `Image<Rgba32>` over a new
    buffer) rather than `Image.LoadPixelData` (which re-decodes from bytes and costs
    more). For a flexi source, the `LoadTexture` overload iterates the
    `TextureCollection`, calls `ToGl(ovlTex, animation)` per frame where `animation`
    is non-null only for the first frame, and flattens into a single GDK `Texture`
    with `Frames.Count == collection.Count`.

    **11b.** `OpenCobra/GDK/Model.cs:8` and `OpenCobra/GDK/Meshes/Primitives.cs:8`
    import the `OpenCobra.GDK.Materials` namespace. Audit any references to
    `AnimatedTexture` and rewrite to read `Texture.Animation` / `Texture.Frames`
    instead. If a site needs to iterate animation frames, change
    `foreach (var frame in animated.Frames)` to
    `foreach (var frame in texture.Frames)` on the single `Texture`.

    **11c.** `OpenRCT3/Game.cs:13`, `OpenRCT3/Simulation/Terrain.cs:8`,
    `OpenRCT3/OpenGL/Renderer.cs:13` consume `OpenCobra.GDK.Materials` outside
    `OpenCobra`. Same audit + rewrite as 11b. Renderer/Terrain code that handles
    animated textures today is the highest-risk area — the new `Texture.Animation`
    API must cover every call site that previously used `AnimatedTexture.Fps` /
    `AnimatedTexture.Frames`.

    **11d.** `OpenCobra/Tests/TestRunner/OvlTestBenchForm.cs` and the ImGui plugins
    under `OpenCobra/Tests/TestRunner/bin/Debug/net8.0/plugins/*.wasm` may bind to
    `AnimatedTexture`. The test bench is a thin GUI over the asset loaders; the
    plugins are pre-built `wasm` artifacts and likely consume the OVL
    `TextureCollection` (not the GDK `Texture`) already, so they probably need no
    changes. Verify before merging step 11; if a plugin does consume `AnimatedTexture`,
    it must be rebuilt or the plugin is incompatible with the new GDK API.

12. **`OpenCobra/Tests/OVL/TexturesTests.cs`** — add unit tests:
    - **Resource disposal (copy-on-conversion ownership model):**
      - A GDK `Texture` built via `ToGl(ovlTex)` from a single `OVL.Files.Texture`
        with mips `[2×2, 1×1, 1×1]`: `Frames[0].Mips` holds three
        **independent** `Image<Rgba32>` instances (`Image.Clone` semantics),
        not the same instances as `ovlTex.MipLevels`. Disposing the GDK
        `Texture` does not affect the OVL `Texture`, and disposing the OVL
        `Texture` does not affect the GDK `Texture`.
      - A GDK `Texture` built from a 3-frame flexi: each `MipChain[i].Mips[0]`
        is independent of the source `TextureCollection`'s frames. Disposing
        the GDK `Texture` walks all three exactly once; disposing the
        `TextureCollection` does not collide.
      - Calling `Dispose()` twice on the same GDK `Texture` is idempotent (no
        `ObjectDisposedException`).
      - Regression: build a GDK `Texture` from the same OVL `Texture` twice
        (e.g. via `WithName` cloning) and confirm the two GDK `Texture`s
        have **different** `Image<Rgba32>` instances — proves the copy, not
        a reference, took place.
    - **`TextureLoader.ToGl` (no GL context required):**
      - Synthetic `OVL.Files.Texture` with a 2×2 mip 0 and a 1×1 mip 1.
        `Frames.Count == 1`, `Frames[0].Mips.Count == 2`,
        `Recolorable == Recolorable.None`, `Format` preserved.
      - Second synthetic with `Recolorable = Recolorable.First | Recolorable.Second`;
        flags plumb through.
      - Converting from a `Texture` whose `MipLevels[0]` is `null` throws
        `InvalidOperationException`.
    - **`FlexiTextureList.Load` returns a `TextureCollection`:** synthetic ftx header
      → assert expected frame count, `Fps`, per-frame `Recolorable`, naming (single
      keeps name; multi gets `"#i"`).
    - **`LoadTexture` for a flexi source** returns a single GDK `Texture` whose
      `Frames.Count == collection.Count`, `Animation.Fps` / `FrameWidth` /
      `FrameHeight` / `FrameCount` all match the source collection.

    Per `AGENTS.md`, run Unit Tests only; the OVL integration suite is not part of
    this plan's verification.

    **STATUS: partially done.** Everything else in this plan (steps 1-11, 13, and the
    disposal/ownership half of step 12) is implemented and merged into the working
    tree — see `TexturesTests.cs`'s `ToGl_*` fixture for the disposal coverage. The
    two bullets above are still outstanding:
    - `FlexiTextureListSyntheticTests.SingleFrame_KeepsSymbolName` /
      `MultiFrame_GetsIndexSuffix` (added so far) assert the naming *contract* in
      isolation — they don't call `FlexiTextureList.Load` against real bytes. Real
      end-to-end coverage of `Load` (frame count, `Fps`, per-frame `Recolorable`)
      still only exists via the `RCT3_PATH`-gated integration suite
      (`ExtractResources.cs`), which doesn't run in CI/unit-test mode.
    - No test exists yet for `LoadTexture` on a flexi source asserting
      `Frames.Count` / `Animation` fields.
    - Blocker: both require a synthetic FTX byte buffer, which means either (a)
      replicating `Ovl`'s relocation-table resolution (`TryResolveRelocation`) in
      the test fixture, or (b) adding a seam to `FlexiTextureList.Load`/`Ovl` that
      lets a test inject pre-resolved frame data without a full relocation table.

    **DECISION: option (b).** Split `FlexiTextureList.Load(Ovl, OvlFile)` into two
    layers:
    - `Load` keeps doing relocation resolution only (`TryResolveRelocation` calls,
      header parse) and hands off a list of already-resolved per-frame raw byte
      spans (scale/width/height/recolorable plus palette/texture/alpha memory) to
      a new `internal static TextureCollection Parse(string name, uint fps, uint
      width, uint height, IReadOnlyList<FlexiFrameData> frames)`.
    - `Parse` owns the actual decode (`PaletteConverter.ConvertIndexedBgraToRgba`),
      `Texture` construction, and the single-vs-multi-frame naming rule (`"#i"`
      suffix). This is the part step 12's tests care about, and it has zero
      dependency on `Ovl`'s relocation machinery — a test builds `FlexiFrameData`
      directly (raw palette/pixel/alpha byte arrays) and calls `Parse`.
    - `Ovl.TryResolveRelocation` stays exactly as-is and untouched; `Parse` never
      sees a relocation table, so tests exercising it can't drift from or duplicate
      `Ovl`'s resolution logic.
    - Add the two still-missing tests from this section against `Parse`: frame
      count / `Fps` / per-frame `Recolorable` / naming for `Load`'s real decode
      path (via `Parse`), and a flexi-source `LoadTexture` test asserting
      `Frames.Count` / `Animation` fields (via `TextureLoader.ToGl` composed with
      a `Parse`-built `TextureCollection`).

13. **`OpenCobra/Tests/Integration/ExtractResources.cs`** — update the two
    `FlexiTextureList.Load` call sites (`ExtractResources.cs:62` and `:143`) to use
    `TextureCollection`. Re-target assertions on `Width`/`Height`/`Length` to
    `collection.Count` and the per-frame `Width`/`Height` from the first `Texture`.

## Out of Scope

- Full mip-aware `Upload()` semantics: `GL_TEXTURE_MIN_FILTER` mode selection, when to
  call `gl.GenerateMipmap` versus per-level `TexImage2D`, sampler selection. The
  upload extension in step 8 is minimal — per-level `TexImage2D` plus the existing
  `TexParameter` calls. Renderer-side mip selection and any texture-style (TXS)
  plumbing remain future plans.
- The actual GPU layout chosen for animation (sprite-sheet vs. array texture vs.
  texture atlas). The `Animation` record struct and the per-frame `MipChain` list
  carry the metadata; choosing and uploading the layout is a renderer-side concern.
- Animated `tex` symbols. RCT3 does not produce these; animation is a flexi-texture
  concept.
- Any deeper rewrite of `Textures.Extract` or `Ovl.LoaderEntriesInOrder` beyond the
  helper extraction in step 6.
- BitmapTable-backed mip0-only handling in `Textures.Extract` (no behavior change there).

## Verification

- Run `make test` (Unit Tests per `AGENTS.md`).
- Confirm the new `TexturesTests` cases pass and the existing `TexturesTests` suite
  still passes — the decoder output and `Textures.Extract` behavior must not change.
- Run `dotnet build OpenCobra.sln` and verify no new warnings on the touched files.

## Complexity & Risk

| Severity | Risk | Mitigation |
|----------|------|------------|
| Mitigated | **Shared `MipLevels` via `WithName`:** `Flic.Read` at `Flic.cs:32-33` returns `table[index].WithName(name)`, a `MemberwiseClone` that shares `Image<Rgba32>` instances with the table entry. `Texture.Dispose()` (step 9) would double-free them. | Step 11a: `ToGl` calls `Image.Clone()` on every mip. The GDK side owns its copies. The unit test in step 12 asserts two GDK `Textures` from the same OVL `Texture` have different `Image<Rgba32>` instances. |
| Mitigated | **`AnimatedTexture` removal is a hard break** for `Model.cs:8`, `Primitives.cs:8`, `Game.cs:13`, `Terrain.cs:8`, `Renderer.cs:13`, and the pre-built ImGui plugins. | Steps 11b–11d update the in-tree consumers as part of the same diff. Pre-built `*.wasm` plugins that bind to `AnimatedTexture` are flagged as needing a rebuild. |
| Low | **`LoadTexture` name collision** when a symbol exists under both `FileType.Texture` and `FileType.FlexibleTexture`. | Try `Texture` first, then `FlexibleTexture`, and log a warning when both resolve. Real archives don't collide. |
| Low | **Existing `mms`/`prt`/`psi` decoding bug** documented at `.agents/plans/fix/ovl-texture-decoding.md` affects real archives. The new `LoadTexture` path returns the same wrong pixels. Not a regression. | Scope the new unit tests in step 12 to synthetic input, not real OVL fixtures. The integration test surface in step 13 is gated by `RCT3_PATH`. |
| Low | **Flexi source always uses `TextureFormat.A8R8G8B8`** (hard-coded at `Textures.cs:115`). `ToGl(TextureCollection)` propagates that field unchanged. | No code change. One-line comment in `ToGl` noting the inherited format. |
| Low | **`Upload()` extension** adds per-mip `gl.TexImage2D` calls. | Additive. Today's flexi path (single mip) keeps the same GL call sequence. |
| Low | **Integration-test rewrite** at `ExtractResources.cs:62,143`: assertions on `flexiTexture[0].Texture` re-target to `collection.First().MipLevels[0]`. | Step 13 covers the rewrite; running the integration tests once is the smoke check. |
| Low | **`OVL.Files.Texture.Recolorable` field placement** as a primary-ctor param. | `Flic.cs:53` and `BitmapTable.cs:60` keep working unchanged (default `Recolorable.None`). `Textures.cs:115` is deleted by step 4, so no manual update. |
| Low | **Project reference direction:** `GDK` → `OVL` is one-way. | The plan introduces no cycle. All OVL→GDK coupling goes through existing `using OpenCobra.OVL;` imports. No `csproj` change. |

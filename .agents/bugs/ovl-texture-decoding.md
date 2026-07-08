# Bug: Texture (`tex`/`flic`/`btbl`) decoding — crash fixed, symbol-resolution issue remains open

## Status: Partially fixed — decode pipeline fixed and hardened; `mms`/`prt`/`psi`/`fct` symbol resolution still open

Two passes over the same subsystem (`OpenCobra/OVL/Files/Textures.cs`, `OpenCobra/OVL/OVL.cs`).
The first fixed a hard crash and got real textures decoding; the second investigated the
remaining graceful failures the first pass left behind and found they're one still-open
symbol-resolution bug, not the several small issues originally guessed at.

## Part 1 — `Textures.Extract` decoded 0 textures and crashed on real RCT3 data (Fixed)

The flic decoder pipeline produced 0 successful texture decodes and aborted on the first
`Debug.Assert` failure when run against the real RCT3 install. Root-caused and fixed by
cross-referencing the reference C++ implementation
(`C:\Users\enigm\Applications\RCT3\Dumper\rct3tex.cpp`, Jonathan Wilson's `rct3tex` dumper —
the working flic decoder). Three concrete fixes:

1. **The mip-header loop trusted `header.MipCount` instead of the data itself
   (`Textures.cs:197-213`).** The reference (`rct3tex.cpp:502-532`) pre-reads the first
   `FlicMipHeader`, then runs a `while (width && height && mh.Pitch && mh.Blocks)` loop
   that re-reads a fresh header at the tail of each iteration and only increments
   `level` when the mip's `MWidth`/`MHeight` match the expected downsampled size
   (`max(1, header.W/H >> level)`). Mismatched headers are *skipped*, not fatal.
   OpenCobra was doing `for (i = 0; i < header.MipCount; i++)` and asserting the
   per-mip dimensions matched — so any flic whose on-disk mip count didn't line up
   with `log2(width)+1` (very common: `MipCount = 0` means "compute", `MipCount = 9`
   is the explicit 256² case) tripped a `Debug.Assert` and threw, killing the
   parallel worker. Replaced with the reference's pre-read + while loop. The
   `ComputeMipCount` helper sizes the `Texture.MipLevels` array to `log2(width)+1`,
   so mips left over by skipped headers become null slots in the array.

2. **`ReadBitmapTable` used a single base-mip size for every mip level
   (`Textures.cs:243`).** Reference (`rct3tex.cpp:540-592`, `ReadTextures`) sizes each
   mip independently: `max(1, h>>i) * max(1, w>>i) * num`, with `w/h /= 4` for DXT
   (to convert pixel counts to 4×4 block counts) and `num = bpp/8` (or 8/16 for the
   DXT block sizes). OpenCobra was reading `flic.Width * flic.Height * bpp/8` for
   every mip, which produced either truncated reads or `Image.Load<Rgba32>` failures
   on the second and later mips. Fixed to match.

3. **`ToImage` only decoded A8R8G8B8.** 40 real DXT1/DXT3 textures failed with
   `Unsupported texture format: Dxt1/Dxt3`. Added a `DxtDecoder` (`Textures.cs:427-562`)
   implementing the standard S3TC block decoders (4×4 blocks, 8 bytes for DXT1, 16
   for DXT3/5, with the proper 4-color DXT1 palette selection and the DXT5 8-alpha
   interpolation table). `ToImage` now takes `width`/`height` and dispatches by
   format. `ReadFlic` threads the expected per-mip dimensions through so DXT can
   compute its block grid.

Two more real bugs surfaced and were fixed in the same pass:

- **`Texture.Dispose` (`Textures.cs:41`) threw `NullReferenceException` on partially
  decoded textures** (any flic where some mips were skipped by the new loop left
  null entries in `MipLevels`). Switched the foreach to `mipLevel?.Dispose()`.
- **`Extract`'s catch blocks (`Textures.cs:98, 118`) were bare `catch { ... }` with
  no exception capture**, so the post-mortem log only showed the file name. Changed
  to `catch (Exception ex)` and pass `ex` to the NLog call so the log file has
  full stack traces.

### Verification

`Textures.Extract` against the full real RCT3 install (`*.common.ovl` only, 7,490
archives), 4.9s wall-clock, no archive-loading crashes:

| Metric                     | Before             | After |
|----------------------------|--------------------|-------|
| Hard archive failures      | 1 (assert dialog)  | 0     |
| Textures decoded           | 1 (then crash)     | 51    |
| Mip levels decoded         | 0                  | 244   |
| Unit tests (with RCT3_PATH)| passing            | passing (14,276 passed, 0 failed) |

`make test` is the canonical Unit Tests entry point and continues to pass.

### Remaining failures identified at this point (all graceful, no crash)

Categorised by error signature from `run.log` at the time:

1. **`Failed to resolve flic data` (~161 entries).** `Tex.Ts2Ptr` resolves, but
   the chain to a flic extra-data chunk doesn't exist for many texture entries —
   texture entries in BTBL-backed archives use a 4-byte BTBL index rather than the
   `Tex → TextureStruct2 → Flic` chain.
2. **`Failed to resolve TextureStruct2` (~50).** Same as #1, the chain breaks
   further upstream.
3. **`Failed to resolve bitmap table data` (~25).** BTBLs with only 1 extra-data
   chunk (header only, no pixel-data chunk).
4. **`references a bitmap table that failed to decode` (~80).** Cascading from #3.
5. **`Image cannot be loaded. Available decoders: ...` (4 entries).** BTBL pixel
   data read with `Image.Load<Rgba32>(data)`, which tries to auto-detect PNG/JPEG/etc.
   from raw bytes instead of using `Image.LoadPixelData<Rgba32>`.
6. **3 `ArgumentOutOfRangeException`** and **3 `BitConverter.ToInt32` errors** —
   small handful, likely individual resource-classification or boundary bugs.

(Categories 1-6 above were the *guessed* root causes going into part 2. Part 2 found
the actual causes were different from — and fewer than — this list.)

## Part 2 — Investigating the remaining failures (Open)

A follow-up pass (`.agents/summaries/flic-decode-gaps.md`) reinvestigated categories
1-6 above against real RCT3 data. Some were confirmed and fixed as guessed; two
(#1/#3) turned out to be a single, different, still-open bug.

### Fixed in this pass

- **#5 (image-format auto-detect) and the same bug in `ReadFlic`'s shared
  `DecodeA8R8G8B8` path (not previously noticed).** Both `Image.Load` call sites
  replaced with `Image.LoadPixelData` using explicit dimensions.
- **`ReadBitmapTable`'s `num = BitsPerPixel()/8` truncates to 0 for DXT1** (4 bits/pixel
  integer-divided by 8), zeroing every compressed BTBL mip read. Fixed to use
  `BlockSize()` for compressed formats.
- **`TryResolveRelocation` didn't guard against a null (`0`) data pointer**, unlike its
  sibling `TryResolveString` — a null pointer spuriously "resolved" to whatever block
  starts at `RelativeOffset` 0. Fixed to match.
- **#1/#2 reframed, not "fixed" as originally scoped:** cross-referencing the reference's
  `TextureLoader`/`FlicStruct`/`TextureStruct2` definitions showed that `Tex.FlicPtr`
  (offset 52) and `Tex.Ts2Ptr` (offset 56) are *only* ever populated by the relocation-
  fixup table (`rct3tex.cpp:1827-1842`, `DoReloc`) at load time — a table `OVL.cs`'s
  `SkipRelocations` never parses. For entries like `TerrainGrid2StageDummy`,
  `GUIRendererBitmap` (runtime-generated render targets, LOD placeholders), these
  fields are genuinely unpatched placeholder bytes on disk — there is no pixel data to
  recover, matching the reference's own `else { tex->Flic = 0; }` no-op branch.
  `ReadTexture` now returns `null` (silently skipped, not logged as a failure) when
  this chain doesn't resolve, instead of throwing. Extraction held steady at the same
  successful-texture count (no regression) while categories #1 (161) and #2 (33)
  disappeared entirely from the failure log.
- Result: 51 → 55 textures decoded, with the bulk of the remaining ~810 log entries
  reclassified into one coherent bug (below) instead of six scattered guesses.

### Still open — `mms`/`prt`/`psi`/`fct`-tagged symbols resolve to the wrong (undersized) block

This is the real shape of what was guessed as #1/#3 above (and, transitively, #4/#6).
Follow-up to [`ovl-resource-relocation.md`](./ovl-resource-relocation.md): that doc fixed
symbol-resolution for `svd`/`ftx` but left this tag family's version of the same bug open.

**Evidence.** `Characters\AF\AF01_Body_Main.common.ovl` has a `BitmapTable` symbol
`SkinBody_AF01_L2:mms` whose `BmpTbl` header claims `count = 192` (192 texture slots —
needs at least `8 + 192*16 = 3080` bytes just for the `FlicHeader` array, before any
pixel data). `Ovl.ReadResource` for this symbol returns only **40 bytes**, and
`Ovl.TryReadExtraData` finds **zero** extra-data chunks for it — far too small to be
genuine; the resolved block is not the real `SkinBody_AF01_L2:mms` data.

A second, more obviously-wrong example in the same install: `Main.common.ovl` has a
symbol `GUIFontSmallNumbers:fct`, previously classified as `FileType.BitmapTable`, whose
"resource" bytes actually decode as a Win32 `LOGFONT`-shaped structure containing
readable ASCII font-family strings (`"Tahoma"`, `"Verdana"`, `"Arial"`, `"Lucida"`, ...) —
not bitmap-table data at all.

**Root cause.** `Ovl.ExtractResources()` (`OpenCobra/OVL/OVL.cs:447-529`) assigns
`FileType` two ways:

1. Primary: split the symbol name on `:` and map the suffix via `ToFileType()`.
2. Fallback (`OVL.cs:506-511`): when (1) yields `Unknown`, use
   `loaderHeaders[loaderIdx].Tag`, where `loaderIdx` is walked forward every time a
   per-loader `SymbolCount` countdown hits zero — a purely *positional*, order-dependent
   scheme (this is exactly "Suspect 3" from `ovl-resource-relocation.md`, previously
   confirmed to desync silently and cascade corruption through the rest of a file's
   symbol table once it desyncs).

At the time this was found, `"mms"`/`"prt"`/`"psi"`/`"fct"` were not in `ToFileType`'s
map at all, so **every** symbol carrying one of these tags fell through to the
positional walk. For `mms`/`prt` (character skin/texture archives) this happened to
produce plausible-looking `Texture`/`Flic`/`BitmapTable` classifications most of the
time (confirmed via `ovl.Keys` dumps — e.g. `AF01_Body_Main.common.ovl` correctly listed
`Texture Body_AF01_L1:mms`, `Flic SkinBody_AF01_L3:mms`, `BitmapTable
SkinBody_AF01_L2:mms`), but the *data pointer* resolution for at least some of these
symbols was wrong (the 40-byte/count-192 mismatch above), and for `fct`/`psi` the
classification itself was sometimes wrong too. Both symptoms point at the same walk
desyncing partway through these files' symbol tables — consistent with
`ovl-resource-relocation.md`'s Suspect 3, whose fix (deterministic symbol stride by
version) did not, in practice, prevent the *loader-position* countdown itself from
desyncing for this tag family.

A narrow fix was attempted (only use the positional fallback when the name has no
`:tag` suffix at all, since an unmapped-but-present suffix is positive evidence of a
countdown desync rather than a missing-suffix v1/v4 archive) and reverted: `mms`/`prt`
symbols *needed* the positional fallback at the time (no suffix mapping existed for
them either), so gating on "suffix present" would have silently dropped the entire
character skin/texture system from `ovl.Keys` — a much larger regression than the bug
it would fix.

**Since then:** `"mms"`, `"prt"`, `"psi"`, and `"fct"` have been added to the `FileType`
enum (`CharacterSkinSet`, `CharacterSkinPart`, `ParticleSpriteItem`,
`FontCharacterTable`) and to `FileTypeExtensions.ToFileType`/`ToTagString` in
`OpenCobra/OVL/Files/FileTypes.cs`, so these symbols now resolve via the reliable
name-suffix path instead of the positional fallback (matching how `svd`/`tex`/etc.
already did). **This has not yet been verified against real data or reconciled with
`Textures.Extract`**, which still filters specifically on `FileType.Texture` /
`FileType.Flic` / `FileType.BitmapTable` — since `mms`/`prt`-tagged symbols will now
classify as `CharacterSkinSet`/`CharacterSkinPart` instead of whatever
`Texture`/`Flic`/`BitmapTable` the old positional fallback happened to guess, this may
need `Textures.Extract` (or a new decoder) updated to also look for the new types,
and the underlying desync itself is still unconfirmed as fixed rather than just
no-longer-triggered by these specific tags falling through to it.

**Impact** (full-install extraction run, prior to the `FileType` enum additions):

- 702 tex entries fail with "references a bitmap table that failed to decode" (cascade
  from the ~25 BTBLs below failing to decode at all)
- 47 named `.flic` symbols fail with "Failed to resolve flic data"
- 26 fail with `ArgumentOutOfRangeException` decoding a BTBL index (`BitConverter.ToUInt32`
  on a chunk that isn't really 4 bytes of index data — same root cause, different
  manifestation)
- 25 BTBL symbols fail with "Failed to resolve bitmap table data" (`chunks.Count < 2`,
  or, as shown above, resolve to plausible-looking but undersized/wrong data)

All four buckets are believed to be one bug, not four.

**Suggested next steps:**

1. Re-run the full-install `Textures.Extract` pass now that `mms`/`prt`/`psi`/`fct` map
   to real `FileType`s, to see whether classification alone changed the failure shape
   (it will very likely need `Textures.Extract`/a new decoder path to actually handle
   `CharacterSkinSet`/`CharacterSkinPart`/`ParticleSpriteItem`/`FontCharacterTable`,
   since nothing consumes them yet).
2. Cross-reference the *exact* on-disk `SymbolStruct`/`SymbolStruct2` and loader-header
   layout against `libOVLng`/`libOVL` (`D:\Users\enigm\GitHub\rct3-importer`) specifically
   for `mms`/`prt`/`psi`/`fct`-tagged archives — these may use a genuinely different
   loader/tag scheme (e.g. a per-character-part loader group) that the current
   `loaderHeaders`/`SymbolCount` walk wasn't modeled against, which would explain the
   40-byte/count-192 mismatch even once classification is no longer guessed.
3. Once confirmed, re-run the full-install `Textures.Extract` pass as a regression
   check; expect the 702/47/26/25 buckets above to collapse together once fixed.

**How this was found.** While implementing `.agents/summaries/flic-decode-gaps.md`'s
priority list, gap #3 ("BTBL archives with only 1 extra-data chunk") didn't hold up: the
`count` field decoded from the resource bytes (192) was wildly inconsistent with the
40-byte resource size actually returned, which is a resolution bug, not a chunk-framing
quirk. Cross-referencing `rct3tex.cpp`'s `TextureLoader`/`FlicStruct`/`TextureStruct2`
definitions also disproved gap #1's proposed fix (reading a "DataRelocation" field from
the flic loader's resolved block) — that field (`FlicStruct.Texture`) is documented in
the reference as "always 0 on disk," and empirically, for the affected tex entries,
*both* `Tex.FlicPtr` (offset 52) and `Tex.Ts2Ptr` (offset 56) are placeholder/garbage on
disk, not just one of them — because both are only ever populated by the relocation-
fixup table at load time, which `OVL.cs`'s `SkipRelocations` never parses. That part
(fixup-table-only pointers) is expected/benign for genuinely textureless tex entries
(render targets, LOD placeholders) and is now handled as a graceful no-op rather than a
logged failure; the `mms`/`prt`/`psi`/`fct` symbol-resolution bug documented above is
the separate, still-open issue.

## Key references

- [`rct3tex.cpp::ReadTexture`](https://github.com/chances/rct3-importer) —
  reference standalone-flic decoder (mip while-loop, level/width math).
- [`rct3tex.cpp::ReadTextures`](https://github.com/chances/rct3-importer) —
  reference BTBL mip decoder (per-mip block-counted sizes).
- [`rct3tex.cpp::TextureLoader`/`FlicStruct`/`TextureStruct2`/`DoReloc`](https://github.com/chances/rct3-importer) —
  reference tex→flic pointer chain and the relocation-fixup table `OVL.cs` doesn't parse.
- `OpenCobra/OVL/Files/Textures.cs` — current state.
- `OpenCobra/OVL/OVL.cs:158-180` — `TryReadExtraData` (the bitmap-table index / flic
  chunk lookup).
- `OpenCobra/OVL/OVL.cs:447-529` — `ExtractResources` (symbol → `FileType` resolution,
  including the positional loader-walk fallback at the center of the open issue).
- `OpenCobra/OVL/Files/FileTypes.cs` — `FileType` enum and `ToFileType`/`ToTagString`
  conversions, now including `CharacterSkinSet`/`CharacterSkinPart`/`ParticleSpriteItem`/
  `FontCharacterTable`.
- [`ovl-resource-relocation.md`](./ovl-resource-relocation.md) — the earlier, related
  symbol-resolution bug (fixed for `svd`/`ftx`; this doc's open issue is the same bug
  for a different tag family).

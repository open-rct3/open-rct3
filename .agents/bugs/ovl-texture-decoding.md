# Bug: Texture (`tex`/`flic`/`btbl`) decoding — crash fixed, symbol-resolution issue remains open

## Status: Partially fixed — decode pipeline fixed and hardened; two structural root causes identified for the remaining `tex`/`flic`/`btbl` failures, not yet fixed. Scope corrected: `mms`/`prt` (Part 5) are not texture data and were never part of this bug; real remaining scope is `fct`-adjacent `tex` failures and unverified `psi`. Exact list of affected real-game files known (Part 4) — use it instead of a full-install rescan.

Five passes over the same subsystem (`OpenCobra/OVL/Files/Textures.cs`, `OpenCobra/OVL/OVL.cs`,
plus `CharacterSkins.cs`/`ParticleEffects.cs`). The first fixed a hard crash and got real
textures decoding; the second investigated the remaining graceful failures the first pass
left behind and found they're one still-open symbol-resolution bug, not the several small
issues originally guessed at; the third cross-referenced the reference dumper's loader-
*iteration* code directly (Part 1/2 had only cross-referenced its struct *definitions*) and
found the "still open" bug is actually two distinct, pre-existing structural gaps in
`OVL.cs` — unrelated to the `mms`/`prt`/`psi`/`fct` `FileType` enum work Part 2 ended on;
the fourth confirmed both root causes still reproduce against the real install and pinned
down the *exact* 37 files (of 7,490) that exercise them; the fifth found that group 1 of
those 37 (`mms`/`prt`, i.e. character/animal skins) was a false lead — that tag family
isn't texture data at all (loader categories `MorphMesh`/`PeoplePart`), so **the real
in-scope repro target is `Main.common.ovl`** (font-table-adjacent `tex` failures, Part 4
group 2), not the character-skin files. See Part 5.

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

## Part 3 — Root-caused: two structural gaps in `OVL.cs`, not a classification issue (Open)

A third pass cross-referenced the reference dumper's actual loader-iteration code
(`C:\Users\enigm\Applications\RCT3\Dumper\rct3tex.cpp:1878-2008`, the `LoaderStruct` loop
in the same function Part 1/2 already cross-referenced for its struct layouts) instead of
just its struct definitions, plus `assets/reference/ovl/{parser.rs,reader.rs}` (a
semi-working Rust reimplementation kept in this repo for cross-reference, which makes the
same assumption as the C# code — see Root cause A). Both gaps below predate the `FileType`
enum additions and are independent of them — they corrupt data for *any* archive whose
loader table contains a `btbl`/`txt`/`gsi` loader followed by other loaders, which is
exactly the shape of `mms`/`prt`-tagged character archives (the doc's own example,
`AF01_Body_Main.common.ovl`, mixes an `mms`-tagged bitmap table with other symbols in the
same file).

### Root cause A — `ReadLoaderExtraData` assumes a uniform chunk format that doesn't exist

`Ovl.ReadLoaderExtraData` (`OpenCobra/OVL/OVL.cs:298-327`) treats the "extra data" stream
that follows the relocation-fixup table as one uniform format for every loader: read
`HasExtraData` as a count, then loop reading `(u32 length, bytes)`-prefixed chunks. The
Rust reference's `read_extra_data` (`assets/reference/ovl/reader.rs:257-279`) makes the
same assumption.

The actual reference dumper's loader loop (`rct3tex.cpp:1878-2008`) shows this section's
layout is **hardcoded per loader type**, and `HasExtraData` isn't even used consistently:

- `btbl` (`rct3tex.cpp:1891-1913`): unconditionally reads `u32, u32, u32`, then
  `BmpTbl.count × sizeof(FlicHeader)` — sized from the loader's *own, already-resolved*
  `count` field, not a stream-encoded length — then another `u32`, then per-mip pixel data
  via `FlicLoader2`. `HasExtraData` is never checked at all for this type.
- `txt` / `gsi` (`rct3tex.cpp:1914-1943`): only read anything `if (l.HasExtraData == 1)` —
  a boolean flag, not a count — and then read a fixed 8 bytes, never a length-prefixed blob.
- `flic` / `tex` / `ftx` (`rct3tex.cpp:1944-1972`): each has its own distinct shape again.

Since `btbl` is guaranteed to hit the mismatch, the live file-stream cursor desyncs the
moment a `btbl` (or `txt`/`gsi`) loader is parsed by the current generic reader, and every
loader parsed afterward in that same archive then reads garbage `chunkSize`/bytes from
whatever position the cursor landed on. This matches the observed symptoms exactly:
corruption clusters by *file*, not by symbol, and an unrelated symbol
(`GUIFontSmallNumbers:fct`) decodes as a Win32 `LOGFONT` — it's reading bytes belonging to
whichever loader actually follows the mis-parsed `btbl`/`txt`/`gsi` entry, not its own data.

### Root cause B — the discarded relocation-fixup table also patches the loader table itself

`Ovl.SkipRelocations` (`OpenCobra/OVL/OVL.cs:447-453`) reads the relocation-fixup table's
count and unconditionally seeks past it, discarding every fixup pair. Part 2 above found
this breaks `Tex.FlicPtr`/`Tex.Ts2Ptr` specifically. The reference's `DoReloc` fixup loop
(`rct3tex.cpp:1830-1842`) runs *before* the loader loop and is not scoped to `Tex` at all —
it patches whatever pointer-typed fields the compiler emitted relocations for, which
includes `LoaderStruct.data` itself: `rct3tex.cpp:1893` dereferences it directly as
`(BmpTbl *)l.data`, which only works if `l.data` was already fixed up from a raw
placeholder into a real pointer. So `loaderBlock`'s `data` field
(`OpenCobra/OVL/OVL.cs:310`) — used as the extra-data dictionary key in
`ReadLoaderExtraData`, and implicitly relied on wherever a loader's data pointer is treated
as directly resolvable — is very likely a fixup-table-only pointer on raw disk, the same
category of bug as the `Tex` fields, just never checked for the loader table.

`ExtractResources`'s per-symbol `dataPtr` resolution (`OpenCobra/OVL/OVL.cs:513`) still has
no origin/type check on the resolved block either (this is "Suspect 4" from
`ovl-resource-relocation.md`, never actually fixed — only Suspect 1's stride-guessing was).
A raw, unfixed-up placeholder pointer is essentially an arbitrary 32-bit value, so it can
easily coincidentally fall inside some unrelated block's byte range — producing exactly
the kind of implausible small read documented above (40 bytes returned for
`SkinBody_AF01_L2:mms` against a BmpTbl claiming `count = 192`, i.e. needing ≥3080 bytes).

### Relationship to the `mms`/`prt`/`psi`/`fct` `FileType` enum work

These two root causes are independent of, and predate, the enum additions described at
the end of Part 2. The `FileType` enum/`ToFileType` changes fixed *classification* (a
symbol now reports as `CharacterSkinSet` instead of a positional-fallback guess), but
classification was never the source of the 40-byte/zero-extra-data symptom — that comes
from data-pointer *resolution*, which routes through the two gaps above regardless of
what `FileType` the symbol ends up labeled. The "genuinely different loader/tag scheme"
theory in Part 2's suggested next step #2 is superseded by this: no different scheme is
needed to explain the symptoms — the existing scheme's extra-data parsing and fixup-table
handling are demonstrably wrong for any archive that exercises them, and `mms`/`prt`-tagged
archives reliably do (per the mixed-symbol example above).

### Suggested next steps (supersedes Part 2's)

1. Rewrite `ReadLoaderExtraData` to dispatch per `LoaderHeader.Loader` (the loader
   category string, already captured but currently unused for this), matching each known
   type's exact layout from `rct3tex.cpp` (`btbl`, `txt`, `gsi`, `flic`, `tex`, `ftx` at
   minimum); default to reading zero bytes for unrecognized types rather than guessing, to
   avoid reintroducing a desync for types not yet reverse-engineered.
2. Parse the relocation-fixup table in `SkipRelocations` (rename it once it stops
   skipping) instead of discarding it, mirroring `DoReloc`'s two-level chase
   (`rct3tex.cpp:1832-1841`), and apply it to `LoaderStruct.data` before it's used as an
   extra-data key or dereferenced as a struct pointer anywhere.
3. Add an origin/plausibility check when resolving a `dataPtr` in `ExtractResources`
   (long-standing "Suspect 4" from `ovl-resource-relocation.md`) so a fixup-table-only
   pointer that hasn't been patched yet fails resolution loudly instead of silently
   matching an unrelated block by coincidence.
4. Verify against the 37-file list in Part 4 (not a full-install rescan — too slow for a
   fix loop) after 1-3; expect the BTBL/flic/tex failure buckets to collapse together,
   consistent with them being one bug family. Re-run the full 7,490-file scan only once,
   as the final regression check.

**How this was found.** Re-reading the reference dumper past the point Part 1/2 had
already cross-referenced (the struct *definitions*) into the actual loader *iteration*
code (`rct3tex.cpp:1878-2008`) showed each loader type has its own hardcoded extra-data
shape, which the current generic `HasExtraData`-as-chunk-count reader cannot match.
Checking `LoaderStruct.data`'s use (`rct3tex.cpp:1893`, a direct pointer dereference)
against `SkipRelocations`'s unconditional discard showed the same "unfixed-up placeholder
pointer" category of bug identified for `Tex.FlicPtr`/`Ts2Ptr` in Part 2 also applies to
the loader table, not just to `Tex`.

## Part 4 — Full-install verification: the exact 37 files affected, out of 7,490 (Open)

A fourth pass confirmed Part 3's two root causes still reproduce against the real install
under current code, and — since a full scan takes ~5 minutes, too slow to re-run on every
iteration of a fix — narrowed the *entire* install down to the specific files worth
targeting directly.

**Also discovered in this pass:** `OpenCobra/OVL/Files/CharacterSkins.cs` and
`OpenCobra/OVL/Files/ParticleEffects.cs` already exist, decoding `mms`/`prt`
(`CharacterSkinSet`/`CharacterSkinPart`) and `psi` (`ParticleSpriteItem`) respectively by
detecting each symbol's on-disk shape (tex/flic/btbl) at decode time rather than trusting
its `FileType`. This is Part 2's suggested-next-step #1 (`Textures.Extract`/a new decoder
needing to handle the new `FileType`s), already done in some prior session not reflected
in this doc until now. Neither module is wired into anything that calls it yet outside of
tests, and both modules' own doc comments already note the underlying bug is open and
expect most real archives to fail to decode until it's fixed. `fct` (`FontCharacterTable`)
has no decoder — correctly, since it's a font/glyph table, not texture data — but a
`fct`-tagged loader sitting in the same file as `tex`/`flic`/`btbl` loaders is still
Root cause A bait (below), so it's tracked below alongside the other three tags.

**Method.** Loaded every `*.common.ovl` in a real install (`RCT3_PATH`), and for each file
containing `Texture`/`Flic`/`BitmapTable` and/or `CharacterSkinSet`/`CharacterSkinPart`/
`ParticleSpriteItem`/`FontCharacterTable` entries, ran the matching decoder(s)
(`Textures.Extract`, `CharacterSkins.Extract`, `ParticleEffects.Extract`) with an NLog
memory target capturing failure output, and flagged the file if either something logged a
failure, or a skin/particle/font tag co-occurred with a tex/flic/btbl entry in the same
file (Root cause A's trigger condition) even without a logged failure — since both root
causes fail *silently* now (`ReadTexture`/`ReadBitmapTable` return `null`/throw internally
and get swallowed), a wrong-but-plausible decode wouldn't necessarily log anything.

**Result: 7,490 scanned, 0 load crashes (Part 1's fix holds), 37 files flagged.** No file
in the flagged set produced a single logged exception/failure — every failure is silent,
consistent with Part 2's "graceful no-op" fix. The 37 split cleanly into three groups:

1. **Character/animal skin archives — 28 files, 100% failure rate.** Every one decodes
   **zero** `mms`/`prt` textures despite having real entries (3–8 each per file):
   - `Characters\AF\AF01_Body_Main.common.ovl`, `AF01_Legs_Main`, `AF06_Body_Main`,
     `AF06_Legs_Main`
   - `Characters\AM\AM01_Body_Main`, `AM01_Legs_Main`, `AM05_Body_Main`
   - `Characters\CF\CF04_Body_Main`, `CF04_Legs_Main`, `CF05_Legs_Main`, `CF06_Body_Main`
   - `Characters\CM\CM01_Body_Main`, `CM01_Legs_Main`, `CM05_Body_Main`, `CM05_Legs_Main`
   - `Characters\TF\TF02_Body_Main`, `TF08_Body_Main`, `TF08_Legs_Main`
   - `Characters\TM\TM01_Body_Main`, `TM01_Legs_Main`, `TM05_Body_Main`, `TM05_Legs_Main`
   - `Characters\Entertainers\Alien\Alien01_Body_Main`
   - `Animals\Duck\Duck.common.ovl`, `Animals\Fish\Mackeral.common.ovl`,
     `Animals\Ray\Ray.common.ovl`, `Animals\Seagull\Seagull.common.ovl`,
     `Animals\Shark\Shark.common.ovl`
   - `Characters\AF\AF01_Body_Main.common.ovl` is the exact file the original evidence
     above (`SkinBody_AF01_L2:mms`, count-192 BTBL → 40 bytes) came from — confirmed
     still broken today, unchanged by the `FileType` enum work or the new decoder modules.
2. **Font-table-adjacent files — 8 files, mostly-to-total failure on their *ordinary* `tex`
   entries.** Presence of `fct` loaders correlates with plain `tex` entries in the *same
   file* also failing — exactly Root cause A's predicted blast radius (a mis-parsed `fct`
   loader desyncs the cursor for every loader after it, corrupting unrelated siblings):
   - `Main.common.ovl` — the standout case: 84 `Texture` entries + 6 `FontCharacterTable`,
     **0 of 84 decoded**. This is also the file `GUIFontSmallNumbers:fct`'s LOGFONT-shaped
     misread came from in Part 2.
   - `gui\800\Resolution.common.ovl` (0/5), `gui\800\SChinese.common.ovl` (1/4),
     `gui\1024\Resolution.common.ovl` (1/5), `gui\1024\SChinese.common.ovl` (2/4),
     `gui\1280\Resolution.common.ovl` (1/5), `gui\1280\SChinese.common.ovl` (3/4) — partial,
     never 100%, consistent with a cursor that sometimes re-syncs by coincidence rather
     than a deterministic all-or-nothing failure.
3. **Particles — 1 file, total failure.** `Particles\Particles.common.ovl`: 41 `psi` +
   5 plain `tex` entries, **0 of 46 decoded**.

Files *not* in this list (including every custom-scenery fixture added under
`OpenCobra/Tests/Fixtures/OVL/`) contain no `mms`/`prt`/`psi`/`fct` loader mixed with a
`tex`/`flic`/`btbl` loader in the same archive, so they never hit either root cause and
decode cleanly — this is why the CS fixtures added earlier all pass despite the bug still
being open; they simply don't exercise it.

**Why this list matters:** re-scanning the full 7,490-file install takes about 5 minutes
per run, which is too slow to use as a fix/verify loop. Point a fix directly at a handful
of files from the list above instead — `Characters\AF\AF01_Body_Main.common.ovl` (skin,
smallest known-affected file with the original evidence already characterized) and
`Main.common.ovl` (font-table, largest blast radius: 84 ordinary textures blocked by 6
`fct` loaders) are the two best single-file repro cases, one per root-cause-triggering
tag family. Re-run the full scan only once, as the final regression check, after a fix
lands.

None of these files can become embedded test fixtures (they're Frontier's copyrighted
base-game data — see the custom-scenery fixture work above for why that data source was
avoided), so this list lives here rather than as committed test data; use it against a
local `RCT3_PATH` install via the existing `RCT3_PATH`-gated integration tests in
`OpenCobra/Tests/Integration/`.

## Part 5 — Correction: `mms`/`prt` are not texture data; group 1 above is out of scope (Open)

**`mms` and `prt` are not texture-shaped data**, so their 100% decode failure in Part 4's
group 1 (28 character/animal skin files) is not this bug — it's `CharacterSkins.cs`
attempting to texture-decode data that was never a texture to begin with. Confirmed by
dumping `AF01_Body_Main.common.ovl`'s raw loader table: loader type 0 (the `mms` tag)'s
own category string is **`MorphMesh`**, and type 1 (`prt`) is **`PeoplePart`** — both read
directly from the archive's own loader header, the same field the reference dumper keys
its per-type decode branches on (`LoaderNames[l.LoaderType]`). Neither name suggests
texture data, and per-symbol dumps confirm it: `SkinBody_AF01_L2:mms`'s resolved block
doesn't match the `BitmapTable{Unk,Length}` shape `CharacterSkins.cs` assumes (`Unk=44`,
not the `0` every other bitmap table in the codebase has) — it's some other, unidentified
`MorphMesh`-specific struct (likely blend-shape/body-morph data for the character
customization system, unrelated to the tex/flic/btbl pipeline this doc covers).

This reframes group 1 entirely: it was never "the same bug" as the font-table/particle
groups, just a coincidentally-identical symptom (0 decoded) from a different cause
(wrong data source, not a resolution bug). **`CharacterSkins.cs`'s premise needs separate
reconsideration outside this doc** — either drop the bitmap-table/flic/tex decode attempt
for `mms` entirely, or find `MorphMesh`'s actual struct layout if it turns out to embed a
texture reference some other way. `prt` (`PeoplePart`) hasn't been individually verified
the same way yet but is grouped with `mms` here on the strength of its equally
texture-unrelated loader name; don't assume it's fixed by anything in Part 3 either.

**What's still real and still in scope:** every skin archive in group 1 also carries
exactly one genuine `Texture`-tagged symbol (e.g. `AF01_Body:tex` in
`AF01_Body_Main.common.ovl`) that is *not* `mms`/`prt`-tagged and sits in the same loader
table as the `MorphMesh`/`PeoplePart` loaders — and it *also* fails to decode, which is
Root cause A's predicted blast radius (a loader type with no matching generic-chunk
handler desyncing the cursor for every loader parsed after it), not a `mms`/`prt`-specific
issue. Re-verifying against `AF01_Body_Main.common.ovl` after a Root cause A/B fix should
check that this one `tex` entry starts decoding — not that the `mms`/`prt` entries do.

`fct` was already shown non-texture-shaped for at least one case in Part 2
(`GUIFontSmallNumbers:fct` decodes as a `LOGFONT`), consistent with this same pattern, but
group 2's actual bug signal (plain `tex` entries failing in `fct`-adjacent files) is
independently confirmed and still the strongest, most direct evidence for Root cause A.
`psi` has not been checked the same way `mms` was here — verify its loader category name
and a sample symbol's raw bytes the same way before assuming `ParticleEffects.cs`'s premise
holds, using the method in Part 4 as a template.

## Key references

- [`rct3tex.cpp::ReadTexture`](https://github.com/chances/rct3-importer) —
  reference standalone-flic decoder (mip while-loop, level/width math).
- [`rct3tex.cpp::ReadTextures`](https://github.com/chances/rct3-importer) —
  reference BTBL mip decoder (per-mip block-counted sizes).
- [`rct3tex.cpp::TextureLoader`/`FlicStruct`/`TextureStruct2`/`DoReloc`](https://github.com/chances/rct3-importer) —
  reference tex→flic pointer chain and the relocation-fixup table `OVL.cs` doesn't parse.
- `rct3tex.cpp:1830-1842` (`DoReloc` fixup loop) and `rct3tex.cpp:1878-2008` (the
  `LoaderStruct` loop, per-loader-type extra-data handling for `btbl`/`txt`/`gsi`/`flic`/
  `tex`/`ftx`) — the two sections behind Part 3's root causes.
- `assets/reference/ovl/parser.rs` (`extract_resources`, `detect_symbol_layout`) and
  `assets/reference/ovl/reader.rs` (`read_extra_data`) — semi-working Rust
  reimplementation kept for cross-reference; shares the same generic-chunk assumption
  identified as Root cause A.
- `OpenCobra/OVL/Files/Textures.cs` — current state.
- `OpenCobra/OVL/OVL.cs:158-180` — `TryReadExtraData` (the bitmap-table index / flic
  chunk lookup).
- `OpenCobra/OVL/OVL.cs:298-327` — `ReadLoaderExtraData` (Root cause A: generic chunk
  parsing where the format is actually per-loader-type).
- `OpenCobra/OVL/OVL.cs:447-453` — `SkipRelocations` (Root cause B: discards the
  relocation-fixup table instead of parsing it).
- `OpenCobra/OVL/OVL.cs:447-529` — `ExtractResources` (symbol → `FileType` resolution,
  including the positional loader-walk fallback, and the still-unfixed "Suspect 4"
  no-origin-check block resolution).
- `OpenCobra/OVL/Files/FileTypes.cs` — `FileType` enum and `ToFileType`/`ToTagString`
  conversions, now including `CharacterSkinSet`/`CharacterSkinPart`/`ParticleSpriteItem`/
  `FontCharacterTable`.
- `OpenCobra/OVL/Files/CharacterSkins.cs` / `OpenCobra/OVL/Files/ParticleEffects.cs` —
  `mms`/`prt`/`psi` decoders (Part 4), shape-detecting per symbol; not yet verified to
  produce correct pixel data since Root causes A/B are still open.
- Part 4's 37-file list (above) — exact real-install files affected; use instead of a
  full-install rescan when iterating on a fix.
- [`ovl-resource-relocation.md`](./ovl-resource-relocation.md) — the earlier, related
  symbol-resolution bug (fixed for `svd`/`ftx`; this doc's open issue is a further,
  structurally different gap in the same subsystem).

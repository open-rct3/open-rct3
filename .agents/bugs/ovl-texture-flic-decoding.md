# Bug: `Textures.Extract` decoded 0 textures and crashed on real RCT3 data

## Status: Fixed

The flic decoder pipeline in `OpenCobra/OVL/Files/Textures.cs` produced 0 successful
texture decodes and aborted on the first `Debug.Assert` failure when run against the
real RCT3 install. Root-caused and fixed by cross-referencing the reference C++
implementation (`C:\Users\enigm\Applications\RCT3\Dumper\rct3tex.cpp`, Jonathan Wilson's
`rct3tex` dumper — the working flic decoder). Three concrete fixes:

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

## Verification

`Textures.Extract` against the full real RCT3 install (`*.common.ovl` only, 7,490
archives), 4.9s wall-clock, no archive-loading crashes:

| Metric                     | Before             | After |
|----------------------------|--------------------|-------|
| Hard archive failures      | 1 (assert dialog)  | 0     |
| Textures decoded           | 1 (then crash)     | 51    |
| Mip levels decoded         | 0                  | 244   |
| Unit tests (with RCT3_PATH)| passing            | passing (14,276 passed, 0 failed) |

`make test` is the canonical Unit Tests entry point and continues to pass.

### Remaining failures (all graceful, no crash)

Categorised by error signature from `run.log`:

1. **`Failed to resolve flic data` (~161 entries).** `Tex.Ts2Ptr` resolves, but
   the chain to a flic extra-data chunk doesn't exist for many texture entries —
   texture entries in BTBL-backed archives use a 4-byte BTBL index rather than the
   `Tex → TextureStruct2 → Flic` chain. Needs a `ReadTexture` fallback that
   recognises a 4-byte standalone flic chunk and treats it as a BTBL index when a
   bitmap table is available (analogous to what `ReadFlic` already does for direct
   `flic` entries).
2. **`Failed to resolve TextureStruct2` (~50).** Same as #1, the chain breaks
   further upstream.
3. **`Failed to resolve bitmap table data` (~25).** BTBLs with only 1 extra-data
   chunk (header only, no pixel-data chunk) — needs investigation of the
   `ReadLoaderExtraData` boundary in `Ovl.cs:300-318` for these specific loaders.
4. **`references a bitmap table that failed to decode` (~80).** Cascading from #3.
5. **`Image cannot be loaded. Available decoders: ...` (4 entries).** The remaining
   BTBL pixel data is being read with `Image.Load<Rgba32>(data)`, which tries to
   auto-detect PNG/JPEG/etc. from the raw bytes. The fix is to use
   `Image.LoadPixelData<Rgba32>` with the per-mip dimensions instead — the
   standalone-flic path now does this correctly, the BTBL path still needs the
   same treatment.
6. **3 `ArgumentOutOfRangeException`** and **3 `BitConverter.ToInt32` errors** in
   the log — small handful, likely individual resource-classification or boundary
   bugs. Worth a follow-up pass once #1-#5 are addressed.

## Key references

- [`rct3tex.cpp::ReadTexture`](https://github.com/chances/rct3-importer) —
  reference standalone-flic decoder (mip while-loop, level/width math).
- [`rct3tex.cpp::ReadTextures`](https://github.com/chances/rct3-importer) —
  reference BTBL mip decoder (per-mip block-counted sizes).
- `OpenCobra/OVL/Files/Textures.cs` — current state.
- `OpenCobra/OVL/OVL.cs:158-180` — `TryReadExtraData` (the bitmap-table index / flic
  chunk lookup that #1 and #3 above need to plug into).

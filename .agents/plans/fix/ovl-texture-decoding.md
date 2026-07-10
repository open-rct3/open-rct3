# Plan: Fix `tex`/`flic`/`btbl` symbol-resolution failures

## Background: on-disk layout needed to debug the remaining BTBL bug

Reference sources: `assets/reference/ovl/{parser.rs,tex.rs,btbl.rs}` (a working, independently
developed Rust OVL decoder that correctly produces `tex`/`ftx` pixel data — treat as
authoritative for algorithm/field layout) and `rct3tex.cpp` (Jonathan Wilson's `rct3tex`
dumper, the original C++ reference both were cross-checked against).

- **`LoaderStruct`** (`blocks[2].Blocks[1]`, one entry per resolved symbol instance, not per
  loader type): 20 bytes — `LoaderType`(u32, direct index into `loaderHeaders`), `data`(ptr,
  u32), `HasExtraData`(u32, v5 packs a 16-bit count in the low bits), `Sym`(ptr, u32),
  `SymbolsToResolve`(u32).
- **`Tex` struct** (`TextureStruct`, icontexture.h, 60 bytes): `FlicPtr` at offset 52 is a
  `FlicStruct**` (double pointer, "always points to pointer before flic data") — the only
  field on the pixel-data path. `Ts2Ptr`/`TextureStruct2` (offset 56) is unrelated to pixel
  data and was removed from the C# struct.
- **BTBL chunk shape** (`ManagerBTBL.cpp`, confirmed by `btbl.rs::BitmapTable::decode_entry`):
  the inline resource is just an 8-byte `BmpTbl{Unk, Length}` header. The loader's own extra
  data is exactly two length-prefixed chunks: chunk 0 = 8 leading zero bytes + `Length ×
  FlicHeader` (16 bytes each: Format, Width, Height, MipCount); chunk 1 = all mip pixel bytes
  for every table entry, concatenated in table order, each mip sized independently
  (`max(1, dim>>level)`, `/4` for DXT block counts, `BlockSize()`/`BitsPerPixel()/8` per pixel).
- **Relocation-fixup table**: a flat list of `relCount` source addresses; for each, the raw
  `u32` stored at that address is trustworthy as a real pointer *only if* the address is
  listed here (unlisted = unpatched placeholder). `LoaderStruct.data` is itself one such
  fixup-table-only pointer, not a directly-usable address.

## Ground rules (still hold)

- Domain logic (what a `btbl`/`flic`/`tex` loader's fields *mean*) belongs in
  `OpenCobra/OVL/Files/TextureDecoding.cs`, not `OVL.cs`. `OVL.cs` only exposes generic
  primitives (relocation lookup, loader-order enumeration).
- The 7 embedded custom-scenery fixtures (baseline: 5 textures decode) are a regression
  check, not a progress check — they don't exercise this bug's real failure modes.
  Progress is measured against a real `RCT3_PATH` install: `Main.common.ovl` (84 `Texture`
  entries, largest blast radius) and `Characters/AF/AF01_Body_Main.common.ovl` (one genuine
  non-`mms`/`prt` `tex` entry) are the two single-file repros. Full 7,490-file scan only as
  a final regression check (~5 min).
- `mms`/`prt`/`psi`/`fct` are conclusively out of scope — not texture-shaped data.
  `CharacterSkins.cs`/`ParticleEffects.cs` have a separate, unrelated premise bug; leave
  them alone beyond keeping them compiling.
- Do not add new `RCT3_PATH` fixtures (copyright-blocked).

## Implemented this session

- **`Ovl.ReadRelocations`/`TryGetRelocationSource`** (`OVL.cs`, replacing the old
  `SkipRelocations` no-op): parses the relocation-fixup table into a
  `sourceAddress -> rawValueAtThatAddress` map. Verified byte-for-byte against
  `assets/reference/ovl/parser.rs`'s `resolve_relocations`/`reloc_target`.
- **`TextureDecoding.ReadTexture`'s `FlicPtr` two-hop chase** (`TextureDecoding.cs`):
  replaced the wrong `Ts2Ptr`-based chain with the double relocation-hop chase on `FlicPtr`
  (offset 52), matching `tex.rs::Texture::decode` exactly. `Ts2Ptr` field removed from the
  `Tex` struct (dead).
- **`Ovl.LoaderEntriesInOrder`** (`OVL.cs`) + **`TextureDecoding.ReadBitmapTableAt`**: `btbl`
  and `flic` are pure loader-category tags, never classified symbols (confirmed empirically:
  `Main.common.ovl` has zero `FileType.BitmapTable` symbols despite 10 real `btbl` loader
  instances) — they're only discoverable by walking the loader table in on-disk order. Added
  an ordered `(Tag, DataAddress)` enumerator on `Ovl`, an address-based BTBL decode path, and
  rewired `Textures.Extract` to build a `flicAddress -> currentBtblTable` map by walking
  loader entries in order (mirrors `tex.rs::TextureContext::build`). `ReadTexture`'s
  `bitmapTable` param is now `IReadOnlyDictionary<uint, Texture[]>?` keyed by flic address
  (was a single `Texture[]?`), since one archive can have multiple BTBLs.
- Skipped the originally-planned "read `LoaderStruct.LoaderType`/`.Sym` directly in
  `ExtractResources`" classification fix — see Finding below; it's a no-op for both repro
  targets.

## Diagnostic finding: classification fix is not needed for either repro target

`LoaderStruct.LoaderType` is a correct, in-range, position-based index into `loaderHeaders`
— but `tex`/`fct` never appear as a `LoaderType` value in `Main.common.ovl`'s loader array at
all. Only loaders carrying an attached extra-data chunk stream (`ftx`/`btbl`/`flic`/`txt`/
`snd`) get a `LoaderStruct` entry; `tex`/`fct` symbols already classify correctly via the
primary name-suffix path and never hit the positional-walk fallback a `LoaderType`-based
classification fix would replace. That fallback bug may still be real for some other archive
lacking name-suffix tags, but it doesn't block either repro target here. See
`OpenCobra/Tests/Integration/LoaderTypeIndexDiagnostic.cs`.

## Verified progress

`Characters/AF/AF01_Body_Main.common.ovl`'s one genuine `tex` entry now decodes
(`OpenCobra/Tests/Integration/TextureDecodeVerification.cs`,
`Af01BodyMain_GenuineTexEntry_DecodesAfterRelocationFix`, passing). Confirms the `FlicPtr`
two-hop relocation chase end-to-end for a real, non-BTBL-backed case.

## Still broken: `Main.common.ovl` remains 0/84

The `FlicPtr` chase itself is confirmed working for this file too (`flicAddr` resolves and is
present in the loader-order flic list). The blocker is entirely inside
`ReadBitmapTableAt`/`DecodeBitmapTable`'s chunk parsing: **all 10** of `Main.common.ovl`'s
BTBL loader instances fail to decode, so `bitmapTablesByFlicAddress` ends up empty and every
`flic`-backed `tex` entry throws "references a bitmap table that failed to decode". Two
distinct failure signatures observed (via `OpenCobra/Tests/Integration/RelocationDebug.cs`, a
scratch diagnostic — delete or formalize it):

- `IndexOutOfRangeException` for several BTBLs — likely the 8-byte `BmpTbl` header read via
  `TryReadBytes(dataAddress, 8, ...)` lands at the wrong address, or the resolved block is
  smaller than expected.
- `"bitmap table entry N mip M truncated: needed X bytes, got 0"` for others — the
  pixel-data chunk (`chunks[1]`) runs out of bytes partway through: either mip-count/
  dimension math is wrong for this data, or `chunks[1]` isn't the full pixel blob expected.

## Scope caveats not yet independently verified

- `psi` (`ParticleSpriteItem`) hasn't been checked the same way `mms`/`prt` were (dumping its
  loader category name and a sample symbol's raw bytes) before assuming `ParticleEffects.cs`'s
  tex/flic/btbl-shaped premise is wrong — it's grouped with `mms`/`prt` on the strength of the
  Rust reference's no-op dispatch arm for it, not independently confirmed.
- `gsi`/`shs` are not classified symbols in `Main.common.ovl` either (0 `LoaderStruct` entries
  each, same signature as `tex`/`fct`) — consistent with the same "loader-category-only, no
  symbol" pattern, but not independently root-caused the way `tex`/`fct` were.

## Known affected files (real install, pre-fix baseline)

A full 7,490-file `*.common.ovl` scan found 37 files exercising this bug family, before this
session's fixes: 28 character/animal-skin archives (now known out-of-scope per `mms`/`prt`
above, except each carries one genuine in-scope `tex` symbol alongside them — e.g.
`Characters/AF/AF01_Body_Main.common.ovl`'s `AF01_Body:tex`), 8 font-table-adjacent files
(`Main.common.ovl` largest at 84 `Texture` entries; `gui/{800,1024,1280}/{Resolution,
SChinese}.common.ovl` partial failures), and `Particles/Particles.common.ovl` (41 `psi` + 5
plain `tex`, out of scope pending the `psi` caveat above). None can become embedded test
fixtures (Frontier's copyrighted base-game data) — verify against a local `RCT3_PATH` install.

## Next steps

1. Debug `ReadBitmapTableAt` against one of the 10 failing BTBLs (e.g. `btbl@54B54` or
   `btbl@565F0` in `Main.common.ovl`): dump `chunks[0]`/`chunks[1]` lengths and the decoded
   `BmpTbl.Length`/per-entry `FlicHeader` values, cross-referenced byte-for-byte against
   `assets/reference/ovl/btbl.rs::BitmapTable::decode_entry` (not yet done — this session
   verified the tex/flic chase against the reference but not the BTBL chunk math).
2. Confirm whether the two-chunk model still holds for these entries, or whether
   `Main.common.ovl`'s BTBLs have a different chunk count/shape than the fixtures'
   `ReadBitmapTable` path was validated against.
3. Re-run `TextureDecodeVerification` against `Main.common.ovl` — expect >0 of 84 to decode.
4. Delete or formalize `RelocationDebug.cs`.
5. Re-run `TexturesMeasurementTests` (fixture baseline, still 5 as of last check) and
   `make test` as regression checks once the BTBL bug is fixed.
6. Only after 1-3 succeed: run the full 7,490-file scan once as the final regression check.

# Plan: Fix `tex`/`flic`/`btbl` symbol-resolution failures

Read `.agents/bugs/ovl-texture-decoding.md` Part 6 before touching this — it has the exact
C++/Rust line references behind every claim below.

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

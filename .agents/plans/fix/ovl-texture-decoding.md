# Plan: Fix `tex`/`flic`/`btbl` symbol-resolution failures

## Status: Fixed and verified

Full 7,490-file `*.common.ovl` scan of a real RCT3 install, before vs. after this fix:

| Metric              | Before | After | Diff  |
|----------------------|-------:|------:|------:|
| Total textures decoded | 258 | 858 | +600 |
| Files with more textures decoded | — | 67 | |
| Files regressed (fewer textures) | — | 0 | |
| Crashes | 0 | 0 | |

`Main.common.ovl` (the largest single-file repro, 84 `Texture` entries): 0 → 84/84 decoded.
`Characters/AF/AF01_Body_Main.common.ovl`'s one genuine `tex` entry: also decodes. `make test`
(152 tests) and the fixture measurement baseline (5 textures, unchanged) both pass.

## Root cause (final)

Three independent bugs stacked on top of each other, all in `OpenCobra/OVL/OVL.cs` and
`OpenCobra/OVL/Files/TextureDecoding.cs`:

1. **The relocation-fixup table was discarded outright** (`SkipRelocations` just seeked past
   it). Fixed: `Ovl.ReadRelocations`/`TryGetRelocationSource` now parse it into a
   `sourceAddress -> rawValueAtThatAddress` map, verified against
   `assets/reference/ovl/parser.rs`'s `resolve_relocations`/`reloc_target`.
2. **`TextureDecoding.ReadTexture` chased the wrong pointer field** (`Ts2Ptr`/`TextureStruct2`,
   offset 56) instead of the one real pixel data lives behind (`FlicPtr`, offset 52 — a
   `FlicStruct**` needing two chained relocation-table hops). Fixed to match
   `tex.rs::Texture::decode` exactly; `Ts2Ptr` removed from the `Tex` struct (dead).
3. **`btbl`/`flic` are pure loader-category tags, never classified symbols** — `ovl.Keys`
   cannot see them at all (confirmed empirically: `Main.common.ovl` has zero
   `FileType.BitmapTable` symbols despite 10 real `btbl` loader instances). Fixed:
   `Ovl.LoaderEntriesInOrder` walks the loader table directly in on-disk order;
   `Textures.Extract` builds a `flicAddress -> currentBtblTable` map from it (mirrors
   `tex.rs::TextureContext::build`); `TextureDecoding.ReadBitmapTableAt` decodes a BTBL by
   address instead of requiring a symbol.
4. **Bitmap-table mip count was derived (`log2(width)+1`) instead of read from disk.**
   `DecodeBitmapTable` used a computed guess instead of each entry's actual on-disk
   `FlicHeader.MipCount` field. Any mismatch desynced the shared pixel-data cursor on the
   first table entry, corrupting every entry after it — this was the last blocker, isolated
   and fixed by reading `flic.MipCount` directly, matching
   `btbl.rs::BitmapTable::decode_entry`'s `mipcount = le_u32(h, 12)`.

## Ground rules (still apply to future OVL/texture work)

- Domain logic (what a `btbl`/`flic`/`tex` loader's fields *mean*) belongs in
  `OpenCobra/OVL/Files/TextureDecoding.cs`, not `OVL.cs`. `OVL.cs` only exposes generic
  primitives (relocation lookup, loader-order enumeration).
- **Read reference implementations first.** `assets/reference/ovl/{parser.rs,tex.rs,btbl.rs}`
  is a working, independently developed Rust OVL decoder — treat it as authoritative for
  algorithm/field layout, and read the relevant source before writing code, not after a guess
  fails. This is now also an `AGENTS.md` rule.
- `mms`/`prt`/`psi`/`fct` are conclusively out of scope — not texture-shaped data.
  `CharacterSkins.cs`/`ParticleEffects.cs` have a separate, unrelated premise bug; leave them
  alone beyond keeping them compiling.
- Do not add new `RCT3_PATH` fixtures (copyright-blocked).

## Scope caveats not yet independently verified (follow-up, not blocking)

- `psi` (`ParticleSpriteItem`) hasn't been checked the same way `mms`/`prt` were (dumping its
  loader category name and a sample symbol's raw bytes) before assuming `ParticleEffects.cs`'s
  tex/flic/btbl-shaped premise is wrong — grouped with `mms`/`prt` on the strength of the Rust
  reference's no-op dispatch arm for it, not independently confirmed.
- `gsi`/`shs` are not classified symbols in `Main.common.ovl` either (0 `LoaderStruct` entries
  each, same signature as `tex`/`fct`) — consistent with the same loader-category-only
  pattern, but not independently root-caused.

## Known bug: unchecked short-read in `BinaryReaderExtensions.Read<T>`

`TextureDecoding.cs` (~line 151-165): `Read<T>` calls `reader.ReadBytes(Marshal.SizeOf(typeof(T)))`,
pins the result, and passes it to `Marshal.PtrToStructure`. `BinaryReader.ReadBytes` silently
returns fewer bytes than requested when the stream is short/truncated rather than throwing — so
a malformed or truncated OVL file can make the pinned array smaller than `sizeof(T)`, and
`PtrToStructure` reads past the end of that heap allocation. Affects every fixed-size struct
read on this path (`Tex`, `FlicHeader`, `FlicMipHeader`, `BitmapTable`). Fix: validate
`bytes.Length == Marshal.SizeOf(typeof(T))` before pinning/casting, return 0/throw otherwise.
Also has a dead `if (structure == null) data = default!;` branch immediately overwritten by an
unconditional `data = structure!;` right after — should instead `return 0` in that branch,
matching the fallback used in the `Read...Header` wrappers.

## Follow-ups (separate from this bug)

- `CharacterSkins.cs`/`ParticleEffects.cs`'s premise (`mms`/`prt`/`psi` are tex/flic/btbl-shaped)
  is wrong, not just unresolved — needs its own investigation into `MorphMesh`/`PeoplePart`'s
  actual struct layout, or dropping the decode attempt entirely.
- The short-read bug above.
- `gsi`/`shs`/`psi` scope caveats above.

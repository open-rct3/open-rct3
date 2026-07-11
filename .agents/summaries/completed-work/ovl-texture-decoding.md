# OVL Texture Decoding

Fixed `tex`/`flic`/`btbl` symbol-resolution failures in `OpenCobra/OVL/OVL.cs` and
`OpenCobra/OVL/Files/{TextureDecoding,Flic,BitmapTable,Textures}.cs`.

Whole-install verification (7,490 real `*.common.ovl` files, unified tex/flic/btbl/ftx count):
**258 → 2,599 textures decoded, 0 crashes, 0 regressions.** `Main.common.ovl`: 0 → 84/84.
`make test` (152 tests) passes.

## Root causes (four, stacked)

1. **Relocation-fixup table was discarded outright** (`SkipRelocations` just seeked past it).
   Fixed: `Ovl.ReadRelocations`/`TryGetRelocationSource` parse it into a
   `sourceAddress -> rawValueAtThatAddress` map, verified against `assets/reference/ovl/parser.rs`.
2. **`ReadTexture` chased the wrong pointer field** (`Ts2Ptr`/`TextureStruct2`, offset 56) instead
   of `FlicPtr` (offset 52, a double pointer needing two chained relocation hops). Fixed to match
   `tex.rs::Texture::decode`; `Ts2Ptr` removed.
3. **`btbl`/`flic` are pure loader-category tags, never classified symbols** — invisible to
   `ovl.Keys` entirely. Fixed: `Ovl.LoaderEntriesInOrder` walks the loader table directly in
   on-disk order; `Textures.Extract` builds a `flicAddress -> currentBtblTable` map from it
   (mirrors `tex.rs::TextureContext::build`).
4. **Bitmap-table mip count was derived (`log2(width)+1`) instead of read from disk**, desyncing
   the shared pixel-data cursor on the first table entry and corrupting every entry after it.
   Fixed by reading `FlicHeader.MipCount` directly, matching `btbl.rs::BitmapTable::decode_entry`.

Also fixed in passing: `ReadTexture` crashed (`Debug.Fail`) on `Style/Vanilla/Scenery.common.ovl`'s
`ObjectIcons`/`ObjectIcons01` because it read the full 60-byte `Tex` struct from a resource-size
guess (block end minus offset) that can be shorter than 60 bytes near a block boundary. Fixed to
read only the one field still needed locally (`Type`, offset 40), bounds-checked — matching how
the reference never reads a bounded `Tex` byte slice at all.

## Refactor

Split per-format decode logic out of `TextureDecoding.cs` into `Flic.cs` and `BitmapTable.cs`
(class `BitmapTables`); `TextureDecoding.cs` now holds only shared plumbing (`Texture`/
`TextureCollection` types, the `Tex` struct + `ReadTexture`, DXT decoder, `ComputeMipCount`).
`Textures.Extract` is the single unified entry point — it now also decodes `ftx` (FlexiTexture)
internally, one `Texture` per animation frame, so callers no longer separately enumerate and call
`FlexiTextureList.Load`.

## Not this bug (diagnosed, mostly benign)

`Style/Vanilla`/`Style/Themed` scenery files showing 0 decodes pre-refactor were **not** broken:
most `*Style.common.ovl` files carry only scenery-item definitions (`sid`/`anr`/`sat`/`sta`), no
pixel data at all; most `*UltraLow*`/`*LowestLOD*`/`*Flexi*` files are `ftx`, decoded by a separate
pipeline the old scan never measured. Both are now correctly included via the `Textures.Extract`
unification above.

## Follow-ups (separate, unresolved)

- `CharacterSkins.cs`/`ParticleEffects.cs`'s premise (`mms`/`prt`/`psi` are tex/flic/btbl-shaped)
  is wrong, not just unresolved — needs investigation into `MorphMesh`/`PeoplePart`'s actual
  struct layout, or dropping the decode attempt entirely. `psi` specifically hasn't been
  independently checked the way `mms`/`prt` were.
- `BinaryReaderExtensions.Read<T>` (`TextureDecoding.cs`) doesn't validate that
  `BinaryReader.ReadBytes` returned the full requested size before `Marshal.PtrToStructure` -
  still affects `FlicHeader`/`FlicMipHeader`/`BitmapTable` reads (fixed only for `Tex`, above).
- `gsi`/`shs` show the same "0 LoaderStruct entries" signature as `tex`/`fct` in `Main.common.ovl`
  but weren't independently root-caused.

# OVL Resource Relocation

`Ovl.ReadResource`/`Ovl.Keys` returned the wrong bytes for a class of resources (SVD, FTX, and likely
others) — sometimes another resource's struct, sometimes a fragment of the resource's own name string.
Three concrete bugs in `Ovl.ExtractResources()` (`OpenCobra/OVL/OVL.cs`) plus a relocation-fixup
table that was being discarded outright. All four fixed; SVD/FTX byte-level decoding now reliable.

## What landed

1. **Symbol record stride was guessed, not derived from version.** The real format is unambiguous: v1
   archives use the 12-byte `SymbolStruct`; v4/v5 use the 16-byte `SymbolStruct2`. Neither layout has
   a 4-byte header before the table — the `blockOffset` branches in the old code were fictional.
   Guessing from `symbolBlock.Size % 12/16` meant any block whose size was a multiple of 48 (divisible
   by both) silently locked onto the wrong stride, misaligning every `namePtr`/`dataPtr` read for the
   rest of that file. Fixed by selecting the stride directly from the OVL header version
   (`version == 1 ? 12 : 16`, no offset, ever).
2. **A bogus "size" field at offset+12 of a 16-byte record.** That offset is actually the symbol's
   djb2 name hash (`SymbolStruct2.hash`) — neither struct stores a size. Removed; size now always
   falls back to "read to the end of the resolved block," the only value the format supports.
3. **Inverted, fragile tag resolution.** Every symbol name is written as `"Name:Tag"`
   (e.g. `RomPil_1H:svd`) regardless of version — confirmed via `FindSymbolString(name, tag)` in the
   reference writer. The old fallback split on `:` and used segment `[0]` (the name) instead of the
   tag, and only ran when the loader/tag countdown walk had already failed — which it reliably does
   for v1/v4 archives, since those versions carry no per-loader symbol count at all (that field is
   v5-only). Fixed by making the name's own embedded tag suffix the *primary* source of `FileType`,
   stripped from the returned name so `OvlFile.Name` is clean (`RomPil_1H`, not `RomPil_1H:svd`);
   the loader/tag walk is now only a fallback for the (should-not-happen) case of a name with no
   recognized tag suffix.
4. **Relocation-fixup table was discarded outright.** The same `SkipRelocations` step that broke
   `Tex.FlicPtr`/`Ts2Ptr` (fixed separately in
   [`ovl-texture-decoding.md`](ovl-texture-decoding.md)) also meant `Ovl.ReadResource` couldn't
   dereference pointer-typed symbol fields at all. The relocation parser — `Ovl.ReadRelocations` /
   `TryGetRelocationSource` — was added in the texture-decoding pass; this resource-relocation fix
   builds on it.

Suspect 5 (shared relocation address space across `common.ovl`/`unique.ovl`) was investigated and
confirmed **correct as originally written** — `cOvlFileClass::MakeRelOffsets` in `OVLClasses.cpp`
explicitly chains the unique file's base offset from the common file's end
(`OpenFiles[OVLT_UNIQUE].MakeRelOffsets(OpenFiles[OVLT_COMMON].MakeRelOffsets(0))`). No change.

## How it was found

While adding a real-data integration test for `SvdFlags` enum coverage
(see [`ovl-enum-verification.md`](ovl-enum-verification.md)), the test's first run against the full
RCT3 asset library (12k+ SVD resources) produced thousands of "undocumented flag bit" failures.
Decoding the "flag" bytes as ASCII showed they were fragments of the resources' own names
(e.g. four distinct `RomPil*` resources across four distinct archives all returning `0x69506D6F` =
`"omPi"`) — the enum wasn't missing bits; the resolver was returning the wrong bytes. The pre-existing
`nullbmp:ftx` defensive assertion in `ExtractResources.cs:43` confirmed the same failure mode had
been observed for FTX on a different loader tag, pointing at the shared `ExtractResources` /
`ReadString` machinery rather than anything SVD-specific.

## Testing

- `EnumCoverage.SvdResources_AreReadable` against the full real RCT3 asset library: **12,114 SVD
  reads across 14,980 archives, all passing.**
- Manual spot-check of the entries from the original investigation, re-read after the fix:

  | Resource          | Before                | After                            |
  |-------------------|-----------------------|----------------------------------|
  | `RomPilBot_1H`    | `0x69506D6F` ("omPi") | `0x00000001` (`SvdFlags.Greenery`) |
  | `RomPilTop_1H`    | `0x69506D6F`           | `0x00000001`                     |
  | `RomPil_1H`       | `0x69506D6F`           | `0x00000001`                     |
  | `RomPil_4H`       | `0x69506D6F`           | `0x00000001`                     |
  | `RomPilBotShort_1H` | (untested before)   | `0x00000000`                     |

  Each resource now reads a distinct, plausible `sivflags` value instead of an identical ASCII
  fragment of its own name.
- `ExtractResources.Load_NullbmpFtx_ExtractsFlexibleTexture` (the pre-existing FTX-side guard) passes
  without its defensive assertion catching anything — real texture data comes back instead of the
  `"nullbmp:ftx"` symbol-name string.
- Full solution test suite: **19,608 passed, 0 failed.**

## References

- [OpenCobra/OVL/OVL.cs](../../OpenCobra/OVL/OVL.cs) — `Ovl.ExtractResources`, `Ovl.ReadRelocations`,
  `Ovl.TryGetRelocationSource` (relocation parser landed in the texture pass;
  see [ovl-texture-decoding.md](ovl-texture-decoding.md)).
- [OpenCobra/OVL/Enums.cs](../../OpenCobra/OVL/Enums.cs) — `FileType` enum + `ToFileType`/`ToTagString`
  conversions.
- [OpenCobra/OVL/Files/FileTypes.cs](../../OpenCobra/OVL/Files/FileTypes.cs) — `FileTypeExtensions`
  (name/tag-suffix handling).
- [OpenCobra/Tests/ExtractResources.cs](../../OpenCobra/Tests/ExtractResources.cs) — `Load_NullbmpFtx`
  guard test.
- [OpenCobra/Tests/EnumCoverage.cs](../../OpenCobra/Tests/EnumCoverage.cs) — `SvdResources_AreReadable`
  integration test (gated on `RCT3_PATH`).
- [ovl-texture-decoding.md](ovl-texture-decoding.md) — sibling fix: relocation-fixup table parser
  and the tex/flic/btbl decode work that consumed it.
- [ovl-enum-verification.md](ovl-enum-verification.md) — the work that surfaced this bug.
- Reference C++: `rct3-importer` → `RCT3 Importer/src/libOVLng/OVLClasses.cpp` (`ReadFile`,
  `MakeSymbols`, `MakeRelOffsets`) and `LodSymRefManager.cpp` (`cSymbol::fill`).

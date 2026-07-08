# Bug: `OVL.cs` resource relocation is unreliable — wrong bytes returned for a class of resources

## Status: Fixed

Root-caused and fixed in `Ovl.ExtractResources()` (`OpenCobra/OVL/OVL.cs`) by cross-referencing the
real archive format against the reference C++ implementation
(`D:\Users\enigm\GitHub\rct3-importer\RCT3 Importer\src\libOVLng\OVLClasses.cpp`'s `ReadFile`/`MakeSymbols`
and `LodSymRefManager.cpp`'s `cSymbol::fill`). Three concrete bugs, all downstream of **Suspect 1**:

1. **Symbol record stride was guessed, not derived from version (Suspect 1).** The real format has no
   ambiguity: v1 archives use the 12-byte `SymbolStruct`; v4/v5 use the 16-byte `SymbolStruct2`. Neither
   layout has a 4-byte header before the table — the `blockOffset` branches in the old code were
   fictional. Guessing from `symbolBlock.Size % 12/16` meant any block whose size was a multiple of 48
   (divisible by both) silently locked onto the wrong stride, misaligning every `namePtr`/`dataPtr` read
   for the rest of that file. This was the actual cause of unrelated entries "colliding" on the same
   garbage bytes — fixed by selecting the stride directly from the OVL header version instead
   (`version == 1 ? 12 : 16`, no offset, ever).
2. **A bogus "size" field (related to Suspect 1).** The old code read the 4 bytes at offset+12 of a
   16-byte record as a resource byte size. That offset is actually the symbol's djb2 name hash
   (`SymbolStruct2.hash`) — neither struct stores a size at all. Removed; size now always falls back to
   "read to the end of the resolved block," the only value the format supports.
3. **Inverted, fragile tag resolution (the "top-level vs. colon-suffixed sub-resource" framing, related
   to Suspects 3 and 6).** Every symbol name is written as `"Name:Tag"` (e.g. `RomPil_1H:svd`)
   *regardless of version* — confirmed via `FindSymbolString(name, tag)` in the reference writer. The old
   fallback split on `:` and used segment `[0]` (the name) instead of the tag, and only ran when the
   loader/tag countdown walk (Suspect 3) had already failed — which it reliably does for v1/v4 archives,
   since those versions carry no per-loader symbol count at all (that field is v5-only). Fixed by making
   the name's own embedded tag suffix the *primary* source of `FileType`, stripped from the returned
   name so `OvlFile.Name` is clean (`RomPil_1H`, not `RomPil_1H:svd`); the loader/tag walk is now only a
   fallback for the (should-not-happen) case of a name with no recognized tag suffix.

Suspect 5 (shared relocation address space across `common.ovl`/`unique.ovl`) was investigated and found
to be **correct as originally written** — confirmed via `cOvlFileClass::MakeRelOffsets` in
`OVLClasses.cpp`, which explicitly chains the unique file's base offset from the common file's end
(`OpenFiles[OVLT_UNIQUE].MakeRelOffsets(OpenFiles[OVLT_COMMON].MakeRelOffsets(0))`). No change needed
there.

### Verification

- `EnumCoverage.SvdResources_AreReadable` against the full real RCT3 asset library: 12,114 SVD reads
  across 14,980 archives, all passing.
- Manual spot-check of the exact entries from the table below, re-read after the fix:

  | Resource | Before | After |
  |---|---|---|
  | RomPilBot_1H | `0x69506D6F` ("omPi") | `0x00000001` (`SvdFlags.Greenery`) |
  | RomPilTop_1H | `0x69506D6F` | `0x00000001` |
  | RomPil_1H | `0x69506D6F` | `0x00000001` |
  | RomPil_4H | `0x69506D6F` | `0x00000001` |
  | RomPilBotShort_1H | (untested before) | `0x00000000` |

  Each resource now reads a distinct, plausible `sivflags` value instead of an identical ASCII fragment
  of its own name.
- `ExtractResources.Load_NullbmpFtx_ExtractsFlexibleTexture` (the pre-existing FTX-side workaround/guard
  for this same bug) passes without needing its defensive assertion to catch anything — real texture
  data comes back instead of the `"nullbmp:ftx"` symbol-name string.
- Full solution test suite: 19,608 passed, 0 failed.

The analysis below is kept as-written for historical context; it correctly identified the failure mode
and pointed at the right region of code even before the root cause was pinned down exactly.

## Summary

`Ovl.ReadResource` (`OpenCobra/OVL/OVL.cs`) sometimes returns bytes that belong to a *different*
resource than the one requested, or bytes that are actually part of a symbol name string rather than
struct data. This has been directly observed against real RCT3 game archives (not synthetic/fixture
data), for both Flexi-Textures (ftx) and Scenery Item Visuals (svd). It is not an isolated one-off: the
codebase already contains a defensive workaround for one manifestation of it
(`OpenCobra/Tests/ExtractResources.cs:43`), which strongly suggests the underlying cause is systemic
rather than specific to one file type.

## Evidence

### 1. FTX (already partially worked around)

`ExtractResources.cs` extracts a FlexiTexture resource named `nullbmp` and explicitly asserts:

```csharp
// Must NOT be the symbol name string
Assert.That(asString, Does.Not.Contain("nullbmp:"),
    "Load returned the symbol name string instead of texture data");
```

The comment confirms this has previously been observed: `ReadResource` can return the resource's own
*symbol name string* (e.g. `"nullbmp:ftx"`) instead of its actual binary contents. The test guards
against it recurring for this one fixture, but nothing prevents the same failure mode for any other
resource — it is caught here only because someone happened to notice and pin it down for `nullbmp`.

### 2. SVD (newly discovered, not worked around)

While writing an integration test against real RCT3 data
(`OpenCobra/Tests/EnumCoverage.cs`), a byte-offset read of the first 4 bytes of several `.svd`
(`SceneryItemVisual`) resources — expected to be the `sivflags` field — produced identical values for
resources with unrelated names:

| Resource name         | Archive                 | Bytes read (LE uint32) | Decoded as ASCII |
|------------------------|--------------------------|-------------------------|-------------------|
| `RomPilBot_1H`         | `RomPilBot_1H.common.ovl` | `0x69506D6F`            | `"omPi"`          |
| `RomPilTop_1H`         | `RomPilTop_1H.common.ovl` | `0x69506D6F`            | `"omPi"`          |
| `RomPil_1H`            | `RomPil_1H.common.ovl`    | `0x69506D6F`            | `"omPi"`          |
| `RomPil_4H`            | `RomPil_4H.common.ovl`    | `0x69506D6F`            | `"omPi"`          |

Four distinct resources across four distinct archives all resolve to exactly the same four bytes, and
those bytes decode as `"omPi"` — a substring of `"RomPil"`, i.e. a fragment of the resources' own
*names*. Other entries produced similarly implausible values, e.g. `ACAM` (`0x4341646C`, ASCII-ish),
`Buggy` (`0x79676775`), `Truck` (`0x574D4143`) — all consistent with reading into a name/string block
rather than the struct itself.

Some entry *names* were themselves corrupted (garbled/non-printable, e.g. `?4+??d?`),
meaning the corruption isn't limited to data pointers — the name pointer resolution can also be wrong.

Given this, a byte-offset `SvdFlags`/`SvdLodType` enum-coverage test cannot be trusted yet (tracked
separately as a follow-up once this is fixed); the integration test that exists today
(`SvdResources_AreReadable`) only checks that *some* non-empty bytes come back, not that they're the
*right* bytes.

## Where this lives in the code

All of the logic below is in `Ovl.ExtractResources()` (`OpenCobra/OVL/OVL.cs:299-369`) and its
helper `Ovl.ReadString()` (`OpenCobra/OVL/OVL.cs:371-391`).

### Suspect 1 — guessed symbol record stride

```csharp
var symbolSize = 0;
var blockOffset = 0;
if (symbolBlock.Size % 16 == 0) {
  symbolSize = 16;
}
else if (symbolBlock.Size > 4 && (symbolBlock.Size - 4) % 16 == 0) {
  symbolSize = 16;
  blockOffset = 4;
}
else if (symbolBlock.Size % 12 == 0) {
  symbolSize = 12;
}
else if (symbolBlock.Size > 4 && (symbolBlock.Size - 4) % 12 == 0) {
  symbolSize = 12;
  blockOffset = 4;
}
else continue;
```

The per-symbol record size (12 or 16 bytes) and whether there's a 4-byte header before the table are
both *inferred* from whether the total block size happens to divide evenly by 12 or 16 — checked in a
fixed priority order (16-no-offset, then 16-with-offset, then 12-no-offset, then 12-with-offset). A
block whose size is divisible by both 12 and 16 (true whenever size is a multiple of 48) will always be
treated as 16-byte stride, silently, even if the archive's actual layout is 12-byte. There is no
cross-check against the archive version, the loader header count, or the actual first few records
looking sane (e.g. that resolved name pointers land inside a name/string block). Any archive whose
symbol table happens to size-match the wrong branch will have every `namePtr`/`dataPtr` in it read at
the wrong stride, i.e. from the wrong byte offset — which is exactly the kind of off-by-N corruption
that would explain both name strings leaking into "data" reads and unrelated resources colliding on
the same decoded pointer.

### Suspect 2 — only the first symbol block per file-type slot is read

```csharp
if (blocks.Length <= 2 || blocks[2].Blocks.Count == 0) continue;
var symbolBlock = blocks[2].Blocks[0];
```

Only `blocks[2].Blocks[0]` (the first block at file-type-slot index 2) is ever treated as a symbol
table. If an archive has more than one block in that slot, the rest are silently ignored — any symbols
defined there are simply missing from `entries`, with no diagnostic.

### Suspect 3 — loader/tag walk desyncs silently and cannot recover

```csharp
if (loaderIdx < loaderHeaders.Length && loaderSymbolRemaining == 0) {
  loaderIdx = Math.Min(loaderIdx + 1, loaderHeaders.Length - 1);
  loaderSymbolRemaining = loaderHeaders[loaderIdx].SymbolCount;
}
```

`FileType` assignment for each symbol depends on walking `loaderHeaders` in lockstep with a
`SymbolCount` countdown, assuming symbols appear in the exact order the loader headers declare. If
`symbolSize`/`blockOffset` (Suspect 1) is wrong even once earlier in the same table, every subsequent
symbol read at the wrong byte offset — so `loaderSymbolRemaining` decrements against records that
aren't the ones the count was describing, and the countdown desyncs for the rest of the file. There is
no validation that the final `loaderIdx` reaches the end of `loaderHeaders` in a consistent state.
This is very likely why the observed corruption clusters by *file* (all of `RomPilBot_1H`,
`RomPilTop_1H`, etc. — separate archives, separate symbol tables) rather than by *entry*: once one
table's stride guess is wrong, everything downstream in that file inherits the corruption.

### Suspect 4 — `resolvedBlock` matched by address range with no origin check

```csharp
var resolvedBlock = allBlocks.FirstOrDefault(fb =>
  dataPtr >= fb.RelativeOffset && dataPtr < fb.RelativeOffset + fb.Size);
```

`dataPtr` is resolved purely by falling within *some* block's `[RelativeOffset, RelativeOffset+Size)`
range, chosen from `allBlocks` — the pooled set of every block from every processed file (`common.ovl`
**and** `unique.ovl`, see Suspect 5). If `dataPtr` was computed from a misaligned read (Suspect 1/3),
it's essentially a garbage 32-bit value; it can easily fall inside some unrelated block's valid range by
coincidence, especially in a codebase where `relocationOffset` accumulates without bound across many
blocks and files. There's no check that the resolved block's `TypeIndex`/origin actually makes sense
for the `FileType` being extracted (e.g. that an `svd` symbol's data actually lands in a block that
could plausibly hold `SceneryItemVisual` structs, vs. a string/name block).

### Suspect 5 — shared, ever-growing relocation address space across files

```csharp
private uint relocationOffset;
...
block.RelativeOffset = relocationOffset;
block.TypeIndex = i;
relocationOffset += block.Size;
```

`relocationOffset` is an instance field on `Ovl`, incremented across **every** block in **every**
file processed by `IngestArchive` — i.e. it is not reset between `common.ovl` and `unique.ovl`. Whether
the on-disk format truly defines one merged relocation address space spanning both files (plausible,
matching how OVL pairs are described elsewhere in this repo) or whether pointers are actually
file-relative and this conflates two independent address spaces has not been verified against the
original C++ implementation. If it's the latter, the same numeric `dataPtr` value could validly refer
to two completely different locations depending on which file's symbol table it came from, and this
code would silently pick whichever block in the pooled list happens to match first.

### Suspect 6 — `ReadString` has the same "first plausible match wins" issue

```csharp
private static string? ReadString(List<FileBlock> blocks, uint ptr) {
  foreach (var fb in blocks.Where(fb => fb.TypeIndex == 0)) {
    ...
  }
  foreach (var fb in blocks) { // falls back to ANY block, any type
    ...
  }
  return null;
}
```

If no `TypeIndex == 0` block claims the pointer, `ReadString` falls back to treating *any* block's raw
bytes as a null-terminated ASCII string — including binary struct data blocks. Given the corrupted
names observed in the SVD data (non-printable garbage), this fallback is very likely firing on pointers
that don't actually reference string data at all, producing whatever bytes happen to be at that offset
interpreted as text.

## Impact

- Any decoder or test built on top of `Ovl.ReadResource`/`Ovl.Keys` cannot currently be trusted to
  return the *correct* bytes for a given named resource — only that *some* bytes come back. This
  blocks writing reliable byte-level decoders (SVD, SID, and likely others) and blocks the
  enum-coverage verification originally requested for `SvdFlags`/`SvdLodType`
  (see `.agents/summaries/ovl-enum-verification.md` and the follow-up task filed from that work).
- The scope appears wider than SVD: the `nullbmp:ftx` workaround in `ExtractResources.cs` shows the
  exact same "name string leaks into data" symptom for Flexi-Textures, on a completely different file
  type and loader tag. This suggests the root cause is in the shared `ExtractResources()`/`ReadString()`
  machinery all file types funnel through, not something specific to SVD parsing.

## Suggested next steps

1. Cross-reference the symbol table layout against the original C++ `libOVL`/`libOVLng` sources
   (`D:\Users\enigm\GitHub\rct3-importer`) to replace the size-modulo guess (Suspect 1) with a
   version-driven, deterministic stride — the C++ loader presumably knows the exact struct size for
   each symbol-table format version rather than inferring it.
2. Add a sanity/consistency check after resolving `resolvedBlock`/`ReadString` — e.g. verify the
   loader-tag walk (Suspect 3) ends with `loaderIdx == loaderHeaders.Length - 1` and
   `loaderSymbolRemaining == 0`, and log/throw instead of silently continuing when it doesn't.
3. Verify whether `relocationOffset` (Suspect 5) is genuinely meant to span both `common.ovl` and
   `unique.ovl` in one address space, by checking the original loader's relocation-fixup logic.
4. Once corrected, re-enable a byte-offset validation test (the `SvdFlags` coverage check attempted in
   `EnumCoverage.cs` before it was scoped back) as a regression guard — real game data is an effective
   fuzz corpus for this, as shown above.

## How this was found

While adding a real-data integration test for `SvdFlags` enum coverage
(see `.agents/summaries/ovl-enum-verification.md`), the test's very first run against the full RCT3 asset
library (12k+ SVD resources, via the `RCT3_PATH` env var) immediately produced thousands of "undocumented
flag bit" failures. Investigating a sample by hand (decoding the "flag" bytes as ASCII) revealed they
were fragments of the resources' own names rather than real flag data — the enum wasn't missing bits;
the resolver was returning the wrong bytes.

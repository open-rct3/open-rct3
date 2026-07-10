# Plan: Fix `tex`/`flic`/`btbl` symbol-resolution failures

## What changed since the last version of this plan

The previous version of this plan (Steps 1-4 below, in git history) was written against
Part 3 of the bug doc, which theorized `OVL.cs`'s `ReadLoaderExtraData` needed a full
per-loader-type rewrite ("Root cause A"). An independently-developed, **working** Rust OVL
decoder was supplied for cross-reference (`assets/reference/ovl/`) and reconciled against
the original C++ dumper (`rct3tex.cpp`) in bug doc Part 6. That reconciliation found:

- **`ReadLoaderExtraData` was never the bug.** It already parses the per-entry
  `LoaderStruct` array correctly (right struct layout, right v5 masking) and its generic
  2-chunk model for `btbl` matches the reference exactly once the C++'s inlined `fread`
  calls are decomposed. **Do not rewrite it.** This plan's old Steps 1-4 are superseded —
  don't implement them.
- **The real classification bug** is that `ExtractResources` reconstructs, by guessing
  (`SymbolCount` countdown), a symbol→loader-category mapping that's already an explicit
  on-disk field (`LoaderStruct.LoaderType`, a direct array index — no guessing needed).
- **The real pixel-data bug** is that `TextureDecoding.ReadTexture` chases the wrong Tex
  struct field (`Ts2Ptr`, offset 56) instead of the one the reference dumper's own
  texture-saving code actually uses (`FlicPtr`, offset 52 — a *double* pointer, needing two
  chained relocation-table hops to resolve). Both hops depend on a relocation table that
  `OVL.cs` currently discards outright (`SkipRelocations` — "Root cause B", still unfixed).
- `mms`/`prt`/`psi`/`fct` are now conclusively confirmed out of scope (the reference solves
  `tex`/`ftx` and explicitly no-ops these four tags) — this was already suspected (Part 5)
  and is now confirmed independently (Part 6 Finding 5). `CharacterSkins.cs`/
  `ParticleEffects.cs` are not touched by this plan.

Read bug doc Part 6 in full before implementing — it has the exact C++ line numbers and
Rust snippets behind every claim below.

## Lessons learned that still hold (from the previous version of this plan)

### 1. Domain logic belongs in `Files/`, not `OVL.cs`

`OVL.cs` is the generic archive parser. Anything that knows what a `btbl`/`flic`/`tex`
loader's fields *mean* (as opposed to how the generic loader-table/extra-data/relocation
machinery is shaped) belongs in `OpenCobra/OVL/Files/TextureDecoding.cs` or a new sibling.
This still applies to the new work below: `Ovl` should expose a relocation-lookup primitive
and the parsed `LoaderStruct` entries as generic data; the Tex→Flic double-hop chase and
the "current btbl" loader-order walk are texture-decoding domain knowledge and belong in
`Files/`.

### 2. Fixture coverage is real but narrow

The 7 custom-scenery fixtures (5 textures decode on baseline) don't exercise the
`mms`/`fct`-adjacent patterns this bug is actually about. They're a regression check
("still decodes 5"), not a progress check. Progress is measured against Part 4's 37-file
list in a real `RCT3_PATH` install — `Main.common.ovl` (0/84 `Texture` entries decoding,
largest blast radius) and `Characters/AF/AF01_Body_Main.common.ovl` (has the one
non-`mms`/`prt` `tex` entry that's genuinely in scope, per Part 5) are the two best
single-file repros. A full 7,490-file scan takes ~5 minutes — use it only as the final
regression check, not the iteration loop.

### 3. `mms`/`prt`/`psi`/`fct` are out of scope (bug doc Parts 5 and 6 Finding 5)

Do not chase "fixes" that try to make these tags decode as textures through this pipeline.
`CharacterSkins.cs`/`ParticleEffects.cs` are separate, pre-existing modules with their own
open (and different) problem — their premise, not their resolution logic, is wrong. Leave
them alone; flag the premise issue separately if picked up later.

### 4. Suspect 4 (string-block drop) is independent and small

A `dataPtr` that lands inside `TypeIndex == 0` (the string block) is an unfixed-up
placeholder. Dropping it silently in `ExtractResources` is correct and low-risk — add it
with no fanfare, independent of everything else in this plan.

## The plan

### Step 0: Add a measurement test

Before any change, lock in the current decode counts with a measurement test in
`OpenCobra/Tests/OVL/TexturesMeasurementTests.cs` (tagged `[Category("Measurement")]` so it
doesn't run in the default `make test` flow). It loads each `*.common.ovl` embedded
fixture, runs `Textures.Extract`, and writes per-fixture counts to a log file. Baseline:
**5 textures** across 7 fixtures. This is a regression check, not a success metric — after
every change below, this number must still be 5.

### Step 1: Confirm Finding 1 empirically before changing `ExtractResources`

Bug doc Part 6 Finding 1 (direct `LoaderStruct.LoaderType` index, no positional walk
needed) was cross-referenced against the C++ source but **not re-verified against real
data** in the session that wrote it. Before
rewriting `ExtractResources`, recreate a small diagnostic (a `[Test]` in
`OpenCobra/Tests/Integration/`, `RCT3_PATH`-gated, using reflection to read `Ovl`'s private
`allFileTypeBlocks`/`allLoaderHeaders` fields — no production code changes needed for this
step) that, for `Main.common.ovl`:

1. Parses `blocks[2].Blocks[1]` as `LoaderStruct[]` (20 bytes/entry: `LoaderType` at +0,
   `data` at +4, `HasExtraData` at +8, `Sym` at +12, `SymbolsToResolve` at +16).
2. Tallies `loaderHeaders[entry.LoaderType].Tag` across all entries.
3. Compares that tally against `ovl.Keys`'s current `FileType` tally (from the existing
   name-suffix + positional-walk classification).

If the direct-index tally is sane (e.g. mostly `tex`, some `fct`, matching the known
84-`Texture`+6-`FontCharacterTable` shape of this file) and differs from the current
positional-walk tally, that confirms Finding 1 and justifies Step 3 below. If it doesn't
line up (e.g. `LoaderType` values are frequently out of range, or the tally looks
nonsensical), stop and re-open Part 6 Finding 1 rather than proceeding on a
wrong premise — the rest of this plan depends on it.

### Step 2: Parse the relocation-fixup table for real (Root cause B / Finding 3)

**File:** `OpenCobra/OVL/OVL.cs`, replacing `SkipRelocations`.

Per `rct3tex.cpp:1830-1842`'s `DoReloc` fixup loop, for each of the `relCount` source
addresses in the table:

1. Resolve the source address to a block + offset (the existing flat-address lookup logic,
   e.g. reuse/extract what `TryResolveRelocation` already does for this part).
2. Read the raw `u32` currently stored there (the on-disk placeholder value).
3. Record `source address → raw value` in a new dictionary.

This is a **gated raw-value lookup**, not a value transformation: it does not try to
further resolve the raw value into anything (that's the caller's job, one hop at a time —
see Step 3). Expose it as something like:

```csharp
public bool TryGetRelocationSource(uint address, out uint rawValue)
```

Rename `SkipRelocations` (it no longer skips) and keep its existing byte-accounting
(`relCount` reads) intact — this method must still consume exactly the same number of bytes
from the stream as before, it just also records what it reads instead of discarding it.

This step is purely additive: nothing currently reads relocation data, so nothing can
regress from adding the capability. Steps 3 and 5 are what consume it.

### Step 3: Read `LoaderStruct.LoaderType`/`.Sym` directly in `ExtractResources`

**File:** `OpenCobra/OVL/OVL.cs`, `ExtractResources()` and `ReadLoaderExtraData()`.

Only proceed once Step 1 confirms the premise. `ReadLoaderExtraData` (`OVL.cs:298-327`) is
the C# equivalent of the Rust reference's `entries`/`OvlLoaderEntry` builder — it already
walks `blocks[2].Blocks[1]` as `LoaderStruct[]` in on-disk order, reading `LoaderType`,
`data` (dataPtr), and `HasExtraData` per entry — but currently discards `LoaderType` and
`Sym` after using them locally, returning only the `dataPtr → chunks` dictionary. Extend it
to also return, in the same pass, an ordered list of per-entry `(LoaderType, dataPtr, Sym)`
(a small record type, e.g. `LoaderEntry`) alongside the existing chunk dictionary — this
becomes the shared source both `ExtractResources` (this step) and Step 5's loader-order index
read from, instead of duplicating the 20-byte offsets a third time. Replace the
`loaderHeaders`/`SymbolCount` positional-walk fallback (`OVL.cs:478-511`) with a lookup into
this list. For each entry:

- `LoaderType` is a direct index into `loaderHeaders` — use `loaderHeaders[entry.LoaderType]`
  directly instead of walking a countdown.
- `Sym` (offset 12) needs one relocation hop (`TryGetRelocationSource`, Step 2) to resolve to
  the owning `SymbolStruct2`'s address; from there the existing name-pointer read gives the
  exact symbol this loader entry belongs to — a direct pairing, not a positional guess.

Keep the existing name-suffix (`rawName[(colonIndex+1)..].ToFileType()`) path as the
*primary* classification source where present (it's simpler and already works); only replace
the *fallback* path with this direct-index lookup, per bug doc Part 6 Finding 1.

### Step 4: Fix `TextureDecoding.ReadTexture`'s Tex→Flic chain (Finding 2)

**File:** `OpenCobra/OVL/Files/TextureDecoding.cs`.

Depends on Step 2. Replace the current chain:

```csharp
if (!ovl.TryResolveRelocation(tex.Ts2Ptr, out var ts2Block, out var ts2Offset)) return null;
var flicLoaderPtr = BitConverter.ToUInt32(ts2Block, ts2Offset + 4);
if (!ovl.TryReadExtraData(flicLoaderPtr, out var chunks) || chunks.Count == 0) return null;
```

with the double relocation-hop chase on `tex.FlicPtr` (offset 52, **not** `Ts2Ptr`):

```csharp
if (!ovl.TryGetRelocationSource(texAddress + 52, out var flicSlot)) return null; // hop 1
if (!ovl.TryGetRelocationSource(flicSlot, out var flicAddr)) return null;        // hop 2
```

`flicAddr` should then match a `flic`-category loader entry's (already relocation-resolved,
one hop — its `data` field is a single pointer, not a double one) `data` address — look this
up via the new loader-order index from Step 5, not `TryReadExtraData` keyed by a possibly
unfixed-up value. `Ts2Ptr`/`TextureStruct2` are no longer read anywhere in this path; the
`Tex` struct's `Ts2Ptr` field and its doc comment should be updated to reflect that it's not
part of the pixel-data chain (or removed if nothing else uses it).

`texAddress` here is the resolved symbol's own data pointer — the same value already
available wherever `ReadTexture` is called from (`Textures.Extract`/`CharacterSkins.Extract`
already have the `OvlFile`; threading its `dataPtr` through is a small signature change).

### Step 5: Build a loader-order BTBL↔FLIC index (Finding 4)

**File:** `OpenCobra/OVL/Files/TextureDecoding.cs` (new type, e.g. `LoaderTextureIndex`).

Walk the parsed `LoaderStruct[]` entries (same array as Step 3, in on-disk order) once per
side (common/unique), tracking the most recently seen `btbl`-category entry:

- On a `btbl` entry: decode it (existing `ReadBitmapTable` logic, keyed by this entry's
  relocation-resolved `data` address) and remember it as "current".
- On a `flic` entry: read its one extra-data chunk (existing generic chunk read — unchanged,
  per Finding 1) as a 4-byte index into "current"'s table; record
  `flic entry's resolved data address → (current btbl, index)`.

Replace `Textures.Extract`'s per-OVL-file `bitmapTables[fileData.OvlName]` keying with a
lookup into this index by the `flicAddr` resolved in Step 4, instead of assuming one BTBL
per file.

### Step 6: Add Suspect 4 origin check in `ExtractResources`

**File:** `OpenCobra/OVL/OVL.cs`, `ExtractResources()`.

When a resolved data pointer's block has `TypeIndex == 0` (the string block), drop the
symbol silently. Real resource data never lives in the string block; a `dataPtr` landing
there is an unfixed-up placeholder. Independent of Steps 1-5, low risk, no fixture impact.

### Step 7: Verify

In order:

1. `dotnet test OpenCobra/Tests/Tests.csproj --filter 'Category=Measurement'` → must
   report **5** textures decoded (no regression from Step 0's baseline).
2. `make test` → all existing unit tests still pass.
3. Against `RCT3_PATH`: re-run Step 1's diagnostic (now validating the *implemented*
   `ExtractResources` change, not just the raw tally) against `Main.common.ovl` and confirm
   at least some of its 84 `Texture` entries now decode (baseline 0/84).
4. Against `RCT3_PATH`: load `Characters/AF/AF01_Body_Main.common.ovl` and confirm the one
   genuine (non-`mms`/`prt`) `tex` entry in the file decodes (per bug doc Part 5's
   "what's still real and still in scope" note — do not expect the `mms`/`prt` entries
   themselves to decode; that's a separate, out-of-scope problem per Finding 5).
5. *Only if* both single-file repros show improvement, run the full 7,490-file scan once as
   the final regression check.

## Estimated complexity

- Step 0: trivial.
- Step 1: small — a read-only diagnostic, no production changes.
- Step 2: small-medium — the address resolution logic already exists (`TryResolveRelocation`);
  this reframes it as a gated raw-value lookup fed by the relocation table instead of
  guessing a value is already a flat address.
- Step 3: medium — touches `ExtractResources`'s core loop; needs the shared `LoaderStruct`
  reader factored out of `ReadLoaderExtraData` first to avoid a third copy of the 20-byte
  offsets.
- Step 4: small — a direct, well-evidenced field swap plus the two-hop chase built on Step 2.
- Step 5: medium — new type, but each piece (btbl decode, flic chunk read) reuses existing
  logic; the new part is the ordered walk and the address-keyed lookup.
- Step 6: trivial.
- Step 7: long-running (5 min for the full scan) but only the final pass.

## What this plan does NOT do

- Does not rewrite `ReadLoaderExtraData` (bug doc Part 6 Finding 1 — it's already correct).
- Does not touch `mms`/`prt`/`psi` decoding or `CharacterSkins.cs`/`ParticleEffects.cs`
  (out of scope per bug doc Part 5 and Part 6 Finding 5).
- Does not add new `RCT3_PATH` fixtures (copyright-blocked per Part 4).
- Does not attempt to fix the `fct` font data itself — only the classification/desync side
  effects on neighboring `tex` loaders, which Steps 3 and the relocation fix address as a
  side effect of reading the correct on-disk fields rather than guessing.

# Decode TerrainType (TER) Entries

**Roadmap**: Phase 1, "Render fluctuating terrain"

**See also**:
- [`grass-from-ovl.md`](../../../research/grass-from-ovl.md) — research on `Terrain_RCT3.*.ovl`'s 32
  `TerrainTypeEntry` entries (`Terrain_00`..`Terrain_25` + `Cliff_00`..`Cliff_05`; a `Terrain_CT.*.ovl` pair
  with 6 more, `Terrain_26`-`31`, was found while writing this plan — see "Production OVLs" below) and the
  `type` field (Ground Unblended/Cliff/Ground Blended), already consumed by
  [`features/terrain-heightmap.md`](../terrain-heightmap.md)'s `SurfaceIndex`/`CliffIndex` corner storage. That
  research covers *what the data means* and already reverse engineered the exact 60-byte struct layout below;
  this plan owns actually parsing `ter` bytes into a `TerrainTypeEntry` record — not yet implemented (no
  `TerrainTypes.cs` exists).
- [`completed-work/grass-from-ovl.md`](../../../summaries/completed-work/grass-from-ovl.md) — the landed grass
  ground-plane work. It hard-codes `Terrain_06` as grass ("confirmed by eye" against reference PNGs, no
  authoritative source) — that guess is exactly the gap this plan closes: once `ter` entries decode,
  `TerrainTypeEntry.TextureRef` + `Type`/`ColourSimple` give an authoritative way to identify grass instead of
  an eyeballed name.

## Problem

TER entries define terrain types with color parameters, texture references, and display metadata. Each terrain
type has a description name, icon, texture reference, and rendering parameters. This plan decodes `ter` entries
into a `TerrainTypeEntry` record (with a `TerrainType` enum for its `type` field), then wires the result into
`Terrain.Load()` (currently hard-coded to `Terrain_06`) and `terrain-heightmap.md`'s
`SurfaceIndex`/`CliffIndex` consumer.

## Background Research

**TER Manager** (`ManagerTER.h/cpp`, reference in `rct3-importer`'s `terraintype.h`):

- Tag: `"ter"`, Name: `"TerrainType"`
- **Correction to the research doc's "unique OVL only" claim, now confirmed disproven**: `ter`-tagged symbols
  are classified in *both* `Terrain_RCT3.common.ovl` and `Terrain_RCT3.unique.ovl` (same 32 names in each), and
  a byte-level comparison (`ReadResource` on a same-named symbol in each file) confirmed the data itself is
  identical between the pair, not just the symbol names — see Data Layout below.
- Struct is 60 bytes total, confirmed layout (offsets from `grass-from-ovl.md`):

| Offset | Field | Type | Notes |
|--------|-------|------|-------|
| 0 | `Version` | u32 | structure version, always 1 |
| 4 | `Unk02` | u32 | always 0 |
| 8 | `Addon` | u32 | 0=Vanilla, 1=Soaked, 2=Wild |
| 12 | `Number` | u32 | terrain index |
| 16 | `Type` | u32 | 0=Ground Unblended, 1=Cliff, 2=Ground Blended |
| 20 | `TextureRef` | u32 (relocated) | `TextureStruct*` → a `tex` entry |
| 24 | `DescriptionName` | u32 (relocated) | `char*` → `txt` in localization OVLs |
| 28 | `IconName` | u32 (relocated) | `char*` → `gsi` in Main OVL |
| 32 | `ColourSimple` | u32 | small terrain colour (editor swatch), default `0xFFFF007F` |
| 36 | `ColourMap` | u32 | map-overlay colour |
| 40 | `InvWidth` | f32 | 0.25 = 1 tile wide |
| 44 | `InvHeight` | f32 | inverse height of texture |
| 48 | `Unk13` | f32 | default 0.3 |
| 52 | `Unk14` | f32 | default 0.0 |
| 56 | `Unk15` | f32 | default 0.5 |

- `Name` is the symbol's own OVL name (e.g. `"Terrain_06"`), not a struct field — same pattern as
  `Textures.cs`/`StaticShapes.cs`, where the loader's symbol name *is* the resource name.
- `ColourSimple`/`ColourMap` are only meaningful for the editor UI (swatch, minimap), not the rendered surface —
  the actual appearance comes from the decoded `TextureRef` texture (see `completed-work/grass-from-ovl.md`).

### Unknown fields — investigation result

Ran a scratch scanner (per the OVL scratch scanner pattern) against the real
`terrain/RCT3/Terrain_RCT3.unique.ovl` (32 entries: `Cliff_00`..`05`, `Terrain_00`..`25`), dumping all fields
including the four unknowns. Actual values, not guesses:

- **`Unk02`**: `0` for all 32 entries, no exception. Genuinely constant — likely reserved/unused by this
  manager. Decode as opaque, don't interpret further.
- **`Unk13`/`Unk14`/`Unk15`**: **not constant** — they vary meaningfully by entry and cluster by apparent
  terrain "family":
  - All 6 `Cliff_*` entries: `Unk13=0.3, Unk14=1, Unk15=0.5` — identical across every cliff, unlike the ground
    entries below.
  - Ground entries split into rough bands by `Unk13` (`0.02`, `0.1`, `0.3`, `0.5`, `0.7`) that loosely track
    visual "roughness": the darkest/smoothest-looking rock entries (`Terrain_23`/`24`/`25`, dark grey) sit at
    `Unk13=0.02`; sand/dirt-toned entries (`Terrain_00`-`03`, `09`, `12`) sit at `Unk13=0.1`; grass-toned
    entries (`Terrain_04`-`08`, `10`, `11`) sit at `Unk13=0.3`; visibly rocky/mountainous entries
    (`Terrain_13`/`14`/`17`/`18`/`21`/`22`) sit at `Unk13=0.5`-`0.7`.
  - `Unk14` ranges continuously from `-1` to `4`. Lighter-colored entries (pale grey/white — `Terrain_17`,
    `19`-`22`, `24`, `25`; plausibly snow/ice/light-rock) cluster at the high end (`2.5`-`4`); darker
    brown/grey entries (`Terrain_02`, `03`, `15`, `16`, `23`) cluster negative (`-1` to `-0.3`). This is
    consistent with — but does **not confirm** — `Unk14` being an altitude-band weight for RCT3's known
    automatic "snow above N meters" terrain-painting behavior, since the lighter/likely-snow entries are the
    ones with the highest values.
    **Weakened by `Terrain_CT` data** (scanned in a follow-up pass): `Terrain_31`, a near-black entry
    (`ColourSimple=0xFF3A3A34`), has `Unk14=4` — the same "high" value the snow/light-color hypothesis predicts
    only for pale entries. The other 5 `Terrain_CT` entries (`26`-`30`, all `Addon=1`/Soaked) sit at `Unk14=0`
    regardless of color. The altitude-band-weight reading no longer fits the full dataset; treat it as one
    disproven guess among several, not a leading hypothesis. `Unk13`'s roughness-family clustering still holds
    for `Terrain_CT` (`0.02` for the two brownish `type=0` entries `27`/`28`, `0.3` for the rest, consistent
    with the `Terrain_RCT3` pattern).
  - `Unk15` ranges `-0.5` to `1`, no clear independent pattern beyond loosely tracking `Unk14`'s sign.
  - `InvWidth`/`InvHeight` (the *documented* parameters, for contrast) are ~`0.1` for literally every entry,
    confirming they're a fixed texture-tiling constant here, not something that varies per terrain — the
    variance really is concentrated in the "unknown" fields, not spread evenly.

**Conclusion**: `Unk13`-`15` are very likely real per-terrain rendering parameters (candidate: a blend-noise
scale + altitude-band weight pair used by the "Ground Blended" (`type=2`) auto-paint/blend system), not padding
— confirmed only for `Unk02` (constant, ignorable). Decoding them as opaque `TerrainUnknowns` fields remains the
right scope for *this* plan (no rendering system exists yet to consume an altitude-weight), but do not assume
they're dead weight — flag them for whoever implements Blended-rendering/auto-paint-by-height in
`terrain-heightmap.md`'s deferred "Blended rendering" item, since `Unk14` is a plausible input to that system.

**Data Layout**:

- Symbol references to: TXT (description), GSI (icon), TEX (texture)
- **`ReadResource(file)` returns more than the 60-byte struct** — confirmed by the scratch scanner: entry sizes
  varied wildly (`Terrain_00`: 1920 bytes, `Terrain_06`: 1560, `Cliff_00`: 360, `Terrain_25`: 420), not a fixed
  60. This matches the "string table entries for name/description/icon/texture" the reference docs mention —
  trailing string data of variable length follows the fixed struct in the same resource block. Decoders must
  read exactly the first 60 bytes for the struct fields and not assume `entry.Size == 60` or otherwise depend
  on the resource's total length.
- **Common vs unique data is byte-identical, not "unique OVL only"**: `TryResolveRelocation`/`ReadResource`
  comparison of `Terrain_00`, `Terrain_06`, `Cliff_00`, `Terrain_25` between `Terrain_RCT3.common.ovl` and
  `Terrain_RCT3.unique.ovl` found identical bytes in every sampled entry. The research doc's "unique OVL only"
  claim is now disproven, not just unconfirmed — `ter` data is fully duplicated across the common/unique pair.
  `TerrainTypes.Extract(Ovl)` only needs to run against whichever single archive of a pair is loaded (either
  gives the same result); no special dual-file handling is needed.

## Production OVLs with `ter` entries (confirmed)

Scanned every `*.ovl` under the install's `terrain/` folder (4 files — cheap, no need for a slower full-install
sweep since `ter` is terrain-specific content, not something scattered across unrelated archives):

| Archive | `ter` entries |
|---|---|
| `terrain/RCT3/Terrain_RCT3.common.ovl` | 32: `Cliff_00`-`05`, `Terrain_00`-`25` |
| `terrain/RCT3/Terrain_RCT3.unique.ovl` | same 32, byte-identical data (see "unique OVL only" correction above) |
| `terrain/CT/Terrain_CT.common.ovl` | 6: `Terrain_26`-`31`, all `Addon=1`/Soaked (previously uncatalogued — not mentioned in `grass-from-ovl.md`) |
| `terrain/CT/Terrain_CT.unique.ovl` | same 6, byte-identical data |

`Terrain_CT`'s full field dump (scanned in a follow-up pass, not just entry names): `Terrain_27`/`28` are
`Type=GroundUnblended` (brownish, `InvWidth`/`InvHeight=0.25` — the one deviation from every `Terrain_RCT3`
entry's `~0.1`); `Terrain_26`/`29`/`30`/`31` are `Type=GroundBlended`. No `Cliff`-type entries in this pack.
See the "Unknown fields" section above for what this data did to the `Unk14` hypothesis.

`Terrain_CT` ("CT" — likely a Complete-Edition-bundled content pack, name not yet decoded from any TXT/GSI
reference) extends the numbering scheme `Terrain_00`-`31` past the 26 entries `grass-from-ovl.md` originally
found. **This plan's decoder and tests must not assume 32 is the total entry count** — `TerrainTypes.Extract`
needs to run against whichever archive(s) a caller loads (each single archive already has the complete,
byte-identical `ter` set for its pack — see the common/unique finding above) rather than hard-coding the
`Terrain_RCT3` pair, and the "every entry decodes to a non-empty name" test needs a fixture/assertion that
covers `Terrain_CT` too, not just `Terrain_RCT3`.

**Known test files with no TER entries**: `style.common.ovl`, `style.unique.ovl`.

## Solution Architecture

### New File: `OpenCobra/OVL/Files/TerrainTypes.cs`

Mirrors `StaticShapes.cs`'s style (`readonly record struct`, `Extract(Ovl ovl)` static entry point,
`ConcurrentBag`/`Parallel.ForEach` + failure tracking).

**Naming note**: the struct's `type` field (0=Ground Unblended, 1=Cliff, 2=Ground Blended) becomes its own
named enum, `TerrainType`, matching the codebase's existing convention (`SvdLodType`, `SidType` in `Enums.cs`).
That reuses the name the manager itself is called (`"TerrainType"` in `ManagerTER`), so the *entry* record —
what the plan's `See also`/Problem sections call "a `TerrainType`" throughout — is renamed to
`TerrainTypeEntry` to avoid a collision. `TerrainTypeEntry` is still the one 60-byte struct this plan decodes;
`TerrainType` is now just the enum for its `Type` field.

```csharp
public enum TerrainType : uint {
  GroundUnblended = 0,
  Cliff = 1,
  GroundBlended = 2
}

public readonly record struct TerrainParameters(
  uint ColourSimple,  // default 0xFFFF007F
  uint ColourMap,     // default 0xFFFF007F
  float InvWidth,     // default 0.1
  float InvHeight     // default 0.1
);

public readonly record struct TerrainUnknowns(
  /// <summary>Always 0 across all 38 observed entries (Terrain_RCT3 + Terrain_CT). Likely reserved/unused.</summary>
  uint Unk02,
  /// <remarks>
  /// Varies by entry, clustered by apparent visual "roughness" family (0.02 darkest/smoothest rock,
  /// 0.1 sand/dirt, 0.3 grass, 0.5-0.7 rocky/mountainous). All 6 Cliff_* entries share 0.3. Speculative:
  /// a blend-noise scale for the Ground Blended (<see cref="TerrainType.GroundBlended"/>) auto-paint system.
  /// Not confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk13,
  /// <remarks>
  /// Ranges -1..4. Lighter/likely-snow-or-ice entries cluster high (2.5-4); darker brown/grey entries
  /// cluster negative. Speculative: an altitude-band weight RCT3's known auto-snow-above-height terrain
  /// painting might read. Not confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk14,
  /// <remarks>
  /// Ranges -0.5..1, loosely tracks <see cref="Unk14"/>'s sign. No independent pattern found. Not
  /// confirmed — see ovl-terrain-types.md's "Unknown fields" section.
  /// </remarks>
  float Unk15
);

public readonly record struct TerrainTypeEntry(
  string Name,
  string? DescriptionName,  // resolved TXT reference
  string? IconName,         // resolved GSI reference
  string? TextureRef,       // resolved TEX reference (symbol name, matches Textures.cs naming)
  uint Version,
  uint Addon,
  uint Number,
  TerrainType Type,
  TerrainParameters Parameters,
  TerrainUnknowns Unknowns
);

public static class TerrainTypes {
  public static IReadOnlyList<TerrainTypeEntry> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `file.Type == FileType.TerrainType` — this enum member already exists in `FileTypes.cs`
   (tag `"ter"`, mapped both directions), no new enum work needed. Not unique-OVL-only in practice (see the
   "Correction" note above, now confirmed) — `Extract(ovl)` operates on whichever single archive is passed in;
   since common/unique hold byte-identical `ter` data, callers don't need to load and merge both.
2. Parse the first 60 bytes of `ovl.ReadResource(file)` per the offset table above — the resource itself is
   larger (variable-length trailing string-table data, see Data Layout above), so read fixed offsets, don't
   read/assume the whole buffer is the struct. Map the raw `type` u32 to the new `TerrainType` enum. Confirmed
   against the real archives: `Version` is always `1`; `Addon` is `0` (Vanilla) for all `Terrain_RCT3` entries
   and `1` (Soaked) for all `Terrain_CT` entries; `Number` matches each entry's position (`0`-`5` for `Cliff_*`,
   `0`-`31` across `Terrain_RCT3`+`Terrain_CT`) — these can be asserted as invariants in tests. `Type` is
   `Cliff` for all `Cliff_*`; for `Terrain_*` it's a mix of `GroundUnblended` (`Terrain_04`, `05`, `23`, `24`,
   `25`, `27`, `28`) and `GroundBlended` (everything else); no `Terrain_*` entry was `Cliff`.
3. Resolve `TextureRef`/`DescriptionName`/`IconName` relocated pointers to their symbol names — same relocation
   resolution `Textures.cs`/`StaticShapes.cs` already use (`ovl.TryGetDataPointer`/relocation table lookup);
   nullable because description/icon references may not resolve in every archive (e.g. missing localization
   OVL).
4. Return `IReadOnlyList<TerrainTypeEntry>` from `Extract`.

### Integration Wiring (in scope for this plan)

- **`OpenRCT3/Simulation/Terrain.cs::Load`**: replace the `Contains("Terrain_06")` hard-coded lookup
  (`completed-work/grass-from-ovl.md`) with a decoded-metadata-driven filter — **caution**: the scratch-scanner
  run above found `Terrain_06`'s actual `ColourSimple` is `0xFF487D10`, not the `0xFF4F810E` the research doc
  assumed from `Color.FromArgb(79, 129, 14)` (they're close but not equal — likely the research doc's swatch
  read was approximate, or `ColourSimple` isn't meant to match the rendered texture's average color exactly, as
  the struct notes already hint it's "only meaningful for the editor UI"). An exact-equality filter would
  therefore select **zero** entries and silently regress `Terrain.Load()`. Filter to `Type ==
  TerrainType.GroundBlended` first (`Terrain_06`'s actual value per the scan, not `GroundUnblended` as
  originally assumed) — this alone narrows 32 entries to ~21 candidates but doesn't uniquely pick grass, since
  most `Terrain_*` entries share it — then take the nearest-color match against `0xFF4F810E` within that
  filtered set, and assert in the test (below) that the nearest
  match is `Terrain_06` specifically, so a future regression here fails loudly instead of picking a different
  green entry silently.
- **`terrain-heightmap.md`'s `TerrainCorner.SurfaceIndex`/`CliffIndex`**: these are already-scoped byte indices
  into the `ter` entries (`Terrain_00..25`/`Cliff_00..05` from `Terrain_RCT3`, `Terrain_26..31` from
  `Terrain_CT` if that pack is loaded). Add `TerrainTypeEntry[] TerrainTypesByNumber`, a flat array sized to
  `max(Number) + 1` and indexed directly by `TerrainTypeEntry.Number` — O(1) lookup, matching
  `SurfaceIndex`/`CliffIndex` already being plain byte indices by design in `terrain-heightmap.md` (not a
  sparse/dictionary-shaped key space; `Number` values observed so far are fully contiguous `0`-`31` across both
  packs). This plan only adds the lookup table; the renderer that actually samples a per-corner texture from it
  is out of scope (same "storage/lookup only" boundary `terrain-heightmap.md` already draws around blended
  rendering).

### Dumper Plugin: `ter-viewer` (required, per `ovl/README.md`'s Dumper Plugin Requirement)

`plugins/README.md` already lists `ter-viewer` as the top-priority planned plugin ("Easy" complexity). This
plan is not complete without it — it's the plugin that graduates it from `📋 Planned` to `✅ Completed` in that
table.

- **`name()`/`version()`/`file_types()`**: `"Terrain Type Viewer"`, `"0.1.0"`, `'["ter"]'`.
- **`render(bytes)` — metadata table**: the 60-byte `TerrainTypeEntry` struct is entirely inline (no
  pointer-chasing needed for `Version`/`Addon`/`Number`/`Type`/`ColourSimple`/`ColourMap`/`InvWidth`/`InvHeight`/
  the three unknowns) — decode directly from `bytes` in AssemblyScript using `readU32LE`/`readF32LE` (from
  `plugins/lib/binaryReader.ts`, already used by `shs-viewer`) at the offsets in the struct-layout table above.
  Render `Type` as its enum name (`GroundUnblended`/`Cliff`/`GroundBlended`), `ColourSimple`/`ColourMap` as CSS
  swatches (`<div style="background:#RRGGBB">`) alongside the hex value — this is the one field this plugin can
  show more usefully than a bare number, since it's a color.
- **`TextureRef`/`DescriptionName`/`IconName` — resolved via host functions, not reimplemented struct-walking**:
  same pattern as `shs-viewer`'s `ftx_ref`/`txs_ref` resolution — `Ovl.currentResourceAddress()` +
  `Ovl.getRelocationSource(addr + 20)` (the `TextureRef` field offset) + `Ovl.findSymbol(...)` to get the `tex`
  symbol's name, and the same for `DescriptionName`/`IconName` at offsets 24/28. This mirrors the plan's own
  "don't reinterpret struct layouts in-plugin" boundary from `ovl/README.md`'s Dumper Plugin Requirement —
  relocation/pointer semantics stay behind the existing `resolve_pointer`/`get_relocation_source`/`find_symbol`
  host functions, this plugin doesn't add new ones for that part.
- **Texture preview: out of scope for this plan** — see Deferred/Future Work below. `ter-viewer` shows the
  resolved `tex` symbol's *name* only (via `find_symbol`, above), not decoded pixels.
- Falls back to `renderHexView(data, 0)` at the end, matching every other viewer's convention.

### Files to Create/Modify

**Create:**

- `OpenCobra/OVL/Files/TerrainTypes.cs`
- `OpenCobra/Tests/OVL/TerrainTypesTests.cs`
- `plugins/ter-viewer/index.ts`, `package.json`, `asconfig.json` (+ `index.test.ts`, matching
  `shs-viewer`/`mam-viewer`'s per-plugin test convention)

**Modify:**

- `OpenRCT3/Simulation/Terrain.cs` — replace `Terrain_06` string match with decoded `ter` lookup
- `OpenCobra/Tests/Integration/IngestionTests.cs` — extend/add a `RCT3_PATH`-gated test asserting decoded
  `ter` entries against the real archive
- `plugins/README.md` — move `ter-viewer` from `📋 Planned` to `✅ Completed` on completion (see
  Post-Implementation Steps)

### Dependencies

- Existing relocation resolution (same mechanism `Textures.cs`/`StaticShapes.cs` use)
- Symbol reference resolution for TXT, GSI, TEX

### Regression Prevention

- No changes to `Ovl.cs`
- Run `make test` before/after implementation; `IngestionTests.cs`'s `LoadTerrainTexture_Succeeds` (grass
  texture) must keep passing once `Terrain.Load()` is rewired off the `Terrain_06` string match

### Testing Strategy

NUnit tests in `OpenCobra/Tests/OVL/TerrainTypesTests.cs`, plus a `RCT3_PATH`-gated real-archive check in
`OpenCobra/Tests/Integration/IngestionTests.cs` (mirroring how `TexturesTests`/`IngestionTests` are structured
today — see `completed-work/grass-from-ovl.md`'s testing section for the live pattern). Cover:

- Synthetic-struct decode of a single 60-byte `TerrainTypeEntry` at known offsets
- Symbol reference resolution (TXT/GSI/TEX), including the case where a reference doesn't resolve (nullable
  fields stay `null`, no throw)
- Against real data: every `ter`-tagged symbol across `Terrain_RCT3.*.ovl` **and** `Terrain_CT.*.ovl` decodes to
  a non-empty `Name` and a defined `TerrainType` value (all 38 entries, not just the 32 in `Terrain_RCT3`)
- The grass-identification path: filtering decoded `Type == TerrainType.GroundBlended` entries by nearest-color
  match to `0xFF4F810E` yields `Terrain_06` specifically (locking in today's known-correct answer, confirmed
  for real against `Terrain_RCT3.unique.ovl`, so a future regression in the filter logic is caught)
- `Number` matches each entry's declared position and `Version`/`Addon` are `1`/`0` for every entry in the base
  install (invariants confirmed by the scratch-scanner run above)

### Success Criteria

- All TER entries extracted with full metadata
- Symbol references to TXT/GSI/TEX resolved
- Parameters and unknowns parsed correctly
- `Terrain.Load()` identifies grass via decoded metadata, not a hard-coded symbol name
- `SurfaceIndex`/`CliffIndex` → `TerrainType` lookup available for `terrain-heightmap.md` consumers
- `ter-viewer` Dumper plugin ships: renders full `TerrainTypeEntry` metadata (including resolved
  `TextureRef`/`DescriptionName`/`IconName` names and `ColourSimple`/`ColourMap` swatches) — texture *pixel*
  preview is explicitly deferred, see Future Work
- Zero regressions

## Post-Implementation Steps

When this decoder is implemented:

1. Add a results summary under `.agents/summaries/completed-work/` (see `flat-empty-park.md` or
   `completed-work/grass-from-ovl.md` for the current convention) and update this plan's status/README row in
   `ovl/README.md`.
2. Move `ter-viewer` from `📋 Planned` to `✅ Completed` in `plugins/README.md`'s status table (per
   `ovl/README.md`'s Dumper Plugin Requirement) — include it in the same table format as the other 6 completed
   plugins (Plugin/Tag/Type/Source columns), noting it's metadata-only (resolved texture *name*, no pixel
   preview — see Deferred below).

## Deferred (out of scope for this plan)

- **Texture pixel preview in `ter-viewer`**: showing actual decoded pixels (not just the resolved `tex`
  symbol's name) needs decoded RGBA bytes, and no existing "ovl" host function returns decoded pixel data —
  `resolve_pointer` only returns a resolved pointer's raw on-disk bytes (still DXT/palette-compressed, mip
  headers, etc.), and full texture decode is deliberately centralized in .NET
  (`Textures.cs`/`TextureDecoding.cs`). Closing this gap needs a new host function (e.g.
  `decode_texture_png(namePtr, nameLen) -> i64 offset` in `Dumper/Plugins/ViewerPlugin.cs`, running the
  existing `Textures.Extract`/`TextureDecoding` pipeline and PNG-encoding the result via ImageSharp, plus a
  `plugins/lib/ovl.ts` wrapper and a base64 encoder for embedding `<img src="data:image/png;base64,...">`) —
  real work, not a one-line addition, so it's left for a follow-up rather than folded into this plan. Whoever
  picks it up should note it'd be the first Dumper plugin to render decoded pixels rather than metadata/hex,
  and that the resulting host function is reusable by `tex-viewer`/`ftx-viewer` (both still `📋 Planned`, "Very
  Difficult") rather than `ter-viewer`-specific.

### Future Work

- Export terrain definitions
- Renderer-side consumption of the `SurfaceIndex`/`CliffIndex` → `TerrainType` lookup for actual per-corner
  texture sampling and blended rendering (tracked as deferred in `terrain-heightmap.md`)

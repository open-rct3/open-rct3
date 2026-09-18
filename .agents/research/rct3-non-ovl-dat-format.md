# RCT3 Non-OVL DAT Container Format

Based on user in-game/filesystem observation, web research, and cross-referencing
[`assets/reference/dat/dat.rs`](../../assets/reference/dat/dat.rs).

## What it is

**Confirmed (user, in-game observation)**: RCT3 saved parks are `.dat` files under
`Documents\RCT3\Parks` (e.g. `Rivendell.dat`) — this is *not* RCT2's `.sv6`/`.sc6` naming.

**Confirmed (user + web research)**: this is a distinct, *non-OVL* `.dat` container format —
separate from the OVL-archive object/asset DAT entries `OpenCobra/OVL` already reads — shared
across most of RCT3's other `Documents\RCT3\*` file kinds: `Coasters\*.trk` (track designs) and
`Fireworks\*.fwd` (firework displays, confirmed present locally as `Fireworks\Stratosphere.fwd`)
per web research, plus `*.frw` (firework effect definitions) which the user separately named as
`.fwr`/`.prf` — `.fwr` is likely the same file kind as `.frw` (extension typo), `.prf` unconfirmed
by any source found so far.

## Container structure

Community documentation ([belgabor.vodhin.org/format](http://belgabor.vodhin.org/format/))
describes this shared container as coming in three versions:

- v1: no header
- v2/v3: a header with a version byte (`0x1F`/`0x2F`) and fixed magic bytes `0xDA 0x1E 0xF1`

followed by a variable/class declaration section (Pascal-style length-prefixed strings naming
typed fields — `int32`, `float32`, `bool`, `array`, `struct`, `reference`, `string`, etc.) and a
data section of class-ID-tagged data blocks.

This matches [`assets/reference/dat/dat.rs`](../../assets/reference/dat/dat.rs)'s
`DataFile`/`DataStruct`/`StructField`/`DataEntry` model, confirming `dat.rs` is a reference for
*this* non-OVL format, not the OVL-internal object DAT format.

## What's undocumented

`dat.rs` names the save-relevant field kinds (`GETerrain`, `PathTileList`, `PathNodeArray`,
`WaypointList`, `WaterManager`) but treats every one as an opaque byte blob to skip — none of their
internal layout is decoded there or in the community documentation found so far. Decoding
`GETerrain` and `PathTileList`/`PathNodeArray` requires reverse-engineering from a real saved-park
`.dat` (e.g. `Rivendell.dat`, on hand locally).

Also unconfirmed: whether RCT3 saves are compressed/encrypted at the container level (some
Frontier titles zlib-wrap save bodies) — unknown until a sample file's header is inspected.

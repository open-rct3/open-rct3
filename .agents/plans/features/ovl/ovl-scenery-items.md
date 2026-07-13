# Decode SceneryItem (SID) and SceneryItemVisual (SVD) Entries

**Roadmap**: Phase 1, "Render built-in static (unanimated) scenery items" and "Render built-in
animated scenery items"

**Status**: Decoders and tests are done — see
[`completed-work/ovl-scenery-items.md`](../../../summaries/completed-work/ovl-scenery-items.md) for
what landed, how it was tested, and the architecture decisions behind it. What's left is the
`sid-viewer` Dumper plugin bug below, plus the deferred Future Work items.

## Open: `sid-viewer` `render()` crash

Calling `sid-viewer`'s `render()` with the full plugin (metadata table + placement diagram + LOD
table all together) crashes the Extism JS test harness with `RangeError: Offset is outside the
bounds of the DataView` inside Extism's own `store_u8`, thrown while marshaling the call.

Bisected into standalone minimal plugins, every individual piece of `renderSceneryItem`'s logic
passes on its own:
- SID field parsing (`readU16LE`/`readU32LE`/`readI32LE` over the raw `render(bytes)` payload)
- The placement SVG diagram (`renderPlacementDiagram`)
- The SVD/LOD-walking host-function chain (`Ovl.symbolAddress` → `Ovl.resolvePointer` →
  `Ovl.getRelocationSource`/`Ovl.resolveSymbolReference` per LOD)
- The TXT content fetch (`readTxtContent` via `Ovl.readResource`)

The crash only reproduces once everything is combined in the real file — the exact interaction
hasn't been pinned down. Suspects not yet ruled out:
- WASM linear-memory growth timing across many host-function calls in one `render()` invocation
  (each `resolve_symbol_reference`/`resolve_pointer` call that returns bytes does its own
  `ctx.store()` allocation; the full render does far more of these per call than any bisected piece
  did)
- AssemblyScript `Array<string>`/`class` allocation interacting with that growth (the bisected LOD
  section used a `string[]` and a `LodSummary` class together; isolating that combination
  specifically, independent of the metadata/placement sections, hasn't been tried)

**Next steps**: bisect further by combining exactly two of the three sections at a time (metadata +
placement, placement + LOD, metadata + LOD) rather than jumping straight to all three, to narrow
which pairing first triggers the crash. The two affected tests are `Deno.test.ignore`d in
[`plugins/sid-viewer/index.test.ts`](../../../../plugins/sid-viewer/index.test.ts) with a note
pointing back here; `name()`/`file_types()` still pass, and the plugin builds cleanly via
`scripts/build-plugins.ts`.

## Future Work

- Full sound script parsing (`SoundScript.RawCommands` currently holds undecoded 8-/16-byte
  command bytes only)
- Flat ride animation references (`SceneryItem.AnrRefs` currently holds raw ANR symbol names only)
- Visualize LOD switching distances in `sid-viewer`'s LOD table (currently just lists the
  `LodDistance` value per row, not a diagram of the switchover points)

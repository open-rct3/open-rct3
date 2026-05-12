# OVL Parser Summary

## What we learned

- `nullbmp.common.ovl` is readable and structurally valid.
- The parser now handles `nullbmp.common.ovl` correctly enough for `Load_NullbmpFtx_ExtractsFlexibleTexture` to pass.
- The initial `OVL.cs` path was wrong in how it extracted the FTX blob. It returned the `nullbmp:ftx` symbol/header string instead of payload bytes.
- The v5 loader metadata alignment theory was wrong. Requiring 4-byte alignment before the v5 symbol-count table broke normal parsing and was reverted.
- The parser was also skipping the `OvlHeader2` field pair for v1 archives. That was the major cursor bug behind the broken `Shapes.common.ovl` parse.
- `Shapes.common.ovl` still fails `Load_ShapesCommon_HasResources`, so resource extraction is not fully correct yet.

## Fixture evidence

- `nullbmp.common.ovl`
  - Parser reaches resource extraction.
  - The FTX test now passes.
  - This was fixed by reading the archive layout at the correct boundaries and treating relative pointer `0` as a valid string offset.
- `Shapes.common.ovl`
  - Expected to contain many resources.
  - Current parser still returns an empty resource map.
  - The archive now parses without hanging, but symbol extraction is still incomplete.

## Reference implementation notes

- I inspected `libOVLDump` in `/Users/chancesnow/GitHub/rct3-importer/RCT3 Importer/src/libOVLDump`.
- That implementation treats type-2 block sub-blocks separately:
  - block 0: symbols
  - block 1: loaders
  - block 2: symbol references
- It also reads `OvlHeader2` for all versions, including v1, before loader headers and block definitions.
- The symbol table for `Shapes.common.ovl` appears to have a 4-byte prefix before the 12-byte v1 symbol records.
- It resolves loader data from loader records, not by guessing directly from the symbol table.

## Current state

- `docs/ovl/archive-format.md` is back to its original state.
- `OpenCobra/Tests/ExtractResources.cs` now includes a second check for `Shapes.common.ovl`.
- `OpenCobra/OVL/OVL.cs` now parses `OvlHeader2` for all versions and handles v1 block sizes inline, but resource extraction for `Shapes.common.ovl` still needs work.

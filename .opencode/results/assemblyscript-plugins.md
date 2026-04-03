# AssemblyScript Dumper Plugins Results

## Summary

Created two AssemblyScript-based viewer plugins for the OVL Dumper:
1. **int-viewer** - Renders integer data as table with hex dump
2. **txt-viewer** - Renders text data with auto-detected encoding and hex dump

## Files Created

### Plugin Sources
```
plugins/
├── int-viewer/
│   ├── asconfig.json          # AssemblyScript config
│   ├── package.json           # npm dependencies (@extism/as-pdk)
│   └── index.ts               # Integer viewer implementation
└── txt-viewer/
    ├── asconfig.json
    ├── package.json
    └── index.ts               # Text viewer implementation
```

### Build System
```
scripts/build-plugins.ts       # Build script using asc compiler
```

### Plan Document
```
.opencode/plans/assemblyscript-plugins.md
```

## Implementation Details

### int-viewer
- Reads input bytes via `Host.input()`
- Parses as little-endian u32 array
- Renders table: Index | Decimal | Hex | Binary
- Hex dump view for debugging
- Signed/unsigned toggle via Extism config

### txt-viewer
- Auto-detects ASCII vs UTF-16LE encoding
- Renders text in `<pre>` block
- Hex dump view always included
- Troubleshooting hint links to GitHub issues

### Build Script
- Installs `assemblyscript` via npm if needed
- Compiles each plugin directory using `asc`
- Outputs to `bin/plugins/*.wasm`
- Uses `Deno.Command` for subprocess execution

## Tasks Added to deno.json

```json
"build:plugins": "deno run --allow-read --allow-write --allow-run=npx scripts/build-plugins.ts",
"check:plugins": "deno check plugins/int-viewer/index.ts plugins/txt-viewer/index.ts"
```

## Notes

- TypeScript/Deno cannot check AssemblyScript code (uses `u32`, `i32`, `bool` types)
- The `check:plugins` task will show errors for AS-specific types - this is expected
- Plugins must be compiled with `asc` (AssemblyScript compiler), not type-checked
- Build requires `assemblyscript` npm package to be installed first

## Next Steps

1. Run `deno task build:plugins` to compile the WASM files
2. Place compiled `.wasm` files in the Dumper's plugins directory
3. Test with actual OVL files containing int/txt entries

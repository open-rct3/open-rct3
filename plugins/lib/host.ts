import { CallContext } from "@extism/extism";

// Mirrors Dumper/Plugins/ViewerPlugin.cs's `NotFound` sentinel (long.MinValue).
const NOT_FOUND = -9223372036854775808n;

export const hostFunctions = {
  "env": {
    "abort": (_ctx: CallContext, message: number, fileName: number, lineNumber: number, columnNumber: number) => {
      console.error("Plugin aborted!");
      console.error(`${message} at ${fileName}(${lineNumber}:${columnNumber})`);
      Deno.exit(1);
    },
  },
  // Default no-op mocks for the "ovl" host functions (see plugins/lib/ovl.ts and
  // Dumper/Plugins/ViewerPlugin.cs) - WASM requires every import to be linked at instantiation
  // time even if a given exported function (e.g. name()/file_types()) never calls them, so tests
  // that don't care about pointer resolution still need *something* registered here. Tests that do
  // care should pass their own `functions` override to `createPlugin` instead of relying on these.
  "ovl": {
    "resolve_pointer": (_ctx: CallContext, _dataPtr: bigint) => NOT_FOUND,
    "get_relocation_source": (_ctx: CallContext, _address: bigint) => NOT_FOUND,
    "find_symbol": (_ctx: CallContext, _dataPtr: bigint) => NOT_FOUND,
    "read_resource": (_ctx: CallContext, _namePtr: bigint, _nameLen: bigint, _tagPtr: bigint, _tagLen: bigint) => NOT_FOUND,
    "current_resource_address": (_ctx: CallContext) => NOT_FOUND,
  },
};

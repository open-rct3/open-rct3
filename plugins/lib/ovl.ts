// @ts-nocheck
// Host functions exposed by Dumper's ViewerPlugin.cs (namespace "ovl") for requesting further
// OVL data on demand against whichever archive is currently open - relocated-pointer resolution,
// symbol lookup, and other-resource reads. Any plugin that needs to walk pointers (not just
// render its own resource's inline bytes) should use this instead of reimplementing struct-layout
// decode logic itself - see Dumper/Plugins/ViewerPlugin.cs's "ovl" host functions for why (keeps
// format-quirk knowledge, e.g. StaticShapes.cs's sort-tail ambiguity, in one place).
import { Memory } from "@extism/as-pdk";

// `@extism/as-pdk`'s own `length`/`load_u8` built-ins aren't re-exported from its public `index.ts`
// (only wrapped internally by its `Memory` class) - declared directly here, matching the PDK's own
// `lib/env.ts` namespace/signature exactly, since `Memory`'s constructor needs a length we can't
// otherwise obtain for an offset this module didn't allocate itself.
@external("extism:host/env", "length")
declare function length(offset: u64): u64;

// Mirrors ViewerPlugin.cs's `NotFound` sentinel (long.MinValue) - never a legitimate archive
// address, Extism memory offset, or relocation-table value. Exported so callers of the
// i64-returning lookups below (AssemblyScript doesn't allow `u32 | null`) can compare against it
// directly instead of a magic literal.
export const NOT_FOUND: i64 = i64(-9223372036854775808);

@external("ovl", "resolve_pointer")
declare function resolve_pointer(dataPtr: i64): i64;
@external("ovl", "get_relocation_source")
declare function get_relocation_source(address: i64): i64;
@external("ovl", "resolve_symbol_reference")
declare function resolve_symbol_reference(fieldAddress: i64): i64;
@external("ovl", "find_symbol")
declare function find_symbol(dataPtr: i64): i64;
@external("ovl", "read_resource")
declare function read_resource(namePtr: u64, nameLen: u64, tagPtr: u64, tagLen: u64): i64;
@external("ovl", "symbol_address")
declare function symbol_address(namePtr: u64, nameLen: u64, tagPtr: u64, tagLen: u64): i64;
@external("ovl", "current_resource_address")
declare function current_resource_address(): i64;

function memoryAt(offset: i64): Memory {
  return new Memory(u64(offset), length(u64(offset)));
}

export class OvlSymbol {
  constructor(public name: string, public tag: string) {}
}

// Shared by findSymbol/resolveSymbolReference - the host always emits exactly
// {"name":"...","tag":"..."} (see ViewerPlugin.cs), so a full JSON parser is unnecessary overhead.
function parseSymbolJson(offset: i64): OvlSymbol | null {
  if (offset == NOT_FOUND) return null;
  const json = memoryAt(offset).toString();
  const nameStart = json.indexOf('"name":"') + 8;
  const nameEnd = json.indexOf('"', nameStart);
  const tagStart = json.indexOf('"tag":"') + 7;
  const tagEnd = json.indexOf('"', tagStart);
  return new OvlSymbol(json.substring(nameStart, nameEnd), json.substring(tagStart, tagEnd));
}

export class Ovl {
  // The currently-rendering resource's own archive address, needed to compute field offsets
  // (e.g. shapeAddress + 40 for StaticShape.sh) since `render(bytes)`'s raw payload has no
  // address of its own. Returns `NOT_FOUND` if unavailable (AssemblyScript has no `u32 | null`).
  static currentResourceAddress(): i64 {
    return current_resource_address();
  }

  // Resolves a relocated pointer's raw target bytes (the block from the pointer's own target
  // offset onward - not the whole underlying archive block). Returns null if `dataPtr` doesn't
  // resolve (zero, or not a real pointer per the relocation-fixup table). Takes `i64` (not `u32`)
  // so a `getRelocationSource`/`currentResourceAddress` result can be passed straight through
  // without a manual cast.
  static resolvePointer(dataPtr: i64): Uint8Array | null {
    const offset = resolve_pointer(dataPtr);
    if (offset == NOT_FOUND) return null;
    return memoryAt(offset).toUint8Array();
  }

  // Gates a pointer-typed struct field's raw on-disk value as a real (fixed-up) pointer rather
  // than unpatched placeholder bytes, per the archive's relocation-fixup table. Returns
  // `NOT_FOUND` if `address` isn't listed (AssemblyScript has no `u32 | null`).
  static getRelocationSource(address: u32): i64 {
    return get_relocation_source(i64(address));
  }

  // Reverse of resolvePointer for symbol identity: given a relocated pointer that points directly
  // at another symbol's data (e.g. StaticShapeMesh.ftx_ref/txs_ref), returns the symbol that owns
  // that address, or null. Takes `i64` for the same reason as resolvePointer.
  static findSymbol(dataPtr: i64): OvlSymbol | null {
    return parseSymbolJson(find_symbol(dataPtr));
  }

  // Resolves an assignSymbolReference-driven cross-resource field (e.g. StaticShapeMesh.FtxRef's
  // own address, or one element of SceneryItem.SvdRefs) to the symbol it names, via the archive's
  // symbol-reference table - NOT getRelocationSource's base relocation-fixup table, which only
  // resolves pointers to other data within the archive's own blocks. Returns null if the field has
  // no SymbolRefStruct entry, or its target lives in an OVL that isn't currently loaded (e.g. a
  // pack-wide shared text/icon catalog). See ViewerPlugin.cs's resolve_symbol_reference and
  // Ovl.cs's symbolReferenceTargets doc comment for why this is a separate table from
  // getRelocationSource's.
  static resolveSymbolReference(fieldAddress: u32): OvlSymbol | null {
    return parseSymbolJson(resolve_symbol_reference(i64(fieldAddress)));
  }

  // Resolves a relocation-gated pointer *value* (e.g. a struct field already read via readU32LE,
  // not the field's own address) to the null-terminated string stored at its target - for plain
  // STRINGLIST_ASSIGN fields like TerrainType.description_name/icon_name, which are relocated
  // ASCII data, not symbol cross-references (unlike texture_ref - see resolveSymbolReference).
  // Returns null if dataPtr is unresolved or the string is empty.
  static resolveString(dataPtr: i64): string | null {
    const bytes = Ovl.resolvePointer(dataPtr);
    if (bytes == null) return null;
    let end = 0;
    while (end < bytes.length && bytes[end] != 0) end++;
    if (end == 0) return null;
    return String.UTF8.decode(bytes.slice(0, end).buffer);
  }

  // Fetches another symbol's own raw resource bytes by name/tag (e.g. once findSymbol resolves an
  // ftx_ref to a name). Returns null if no matching symbol exists in the currently open archive.
  static readResource(name: string, tag: string): Uint8Array | null {
    const nameMem = Memory.allocateString(name);
    const tagMem = Memory.allocateString(tag);
    const offset = read_resource(nameMem.offset, nameMem.length, tagMem.offset, tagMem.length);
    nameMem.free();
    tagMem.free();
    if (offset == NOT_FOUND) return null;
    return memoryAt(offset).toUint8Array();
  }

  // Resolves a symbol's own archive address by name/tag - needed to walk pointers *inside* a
  // different resource (e.g. sid-viewer resolving a SvdRefs entry, then reading that SVD's own
  // lods[] array via getRelocationSource(svdAddress + 24)), which read_resource's raw bytes and
  // findSymbol/resolveSymbolReference's name-only result can't provide on their own. Returns
  // `NOT_FOUND` if no matching symbol exists in the currently open archive.
  static symbolAddress(name: string, tag: string): i64 {
    const nameMem = Memory.allocateString(name);
    const tagMem = Memory.allocateString(tag);
    const address = symbol_address(nameMem.offset, nameMem.length, tagMem.offset, tagMem.length);
    nameMem.free();
    tagMem.free();
    return address;
  }
}

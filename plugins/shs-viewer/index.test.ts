import { assert, assertEquals } from "@std/assert";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/shs-viewer.wasm", import.meta.url);

Deno.test("shs-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Static Shape Viewer");
  await plugin.close();
});

Deno.test("shs-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()), ["shs"]);
  await plugin.close();
});

// Mirrors ViewerPlugin.cs's `NotFound` sentinel (long.MinValue).
const NOT_FOUND = -9223372036854775808n;

Deno.test("shs-viewer: render() walks sh[] via ovl host functions for a real per-mesh summary", async () => {
  // A synthetic single-mesh StaticShape: shapeAddr=1000, sh[]=2000, sh[0]=3000 (meshAddr).
  // meshAddr+4 (ftx_ref) unresolved (no ftx); meshAddr+8 (txs_ref) resolves to a symbol.
  const shsFunctions = {
    ...functions,
    "ovl": {
      "current_resource_address": () => 1000n,
      "get_relocation_source": (_ctx: unknown, address: bigint) => {
        if (address === 1040n) return 2000n; // shapeAddr + 40 (sh field) -> sh[] array address
        if (address === 2000n) return 3000n; // sh[0] slot -> meshAddr
        return NOT_FOUND;
      },
      "resolve_symbol_reference": (ctx: { store: (v: Uint8Array) => bigint }, fieldAddress: bigint) => {
        if (fieldAddress !== 3008n) return NOT_FOUND; // meshAddr + 8 (txs_ref)
        return ctx.store(new TextEncoder().encode(JSON.stringify({ name: "BillboardStandard", tag: "txs" })));
      },
      "resolve_pointer": (ctx: { store: (v: Uint8Array) => bigint }, dataPtr: bigint) => {
        if (dataPtr !== 3000n) return NOT_FOUND;
        const meshBytes = new Uint8Array(40);
        const view = new DataView(meshBytes.buffer);
        view.setUint32(0, 1, true); // support_type
        view.setUint32(20, 3, true); // sides
        view.setUint32(24, 8, true); // vertex_count
        view.setUint32(28, 12, true); // index_count
        return ctx.store(meshBytes);
      },
      "find_symbol": () => NOT_FOUND,
      "read_resource": () => NOT_FOUND,
    },
  };

  // The 56-byte StaticShape header: mesh_count=1 at offset 36, effect_count=0 at offset 44.
  const header = new Uint8Array(56);
  new DataView(header.buffer).setUint32(36, 1, true);

  const plugin = await createPlugin(wasmUrl, { functions: shsFunctions });
  const out = await plugin.call("render", header);
  assert(out !== null, "Expected a result!");
  const html = out!.text();

  assert(html.includes("Meshes (1)"), "Expected a resolved per-mesh table, got: " + html);
  assert(html.includes(">8<"), "Expected the resolved vertex count (8), got: " + html);
  assert(html.includes(">12<"), "Expected the resolved index count (12), got: " + html);
  assert(html.includes(">3<"), "Expected the resolved sides value (3), got: " + html);
  assert(html.includes("(none)"), "Expected FtxRef to render as (none), got: " + html);
  assert(html.includes("BillboardStandard"), "Expected the resolved TxsRef symbol name, got: " + html);
  await plugin.close();
});

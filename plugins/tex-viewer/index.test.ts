import { assert, assertEquals } from "@std/assert";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/tex-viewer.wasm", import.meta.url);

Deno.test("tex-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Texture Viewer");
  await plugin.close();
});

Deno.test("tex-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()).sort(), ["btbl", "tex"].sort());
  await plugin.close();
});

Deno.test("tex-viewer: render() tex", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  
  // Create dummy Tex header (48 bytes)
  const data = new Uint8Array(48);
  data[0] = 0x07; data[1] = 0x00; data[2] = 0x07; data[3] = 0x00; // 0x00070007
  data[32] = 0; // count = 0
  data[44] = 0; // unk12 = 0
  
  const out = await plugin.call("render", data);
  assert(out !== null, "Expected a result!");
  const html = out!.text();
  assert(html.includes("tex-viewer"), "Expected tex-viewer wrapper");
  assert(html.includes("Texture Header (Tex)"), "Expected tex header");
  await plugin.close();
});

Deno.test("tex-viewer: render() btbl", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  
  // Create dummy BitmapTable header (16 bytes min for viewer logic)
  const data = new Uint8Array(16);
  data[0] = 0x00; data[1] = 0x00; data[2] = 0x00; data[3] = 0x00; // Unk = 0
  data[4] = 0x01; data[5] = 0x00; data[6] = 0x00; data[7] = 0x00; // Length = 1
  
  const out = await plugin.call("render", data);
  assert(out !== null, "Expected a result!");
  const html = out!.text();
  assert(html.includes("tex-viewer"), "Expected tex-viewer wrapper");
  assert(html.includes("Bitmap Table Header"), "Expected btbl header");
  await plugin.close();
});

import { assert, assertEquals } from "@std/assert";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/snd-viewer.wasm", import.meta.url);

Deno.test("snd-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Sound Viewer");
  await plugin.close();
});

Deno.test("snd-viewer: version()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("version");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "0.1.0");
  await plugin.close();
});

Deno.test("snd-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()), ["snd"]);
  await plugin.close();
});

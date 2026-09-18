import { assert, assertEquals } from "@std/assert";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/spl-viewer.wasm", import.meta.url);

Deno.test("spl-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Spline Viewer");
  await plugin.close();
});

Deno.test("spl-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()), ["spl"]);
  await plugin.close();
});

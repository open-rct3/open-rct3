import { assert } from "@std/assert";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/ter-viewer.wasm", import.meta.url);

Deno.test("ter-viewer: plugin loads successfully", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  assert(plugin !== null);
  await plugin.close();
});

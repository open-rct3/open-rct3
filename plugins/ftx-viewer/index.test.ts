import { assert, assertEquals } from "@std/assert";
import { existsSync } from "jsr:@std/fs/exists";
import createPlugin from "@extism/extism";

import { hostFunctions as functions } from "../lib/host.ts";

const wasmUrl = new URL("../../bin/plugins/ftx-viewer.wasm", import.meta.url);

Deno.test("ftx-viewer: name()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("name");
  assert(out !== null, "Expected a result!");
  assertEquals(out!.text(), "Flexi-Texture Viewer");
  await plugin.close();
});

Deno.test("ftx-viewer: file_types()", async () => {
  const plugin = await createPlugin(wasmUrl, { functions });
  const out = await plugin.call("file_types");
  assert(out !== null, "Expected a result!");
  assertEquals(JSON.parse(out!.text()), ["ftx", "flt"]);
  await plugin.close();
});

Deno.test("ftx-viewer: render() nullbmp", async () => {
  const nullBmp = new URL("../tests/nullbmp.ftx", import.meta.url);
  if (!existsSync(nullBmp)) return console.info(
    "plugins/tests/nullbmp.ftx not found. Skipping this integration test."
  );

  const plugin = await createPlugin(wasmUrl, { functions });
  const data = await Deno.readFile(nullBmp);
  const out = await plugin.call("render", data);
  assert(out !== null, "Expected a result!");
  const html = out!.text();
  assert(html.includes("ftx-viewer"), "Expected ftx-viewer wrapper");
  assert(!html.includes("class='error'"), "Expected no error, got: " + html);
  await plugin.close();
});

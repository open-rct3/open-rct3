import { debounce, delay } from "jsr:@std/async";
import { join } from "jsr:@std/path";

import build, { BuildState } from "./build.ts";

if (import.meta.main) {
  try {
    await rebuild();
    const rebuildInfrequently = debounce((event) => rebuild(event), 250);
    for await (const event of Deno.watchFs(join(import.meta?.dirname ?? Deno.cwd(), "src"), { recursive: true })) {
      await rebuildInfrequently(event);
    }
    Deno.exit(0);
  } catch (err) {
    console.error(err instanceof Error ? `${err.stack}` : `Error: ${err.toString()}`);
    Deno.exit(1);
  }
}

async function rebuild(event?: Deno.FsEvent) {
  if (event && event.kind !== "modify") return;
  const result = await build();
  if (result?.state === BuildState.success) await delay(1500).then(() => console.clear());
  console.log("Watching for changes…");
}

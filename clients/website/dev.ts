import { debounce, delay } from "jsr:@std/async";
import { join } from "jsr:@std/path";

import build, { BuildState } from "./build.ts";
import { BuildStatus } from "./build.ts";

const website = import.meta?.dirname ?? Deno.cwd();

if (import.meta.main) {
  const siteChanges = Deno.watchFs([join(website, "src")], { recursive: true });
  // TODO: Use WMR programmatically
  const wmr = new Deno.Command("npx", {
    args: [
      "wmr",
      "serve",
      "--public",
      join(website, "_site"),
      "--out",
      join(website, "_site")
    ],
    stdout: "piped",
    stderr: "piped"
  }).spawn();

  try {
    // TODO: Refactor to a spinner interface
    Deno.addSignalListener("SIGINT", () => {
      siteChanges.close();
      wmr.kill();
    });

    const wmrStarted = wmr.stdout.values({ preventCancel: true }).next();
    await build().then(delayABit);
    console.clear();
    console.log("⏳ Starting dev server…");
    await wmrStarted.then(() => delayABit());
    console.clear();
    console.log("👁 Watching for changes…");

    const rebuildInfrequently = debounce((event) => rebuild(event), 250);
    for await (const event of siteChanges) await rebuildInfrequently(event);
    Deno.exit(0);
  } catch (err) {
    console.error(err instanceof Error ? `❌ ${err.stack}` : `❌ Error: ${err.toString()}`);
    // FIXME: Don't exit for recoverable errors.
    wmr.kill();
    Deno.exit(1);
  }
}

async function delayABit(result?: BuildStatus) { await delay(750); return result; }

async function rebuild(event?: Deno.FsEvent) {
  // Only rebuild if a project file has been modified
  const fileWasModified = (event?.isFile ?? false) && event?.kind === "modify";
  // FIXME: Also rebuild if a file was created
  if (!fileWasModified) return;
  console.debug(event?.kind ?? "First build!");
  const result = await build();
  if (result?.state === BuildState.success) await delay(1000).then(() => console.clear());
}

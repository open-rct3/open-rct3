import { debounce, delay } from "@std/async";
import { parseArgs } from "@std/cli/parse-args";
import * as path from "@std/path";
import { open } from 'https://deno.land/x/open/index.ts';

import build, { BuildState, BuildStatus } from "./build.ts";
import { port } from "./server.ts";

const dirname = import.meta?.dirname ?? Deno.cwd();
const appDir = path.resolve(path.join(".", "clients", "isomorphic"));
const websiteDir = path.resolve(path.join(".", "clients", "website", "_site"));

// TODO: Fix remote debugging. See https://stackoverflow.com/a/69368719/1363247

if (import.meta.main) {
  const args = parseArgs<{ open: boolean }>(Deno.args);
  const siteChanges = Deno.watchFs([appDir, websiteDir], { recursive: true });
  Deno.addSignalListener("SIGINT", () => siteChanges.close());

  await build().then(delayABit);

  // Open the site's URL in the user's browser
  if (args.open) open(`http://localhost:${port}/play`, { url: true, background: true });

  console.clear();
  console.log("Watching for changes…");
  const rebuildInfrequently = debounce((event: Deno46.FsEvent) => rebuild(event), 250);

  try {
    // FIXME: This isn't rebuilding the site... 🙄
    for await (const event of siteChanges) {
      log("File changed.", "👁️");
      await rebuildInfrequently(event as Deno46.FsEvent);
      console.clear();
      console.log("Watching for changes…");
    }
  } catch (err) {
    // deno-lint-ignore no-explicit-any
    const error = `${err instanceof Error ? err.message : `Error: ${(err as any).toString()}`}`;

    log(error, "❌");
    if (err instanceof Error && err.stack) console.error(err.stack);
    // FIXME: Don't exit for recoverable errors.
    siteChanges.close();
    Deno.exit(1);
  }

  log("Finished.", "✅");
  Deno.exit();
}

async function delayABit(result?: BuildStatus) { await delay(750); return result; }

declare namespace Deno46 {
  interface FsEvent extends Deno.FsEvent {
    isFile: boolean;
  }
}

async function rebuild(event?: Deno46.FsEvent) {
  // Only rebuild if a project file has been modified
  const fileWasModified = (event?.isFile ?? false) && (event?.kind === "create" || event?.kind === "modify");
  if (!fileWasModified) return;
  console.debug(event?.kind ?? "First build!");
  const result = await build();
  if (result?.state === BuildState.success) await delay(1000);
}

function log(message: string, flair?: string) {
  console.log(`${flair ? `${flair} ` : ""}${message}`)
}

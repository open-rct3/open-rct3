import { debounce, delay } from "jsr:@std/async";
import { join } from "jsr:@std/path";

import build, { BuildState } from "./build.ts";

const website = import.meta?.dirname ?? Deno.cwd();

if (import.meta.main) {
  const siteChanges = Deno.watchFs([join(website, "src")], { recursive: true });

  try {
    // TODO: Refactor to a spinner interface
    Deno.addSignalListener("SIGINT", () => {
      siteChanges.close();
    });

    await build().then(() => delay(750));
    console.clear();
    console.log("Watching for changes…");

    const rebuildInfrequently = debounce((event) => rebuild(event), 250);
    for await (const event of siteChanges) await rebuildInfrequently(event);
    Deno.exit(0);
  } catch (err) {
    console.error(
      // deno-lint-ignore no-explicit-any
      err instanceof Error ? `${err.stack}` : `Error: ${(err as any).toString()}`,
    );
    // FIXME: Don't exit for recoverable errors.
    Deno.exit(1);
  }
}

async function rebuild(event?: Deno.FsEvent) {
  // Only rebuild if a project file has been modified
  const fileWasModified = (event?.isFile ?? false) && event?.kind === "modify";
  // FIXME: Also rebuild if a file was created
  if (!fileWasModified) return;
  console.debug(event?.kind ?? "First build!");
  const result = await build();
  if (result?.state === BuildState.success) await delay(1000).then(() => console.clear());
}

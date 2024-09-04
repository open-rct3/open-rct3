import { debounce, delay } from "jsr:@std/async";
import { join } from "jsr:@std/path";
import * as path from "@std/path";
import * as fs from "@std/fs";
import * as server from "npm:@web/dev-server";

import build, { BuildState } from "./build.ts";
import { BuildStatus } from "./build.ts";

const website = import.meta?.dirname ?? Deno.cwd();

const port = 8080;
const devServerPromise = server.startDevServer({
  config: {
    // FIXME: port: port,
    watch: true,
    open: true,
    rootDir: join(website, "_site"),
    appIndex: join(website, "_site", "index.html"),
    // FIXME: basePath: join(website, "_site"),
    nodeResolve: false,
    // FIXME: esbuildTarget: "es2020"
  },
  argv: [],
  readCliArgs: false,
  readFileConfig: false,
  autoExitProcess: true,
  logStartMessage: false
});

if (import.meta.main) {
  try {
    // TODO: Refactor to a spinner interface
    console.log("⏳ Starting dev server…");
    const devServer = await devServerPromise.then((server) => {
      console.debug("Hello?");
      return server;
    });
    Deno.addSignalListener("SIGINT", () => devServer.stop());

    const rebuildInfrequently = debounce((event: Deno46.FsEvent) => rebuild(event), 250);
    devServer.fileWatcher.on("change", (filePath, stats) => rebuildInfrequently({
      kind: "modify",
      paths: [filePath],
      isFile: stats?.isFile() ?? false
    }));

    const serverAddress = new URL(devServer.server?.address()?.toString() ?? `http://localhost:${port}`).toString();
    console.log(`✅ Serving site at ${serverAddress}`);

    await build({ timeout: 3500 });
    console.log("👁 Watching for changes…");
    await devServer.start();

    Deno.exit(0);
  } catch (err) {
    console.error(err instanceof Error ? `❌ ${err.stack}` : `❌ Error: ${err.toString()}`);
    // FIXME: Don't exit for recoverable errors.
    (await devServerPromise).stop().then(() => Deno.exit(1));
  }
}

async function delayABit(result?: BuildStatus) {
  await delay(750);
  return result;
}

declare namespace Deno46 {
  interface FsEvent extends Deno.FsEvent {
    isFile: boolean;
  }
}

async function rebuild(event?: Deno46.FsEvent) {
  // Only rebuild if a project file has been modified
  const fileWasModified = (event?.isFile ?? false) && event?.kind === "modify";
  // FIXME: Also rebuild if a file was created
  if (!fileWasModified) return;
  console.debug(event?.kind ?? "First build!");
  const result = await build({ timeout: 2500 });
  if (result?.state === BuildState.success) await delay(1000).then(() => console.clear());
}

import { debounce, delay } from "@std/async";
import { open } from 'https://deno.land/x/open/index.ts';
import { exists } from "@std/fs";
import { contentType } from "@std/media-types";
import { join } from "@std/path";
import * as path from "@std/path";
import * as posix from "jsr:@std/path/posix";
import * as windows from "jsr:@std/path/windows";
import { serveFile, ServerReadableFile } from "jsr:@geacko/serve-file";

import { unreachable } from "@std/assert";
import build, { BuildState, BuildStatus } from "./build.ts";

const port = 8080;
const website = import.meta?.dirname ?? Deno.cwd();
const appDir = path.resolve(path.join(".", "clients", "isomorphic"));
const filesDir = path.resolve(path.join(".", "clients", "website", "_site"));

if (import.meta.main) {
  const siteChanges = Deno.watchFs([appDir, join(website, "src")], { recursive: true });
  const abortion = new AbortController();

  // TODO: Refactor to a spinner interface
  await build().then(delayABit).then(() => console.clear());

  // TODO: Extract this server for production use
  // FIXME: Argument of type '(req: Request, _info: ServeUnixHandlerInfo) => Promise<Response>' is not assignable to parameter of type 'ServeHandler'.
  const server = Deno.serve({
    port,
    hostname: "localhost",
    reusePort: false,
    signal: abortion.signal,
    async onListen(serverAddress) {
      const addressUrl = `http://${serverAddress.hostname === "::1" ? "localhost" : serverAddress.hostname}:${serverAddress.port}`;
      try {
        console.log(`🌎 Serving at ${addressUrl}`);

        Deno.addSignalListener("SIGINT", async () => {
          await Deno.stdout.write(
            new TextEncoder().encode("⏳ Gracefully shutting down…")
          );
          await server.shutdown();
          siteChanges.close();
        });

        // Open the site's URL in the user's browser
        // TODO: Make this optional
        open(`${addressUrl}/play`, { url: true, background: true });

        console.log("👁 Watching for changes…");
        const rebuildInfrequently = debounce((event) => rebuild(event), 250);
        for await (const event of siteChanges) await rebuildInfrequently(event);

        await server.finished.then(() => delayABit());
      } catch (err) {
        console.error(err instanceof Error ? `❌ ${err.stack}` : `❌ Error: ${err.toString()}`);
        // FIXME: Don't exit for recoverable errors.
        server.shutdown();
        Deno.exit(1);
      }
    },
    onError(err: Error | unknown) {
      // deno-lint-ignore no-explicit-any
      console.debug(`❌ ${err instanceof Error ? (err.stack ?? err.message) : (err as any).toString()}`);
      return new Response(null, { status: err instanceof Deno.errors.NotFound ? 404 : 500 });
    }
  }, async function serve(req: Request, _info: Deno.ServeUnixHandlerInfo) {
    const headers = req.headers;
    const route = new URL(req.url).pathname;
    console.debug(`➡ ${req.method} ${route}`);

    if (req.method == 'OPTIONS') return new Response(void 0, { status: 204, headers });
    if (req.method != 'GET' && req.method != 'HEAD') return new Response(void 0, { status: 405 });
    if (!URL.canParse(req.url)) return new Response(void 0, { status: 400 });

    const pathAbsolute = path.normalize(path.resolve(path.join(filesDir, route)));
    const pathExists = await exists(pathAbsolute);
    if (!pathExists) throw new Deno.errors.NotFound(pathAbsolute);
    const filePath = await (async function () {
      const pathStats = await Deno.stat(pathAbsolute);
      const resourcePath = pathStats.isDirectory ? `${pathAbsolute}/index.html` : pathAbsolute;
      switch (Deno.build.os) {
        case "windows": return windows.normalize(resourcePath);
        case "darwin":
        case "linux": return posix.normalize(resourcePath);
        default: throw new Error("Unsupported OS!");
      }
    })();

    const pathStats = await Deno.stat(filePath);
    if (req.method === "GET" && (pathStats.isDirectory || pathStats.isFile)) return serveFile(req, {
      contentType: contentType(path.extname(filePath)) || "",
      additionalHeaders: null,
      size: pathStats.size,
      lastModified: pathStats.mtime?.getUTCDate() ?? Date.now(),
      etag: (pathStats.mtime ?? new Date()).toISOString(),
      async open(): Promise<ServerReadableFile> {
        const file = await Deno.open(filePath);
        console.info(`💁‍♀️ ${req.method} ${filePath}`);
        return {
          readable: file.readable,
          seek: (x) => file.seek(x, Deno.SeekMode.Start)
        };
      }
    });

    unreachable("❌ Unknown resource request.");
  });
  await server.finished;
  Deno.exit(0);
}

async function delayABit(result?: BuildStatus) { await delay(750); return result; }

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
  const result = await build();
  if (result?.state === BuildState.success) await delay(1000).then(() => console.clear());
}

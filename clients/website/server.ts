import { unreachable } from "@std/assert";
import { exists } from "@std/fs";
import { contentType } from "@std/media-types";
import * as path from "@std/path";
import * as posix from "jsr:@std/path/posix";
import * as windows from "jsr:@std/path/windows";
import { serveFile, ServerReadableFile } from "jsr:@geacko/serve-file";

export const port = 8080;
const dirname = import.meta?.dirname ?? Deno.cwd();
const websiteDir = path.resolve(path.join(".", "clients", "website", "_site"));

export default async function startServer(options: { filesDir: string }) {
  const { filesDir } = options;
  const abortion = new AbortController();

  const server = Deno.serve({
    port,
    hostname: "localhost",
    reusePort: false,
    signal: abortion.signal,
    async onListen(serverAddress) {
      const addressUrl = `http://${serverAddress.hostname === "::1" ? "localhost" : serverAddress.hostname}:${serverAddress.port}`;
      console.log(`🌎 Serving at ${addressUrl}`);
    },
    onError(err: Error | unknown) {
      // deno-lint-ignore no-explicit-any
      console.debug(`❌ ${err instanceof Error ? (err.stack ?? err.message) : (err as any).toString()}`);
      return new Response(null, { status: err instanceof Deno.errors.NotFound ? 404 : 500 });
    }
  }, async function serve(req: Request) {
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

  return server;
}

if (import.meta.main) {
  await startServer({ filesDir: websiteDir });
  Deno.exit();
}

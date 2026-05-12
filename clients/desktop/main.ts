// deno-lint-ignore-file no-var
import { SizeHint } from "@webview/webview";
import { asset, uri } from "./src/env.ts";
import { WebView } from "./src/platform/window.ts";

// QUESTION: https://gpl.ea.com/eawebkit.html

// TODO: https://deno.land/x/wincompile@0.3.2
// TODO: https://deno.com/blog/supabase-on-jsr

declare global {
  var mainWindow: WebView | undefined;
}

if (import.meta.main) {
  const { preload } = await import("@webview/webview");
  const indexUri = uri("play?client=desktop");

  await preload();
  const window = globalThis.mainWindow = new WebView("OpenRCT3");
  window.view.init(await Deno.readTextFile(asset("content-script.js")));
  window.size = { width: 800, height: 500, hint: SizeHint.NONE };
  // Render splash page
  window.view.navigate(`data:text/html,${await Deno.readTextFile(asset("index.html"))}`);
  // FIXME: Window flickers too much when launching on Windows 10
  window.view.run();
}

// TODO: Extract IPC primitives into a shared library
// IPC Protocol Primitives
// Prior art: Maxis' EA WebKit GUI for SimCity
interface Request<Response, Body = undefined> {
  method: "get" | "post";
  uri: URL | string;
  send(body?: Body): Promise<Response>;
}

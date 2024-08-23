// deno-lint-ignore-file no-var
import { SizeHint, type Webview } from "@webview/webview";
import * as emit from "jsr:@deno/emit";
import { asset, uri } from "./src/env.ts";

const decoder = new TextDecoder("utf-8");

// QUESTION: https://gpl.ea.com/eawebkit.html

// TODO: https://deno.land/x/wincompile@0.3.2
// TODO: https://deno.com/blog/supabase-on-jsr

if (import.meta.main) {
  const { Webview } = await import("@webview/webview");
  const indexUri = uri("play?client=desktop");

  const view = globalThis.mainWindow = new Webview();
  queueMicrotask(async function (this: typeof globalThis) {
    const { html, render } = await import("@lit-labs/ssr");
    const { unsafeHTML } = await import("lit/directives/unsafe-html");
    const { collectResult } = await import("@lit-labs/ssr/render-result");
    const { splash, styles } = await import("./src/splash.ts");
    const document = await collectResult(render(
      html`
        <head>
          <meta charset="utf-8">
          <meta name="" >
          ${unsafeHTML(`<style>${styles.toString()}</style>`)}
        </head>
        <body>${splash}</body>
      `
    ));

    view.navigate(`data:text/html,${document}`);
  });

  view.init(await bundle(asset("content-script.ts")));
  view.title = "OpenRCT3";
  // view.size = { width: 800, height: 450, hint: SizeHint.MIN };
  // view.size = { width: 800, height: 600, hint: SizeHint.NONE };
  // FIXME: Window flickers too much when launching on Windows 10
  view.run();
}

// TODO: Extract IPC primitives into a shared library
// IPC Protocol Primitives
// Prior art: Maxis' EA WebKit GUI for SimCity
interface Request<Response, Body = undefined> {
  method: "get" | "post";
  uri: URL | string;
  send(body?: Body): Promise<Response>;
}

declare global {
  var mainWindow: Webview | undefined;
}

async function bundle(script: string) {
  const bundle = await emit.bundle(script).catch(err => {
    // FIXME: See https://github.com/denoland/deno/issues/15015
    if (err instanceof Error && err.message.startsWith("WebAssembly.compile"))
      throw new Error([
        "Do not bundle scripts while running the Deno debugger.",
        "See https://github.com/denoland/deno/issues/15015 for details.",
      ].join("\n\n"), { cause: err });
    throw err;
  });
  if (bundle instanceof Object === false) throw new Error(`Could not bundle script: ${script}`);
  if (typeof bundle.code !== "string") throw new Error(`${JSON.stringify(bundle)}`);
  return bundle.code;
}

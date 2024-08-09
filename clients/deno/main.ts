// deno-lint-ignore-file no-var
import { Document, DOMParser, Element, ElementCreationOptions, Node } from "@b-fuze/deno-dom";
import { assert, assertInstanceOf } from "@std/assert";
import { SizeHint, type Webview } from "@webview/webview";
import * as emit from "jsr:@deno/emit";
import { Filter } from "./polyfills/node-filter.ts";
import { TreeWalker } from "./polyfills/tree-walker.ts";
import { asset, uri } from "./src/env.ts";

const decoder = new TextDecoder("utf-8");

// QUESTION: https://gpl.ea.com/eawebkit.html

// TODO: https://deno.land/x/wincompile@0.3.2
// TODO: https://deno.com/blog/supabase-on-jsr

if (import.meta.main) {
  const { Webview } = await import("@webview/webview");
  const indexUri = uri("play?client=desktop");
  const document = globalThis.document = new DOMParser().parseFromString("", "text/html");

  // TODO: Upstream some sort of Deno-safe solution for this back to lit-html
  // See https://github.com/lit/lit/blob/619449b84cb63d9c00e4316551246957c939a64b/packages/lit-html/src/lit-html.ts#L352
  globalThis.document = {
    createTreeWalker(root: Node, whatToShow?: number, filter?: Filter) {
      return new TreeWalker(root, whatToShow, filter);
    },
    createTextNode: (data?: string) => document.createTextNode(data),
    createComment: (data?: string) => document.createComment(data),
    createElement: (tagName: string, options?: ElementCreationOptions) => document.createElement(tagName, options),
    // See https://dom.spec.whatwg.org/#dom-document-importnode
    // See https://dom.spec.whatwg.org/#concept-node-clone
    importNode: (node: Node, deep?: boolean) => {
      // FIXME: Also throw here if `node instanceof ShadowRoot`.
      if (node instanceof Document) {
        throw new DOMException(undefined, "NotSupportedError");
      }
      return node.cloneNode(deep);
    },
  } as unknown as Document;
  // TODO Defer `globalThis.document = undefined` at end of scope. See

  const view = globalThis.mainWindow = new Webview();
  queueMicrotask(async function (this: typeof globalThis) {
    const { html, render } = await import("lit-html");
    const { splash, styles } = await import("./src/splash.ts");
    render(html`${splash}`, document.body);
    document.head.innerHTML = `<meta charset="utf-8"><style>${styles.toString()}</style>`;

    view.init(await bundle(asset("content-script.ts")));
    view.title = "OpenRCT3";
    view.size = { width: 800, height: 450, hint: SizeHint.MIN };
    view.size = { width: 800, height: 600, hint: SizeHint.NONE };
    view.navigate(`data:text/html,${document.documentElement?.outerHTML}`);
    view.run();
  });
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
  var document: Document | undefined;
  var mainWindow: Webview | undefined;
}

// TODO: Upstream this polyfill back to deno-dom
Element.prototype.hasAttributes = function (this: Element) {
  return this.attributes.length === 0;
};

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

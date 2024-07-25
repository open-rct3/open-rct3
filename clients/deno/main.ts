import { assert } from "@std/assert";
import * as path from "@std/path";
import * as fs from "@std/fs";

const decoder = new TextDecoder("utf-8");

// QUESTION: https://gpl.ea.com/eawebkit.html

// TODO: https://deno.land/x/wincompile@0.3.2

// TODO: Extract IPC primitives into a shared library

// IPC Protocol Primitives
// Prior art: Maxis' EA WebKit GUI for SimCity
interface Request<Respose, Body = undefined> {
  method: "get" | "post";
  uri: URL | string;
  send(body?: Body): Promise<Respose>;
}

function uri(path: string) {
  // TODO: Set `RCT3_SERVER_HOST` at compile-time. See https://github.com/denoland/deno/issues/9152#issuecomment-773522311
  // See also https://docs.deno.com/examples/importing-json (https://github.com/denoland/deno/issues/7623)
  assert(Deno.env.has("RCT3_SERVER_HOST"), "Could not determine OpenRCT3 Server host name.");
  return `${Deno.env.get("RCT3_SERVER_HOST")}/${path.startsWith("/") ? path.slice(1) : path}`;
}

if (import.meta.main) {
  const cwd = Deno.cwd();
  const indexUri = uri("index.html");
}

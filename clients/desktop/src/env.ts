import { assert } from "@std/assert";
import * as fs from "@std/fs";
import * as path from "@std/path";

/** @throws When the given environment `key` does not exist in the current environment. */
// See https://docs.deno.com/examples/environment-variables
export function enforceEnv(key: string, message?: string) {
  assert(Deno.env.has(key), message);
  return Deno.env.get(key)!;
}

export function envOrDefault<T>(key: string, default_: T | null = null) {
  if (Deno.env.has(key)) return Deno.env.get(key);
  return default_;
}

export function uri(pathOrAsset: string, base?: string) {
  // QUESTION: See https://github.com/denoland/deno/issues/9152#issuecomment-773522311
  // See https://docs.deno.com/examples/importing-json (https://github.com/denoland/deno/issues/7623)
  const url = new URL(base || enforceEnv("RCT3_SERVER_URL", "Could not determine OpenRCT3 Server host name."));
  url.pathname = path.join(url.pathname, pathOrAsset);
  return url.toString();
}

/** @returns Normalized path to the given `asset`. */
export function asset(asset: string, options?: { mustExist: boolean }) {
  options = { mustExist: true, ...options };
  const moduleUrl = new URL(import.meta.url);
  const sourceRoot = path.dirname(path.parse(path.fromFileUrl(moduleUrl)).dir)
  // TODO: Use the bundled resource path on mac OS
  const assetPath = path.resolve(sourceRoot, "resources", asset);
  if (options.mustExist) assert(fs.existsSync(assetPath), `Asset does not exist: ${assetPath}`);
  return assetPath;
}

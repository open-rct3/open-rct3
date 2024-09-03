import * as emit from "@deno/emit";
import { type ImportMap } from "@deno/emit";
import { assert } from "@std/assert";
import { delay } from "@std/async";
import * as path from "@std/path";
import { existsSync } from "@std/fs";

import importMap from "./imports.json" with { type: "json" };
import config from "../../deno.json" with { type: "json" };
import { formatMeasure } from "../website/build.ts";

export default async function buildApp(
  options?: { entryPoints?: string[] } = { entryPoints: ["isomorphic/game.tsx"] }
) {
  return await build(options);
}

if (import.meta.main) {
  try {
    // TODO: Refactor to a spinner interface
    console.log("Building isomorphic client…");
    const markerName = Object.getPrototypeOf(build).name;
    const _startMark = performance.mark(markerName);
    const modules = await buildApp();
    const measure = performance.measure(markerName, { start: markerName });
    console.log(`✅ Built ${modules.length} module${modules.length !== 1 ? "s" : ""} in ${formatMeasure(measure)}.`);
    Deno.exit();
  } catch (err) {
    console.error(err instanceof Error ? err.stack : err.toString());
    Deno.exit(1);
  }
}

/**
 * Bundle a set of TypeScript modules.
 * @see [jsr:@libs/bundle:bundle](https://jsr.io/@libs/bundle/doc/ts/~/bundle#function_bundle_0)
 **/
export async function build(options?: { entryPoints?: string[] }): Promise<string[]> {
  // Ensure destination exists
  const js = new URL(import.meta.resolve("./../website/src/js")).pathname;
  if (!existsSync(js)) Deno.mkdirSync(js, { recursive: true });

  return Promise.all((options?.entryPoints ?? []).map(async entryPoint => {
    const entryPath = new URL(import.meta.resolve(`./../${entryPoint}`)).pathname;
    assert(existsSync(entryPath), `File not found: ${entryPath}`);
    const bundleName = path.basename(entryPath, path.extname(entryPath));
    const bundlePath = path.normalize(path.join(js, `${bundleName}.js`));
    await Deno.writeFile(
      bundlePath,
      await bundle(entryPath).catch(err => {
        console.error(`❌ ${bundlePath}`);
        throw err;
      })
    );
    console.log(`📝 ${bundlePath}`);
    return bundlePath;
  }));
}

async function bundle(root: string): Promise<string> {
  const dir = path.dirname(root);
  // TODO: Use the live URL in production builds
  const publicPath = import.meta?.dirname ?? "https://rct3.chancesnow.me";

  try {
    const result = await emit.bundle(root, {
      type: "classic",
      compilerOptions: {
        inlineSourceMap: true,
        jsx: "react",
        jsxFactory: "h",
        jsxFragment: 'Fragment',
      },
      allowRemote: true,
      importMap: {
        imports: {
          ...config.imports,
          ...importMap.imports
        }
      },
      // TODO: Turn this on in production builds
      minify: false,
    });

    const successful = result.code && result.code.length;
    if (!successful) throw new Error("Could not bundle entry.");
    // QUESTION: Do something with `result.outputFiles[0].hash`?

    const encoder = new TextEncoder();
    return encoder.encode(result.code);
  } catch (err) {
    throw new Error(err instanceof Error ? err.message : err.toString(), { cause: err });
  }
}

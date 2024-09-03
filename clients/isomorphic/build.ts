import * as esbuild from "esbuild";
import * as importMap from "npm:esbuild-plugin-import-map";
import { assert } from "@std/assert";
import { delay } from "@std/async";
import * as path from "@std/path";
import { existsSync } from "@std/fs";

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
    console.error(err instanceof Error ? err.message : err.toString());
    Deno.exit(1);
  }
}

/**
 * Bundle a set of TypeScript modules.
 * @see [jsr:@libs/bundle:bundle](https://jsr.io/@libs/bundle/doc/ts/~/bundle#function_bundle_0)
 **/
export async function build(options?: { entryPoints?: string[] }): Promise<string[]> {
  // Configure import map
  importMap.load([new URL(import.meta.resolve("./imports.json")).pathname]);

  // Ensure destination exists
  const js = new URL(import.meta.resolve("./../website/src/js")).pathname;
  if (!existsSync(js)) Deno.mkdirSync(js, { recursive: true });

  return Promise.all((options?.entryPoints ?? []).map(async entryPoint => {
    const entryPath = new URL(import.meta.resolve(`./../${entryPoint}`)).pathname;
    assert(existsSync(entryPath), `File not found: ${entryPath}`);
    const bundleName = path.basename(entryPath, path.extname(entryPath));
    const bundlePath = path.join(js, `${bundleName}.js`);
    await Deno.writeFile(
      bundlePath,
      await bundle(entryPath, { public: path.normalize(path.join(js, "..")), outFile: bundlePath })
        .catch(err => {
          console.error(`❌ ${bundlePath}`);
          throw err;
        })
    );
    console.log(`📝 ${bundlePath}`);
    return bundlePath;
  }));
}

async function bundle(filename: string, options: { public?: string, outFile: string }): Promise<string> {
  const dir = path.dirname(filename);
  // TODO: Use the live URL in production builds
  const publicPath = options.public ?? "https://rct3.chancesnow.me";
  const outfile = options.outFile;

  try {
    const result = await esbuild.build({
      bundle: true,
      target: [
        "es2020",
        "deno1"
      ],
      entryPoints: [filename],
      // TODO: Add $HOME/Library/Caches/deno/npm/registry.npmjs.org to Node module resolution
      nodePaths: [],
      plugins: [importMap.plugin()],
      jsx: "transform",
      jsxFactory: "h",
      jsxFragment: 'Fragment',
      // TODO: Add loaders for the files we import
      loader: { '.png': 'file' },
      publicPath,
      outfile,
      write: false,
      // TODO: Turn this on in production builds
      minify: false,
      // TODO: Turn this off in production builds
      sourcemap: "inline"
    });
    if (result.errors.length) throw new Error(result.errors.join("\n"));
    if (result.warnings.length) console.log(result.warnings.join("\n"));

    const successful = result.outputFiles && result.outputFiles.length;
    if (!successful) throw new Error("Could not compile source.");
    assert(result.outputFiles.length);
    // QUESTION: Do something with `result.outputFiles[0].hash`?
    return result.outputFiles[0].contents;
  } catch (err) {
    throw new Error(err instanceof Error ? err.message : err.toString(), { cause: err });
  }
}

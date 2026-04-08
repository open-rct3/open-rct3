import * as fs from "@std/fs";
import * as path from "@std/path";
import { Spinner } from "@std/cli/unstable-spinner";
import asc from "assemblyscript/asc";

import { setupFileLogger } from "../lib/log.ts";

const verbose = Deno.args.includes("--verbose") || Deno.args.includes("-v");
const denoDir = Deno.env.get("DENO_DIR")
  ?? (Deno.build.os === "windows"
    ? `${Deno.env.get("LOCALAPPDATA")}\\deno`
    : Deno.build.os === "darwin"
    ? `${Deno.env.get("HOME")}/Library/Caches/deno`
    : `${Deno.env.get("HOME")}/.cache/deno`);
const npmCachePath = path.join(denoDir, "npm", "registry.npmjs.org");

const PLUGINS_DIR = path.resolve(Deno.cwd(), "plugins");
const OUTPUT_DIR = path.resolve(Deno.cwd(), "bin", "plugins");

function formatDuration(ms: number): string {
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)}s`;
  const m = Math.floor(ms / 60_000);
  const s = Math.round((ms % 60_000) / 1000);
  return `${m}m ${s}s`;
}

interface BuildResult {
  name: string;
  skipped?: string;
  size?: number;
  ms?: number;
  error?: string;
  stderr?: string;
}

async function buildPlugin(pluginDir: string): Promise<BuildResult> {
  const pluginName = path.basename(pluginDir);
  const sourceFile = path.join(pluginDir, "index.ts");
  const outputFile = path.join(OUTPUT_DIR, `${pluginName}.wasm`);
  const configFile = path.join(pluginDir, "asconfig.json");

  try {
    await Deno.stat(sourceFile);
  } catch {
    return { name: pluginName, skipped: "no index.ts found" };
  }

  const start = performance.now();

  await ensureDir(OUTPUT_DIR);

  const tempConfigFile = path.join(tempDir, `${pluginName}.asconfig.json`);
  await Deno.writeTextFile(tempConfigFile, JSON.stringify({
    extends: configFile,
    options: {
      path: [shimsDir],
    },
  }));

  const args = [
    sourceFile,
    "--outFile", outputFile,
    "--noAssert",
    "--path", shimsDir,
    "--config", tempConfigFile,
    "--target", "release",
  ];

  const { error, stderr } = await asc.main(args, {
    readFile: (name: string, baseDir: string) => {
      const filePath = path.isAbsolute(name)
        ? name
        : path.join(baseDir, name);
      console.debug("readFile:", filePath);
      return Deno.readTextFile(filePath).catch(() => null);
    },
    writeFile: (name: string, data: string | Uint8Array, _baseDir: string) => {
      if (typeof data === "string") data = new TextEncoder().encode(data);
      return Deno.writeFile(name, data);
    },
    listFiles: () => Promise.resolve([]),
  });

  if (error) {
    return { name: pluginName, error: error.message, stderr: stderr.toString() };
  }

  const stats = await Deno.stat(outputFile);
  return { name: pluginName, size: stats.size, ms: performance.now() - start };
}

async function buildSharedTextures(): Promise<BuildResult> {
  const start = performance.now();
  const projectFile = path.resolve(Deno.cwd(), "OpenCobra", "Textures", "Textures.csproj");
  const outputWasm = path.resolve(OUTPUT_DIR, "OpenCobra.Textures.wasm");

  try {
    const process = new Deno.Command("dotnet", {
      args: [
        "publish",
        projectFile,
        "-c", "Release",
        "-f", "net8.0",
        "-r", "browser-wasm",
        "/p:NativeLib=Static",
        "-o", path.dirname(outputWasm)
      ],
      stdout: "piped",
      stderr: "piped",
    });

    const { success, stdout, stderr } = await process.output();

    if (!success) {
      return {
        name: "OpenCobra.Textures",
        error: "dotnet publish failed",
        stderr: new TextDecoder().decode(stderr)
      };
    }

    // Move the resulting wasm to the expected location if it's named differently
    // ILCompiler usually produces a .wasm file
    const builtWasm = path.join(path.dirname(outputWasm), "dotnet.native.wasm");
    try {
        await Deno.rename(builtWasm, outputWasm);
    } catch {
        // Might already be named correctly or produced elsewhere
    }

    const stats = await Deno.stat(outputWasm);
    return { name: "OpenCobra.Textures", size: stats.size, ms: performance.now() - start };
  } catch (e) {
    return { name: "OpenCobra.Textures", error: e.message };
  }
}

// TODO: Replace with fs.mkdirp, or equivalent
async function ensureDir(dir: string): Promise<void> {
  try {
    await Deno.stat(dir);
  } catch {
    await Deno.mkdir(dir, { recursive: true });
  }
}

async function main(): Promise<void> {
  if (!await fs.exists(PLUGINS_DIR)) {
    console.error(`Plugins directory not found: ${PLUGINS_DIR}`);
    Deno.exit(1);
  }

  await ensureAscShims();
  await ensureDir(OUTPUT_DIR);

  const entries = Deno.readDir(PLUGINS_DIR);
  const pluginDirs: string[] = [];
  for await (const entry of entries) {
    if (entry.isDirectory && entry.name) {
      pluginDirs.push(path.join(PLUGINS_DIR, entry.name));
    }
  }

  if (pluginDirs.length === 0) {
    console.log("No plugins found.");
    return;
  }

  const total = pluginDirs.length;
  const useSpinner = Deno.stderr.isTerminal();
  const spinnerMsg = (n: number) => `Compiling ${total} plugin${total !== 1 ? "s" : ""}... (${n}/${total})`;
  const spinner = useSpinner ? new Spinner({ message: spinnerMsg(0), color: "yellow" }) : null;
  spinner?.start();

  const sharedResult = await buildSharedTextures();
  if (sharedResult.error) {
    spinner?.stop();
    console.error(`✗ Failed to build shared textures: ${sharedResult.error}`);
    if (sharedResult.stderr) console.error(sharedResult.stderr);
    Deno.exit(1);
  }
  console.log(`✅ Built shared textures (${sharedResult.size} bytes, ${formatDuration(sharedResult.ms!)})`);

  let completed = 0;
  const results = await Promise.all(pluginDirs.map(async (pluginDir) => {
    const result = await buildPlugin(pluginDir);
    if (spinner) spinner.message = spinnerMsg(++completed);
    return result;
  }));

  spinner?.stop();
  console.log("");

  let failed = 0;
  for (const result of results) {
    if (result.skipped) {
      console.log(`- Skipped ${result.name}: ${result.skipped}`);
    } else if (result.error) {
      console.error(`✗ Failed to compile ${result.name}: ${result.error}`);
      if (result.stderr) console.error(result.stderr);
      failed++;
    } else {
      console.log(`✅ Built ${result.name} (${result.size} bytes, ${formatDuration(result.ms!)})`);
    }
  }

  console.log("");
  if (failed > 0) {
    console.error(`${failed} plugin${failed !== 1 ? "s" : ""} failed to compile.`);
    Deno.exit(1);
  } else {
    console.log("Build complete!");
  }
}

await setupFileLogger("build-plugins", verbose);

const tempDir = await Deno.makeTempDir();
const shimsDir = path.join(tempDir, "as-shims");
console.debug("Creating AssemblyScript shims:", shimsDir);

async function ensureAscShims(): Promise<void> {
  const pdkSrc = path.join(npmCachePath, "@extism", "as-pdk", "1.0.0");
  const shimDir = path.join(shimsDir, "@extism", "as-pdk");
  await fs.copy(pdkSrc, shimDir, { overwrite: true });
  await Deno.writeTextFile(path.join(shimDir, "package.json"), JSON.stringify({
    name: "@extism/as-pdk",
    version: "1.0.0",
    ascMain: "index.ts",
  }));
}

if (import.meta.main) main();

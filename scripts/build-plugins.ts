import * as fs from "@std/fs";
import * as path from "@std/path";
import asc from "assemblyscript/asc";

const PLUGINS_DIR = path.resolve(Deno.cwd(), "plugins");
const OUTPUT_DIR = path.resolve(Deno.cwd(), "bin", "plugins");

const denoDir = Deno.env.get("DENO_DIR")
  ?? (Deno.build.os === "windows"
    ? `${Deno.env.get("LOCALAPPDATA")}\\deno`
    : `${Deno.env.get("HOME")}/.deno`);
const npmCachePath = path.join(denoDir, "npm", "registry.npmjs.org");

async function buildPlugin(pluginDir: string): Promise<void> {
  const pluginName = path.basename(pluginDir);
  const sourceFile = path.join(pluginDir, "index.ts");
  const outputFile = path.join(OUTPUT_DIR, `${pluginName}.wasm`);
  const configFile = path.join(pluginDir, "asconfig.json");

  try {
    await Deno.stat(sourceFile);
  } catch {
    console.error(`Skipping ${pluginName}: no index.ts found`);
    return;
  }

  console.log(`Building ${pluginName}...`);

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
      console.log("readFile:", filePath);
      return Deno.readTextFile(filePath).catch(() => null);
    },
    writeFile: (name: string, data: string | Uint8Array, _baseDir: string) => {
      if (typeof data === "string") data = new TextEncoder().encode(data);
      return Deno.writeFile(name, data);
    },
    listFiles: () => Promise.resolve([]),
  });

  if (error) {
    console.error(`Failed to compile ${pluginName}: ${error.message}`);
    console.error(stderr.toString());
    // TODO: Prevent the "Build complete!" message from being printed at the end
    return;
  }

  const stats = await Deno.stat(outputFile);
  console.log(`✓ Built ${pluginName} (${stats.size} bytes)`);
}

// TODO: Replace with fs.mkdirp, or uquivalent
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

  console.log(`Found ${pluginDirs.length} plugin${pluginDirs.length !== 1 ? "s" : ""}.`);
  console.log("Compiling...");
  console.log("");

  for (const pluginDir of pluginDirs) {
    await buildPlugin(pluginDir);
  }

  console.log("");
  console.log("Build complete!");
}

const tempDir = await Deno.makeTempDir();
const shimsDir = path.join(tempDir, "as-shims");
console.log("shimsDir:", shimsDir);

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

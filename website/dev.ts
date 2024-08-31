import { ansi } from "@cliffy/ansi";
import { tty } from "@cliffy/ansi/tty";
import Kia from "kia";
import { abortable, debounce, delay } from "jsr:@std/async";
import { join } from "jsr:@std/path/join";

const encoder = new TextEncoder();
const self = import.meta.dirname ?? Deno.cwd();

// TODO: This script is convoluted. Make it simpler. Maybe extract the worker bootstrap effort?
function initWorker() {
  return new Worker(URL.createObjectURL(
      new Blob([`import site from "file://${join(self, "config.ts")}";

  // Ignore logs
  const log = globalThis.console.log;
  globalThis.console.log = (...args) => {};

  self.addEventListener("message", async (msg) => {
    if (msg.data.type !== "execute") return;
    await site.build();
    self.postMessage({ type: "resolve" });
  });`], { type: "text/javascript" })), { type: "module" });
}

function createBuildWorker() {
  let worker = initWorker();
  return {execute: function (options: { timeout?: number } = { timeout: 5000 }) {
    const signal = AbortSignal.timeout(options?.timeout ?? 5000);
    return abortable(new Promise((resolve, reject) => {
      worker.addEventListener("message", (e: MessageEvent) => {
        if (e.data.type === "resolve") resolve();
        else reject(e.data);
      });
      worker.addEventListener("error", (err: Error | any) => reject(err));
      signal.addEventListener("abort", () => {
        worker.terminate();
        worker = initWorker();
      });
      worker.postMessage({ type: "execute" });
    }), signal).catch(err => {
      if (err instanceof DOMException) throw new Error("Build took too long.", { cause: err });
      else if (err instanceof Error) throw err;
      else throw new Error(err.toString());
    });
  }};
}

const worker = createBuildWorker();
function getBuildTime() {
  performance.mark("afterBuild");
  const duration = performance.measure("", "beforeBuild", "afterBuild").duration / 1000;
  return duration >= 1 && duration.toFixed(1).endsWith(".0") ? Math.floor(duration) : duration.toFixed(1);
}
const build = debounce(async () => {
  const kia = new Kia("Building site…");
  kia.start();
  try {
    performance.mark("beforeBuild");
    await worker.execute();
    kia.succeed(`Built site in ${getBuildTime()}s.`);
  } catch (err) {
    kia.fail(err instanceof Error ? err.message : err.toString());
  } finally {
    await delay(500);
    kia.stop();
    Deno.stdout.writeSync(encoder.encode("Watching for changes…"));
  }
}, 250);

if (import.meta.main) {
  Deno.addSignalListener("SIGINT", async () => {
    Deno.stdout.writeSync(ansi.eraseLine.cursorLeft.bytes());
    Deno.stdout.writeSync(encoder.encode("^C"));
    Deno.exit();
  });

  await build();
  for await (const _ of Deno.watchFs(join(self, "src"), { recursive: true })) {
    Deno.stdout.writeSync(ansi.eraseLine.bytes());
    await build();
  }
}

async function getCursorPosition(): { x: number, y: number } {
  const ESC = "\x1B";
  const decoder = new TextDecoder();
  Deno.stdin.setRaw(true, { cbreak: true });
  Deno.stdout.writeSync(ansi.cursorPosition.bytes());
  const buf = new Uint8Array(255);
  Deno.stdin.readSync(buf);
  Deno.stdin.setRaw(false, { cbreak: true });

  const location = decoder.decode(buf).replace(ESC+'[', "").split(";").map(x => parseInt(x, 10));
  return { x: location[0], y: location[1] };
}

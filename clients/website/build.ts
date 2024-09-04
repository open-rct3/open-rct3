import * as path from "@std/path";

import site from "./config.ts";

export enum BuildState {
  success,
  failed,
  timeout,
  aborted
}

export class BuildStatus {
  readonly state: BuildState = BuildState.success;
  readonly measure: PerformanceMeasure | null = null;

  constructor(state: PerformanceMeasure | BuildState = BuildState.success) {
    this.state = typeof state === "string" ? state : BuildState.success;
    if (typeof state === "object") this.measure = state;
  }
}

const dirname = (function () {
  const dir = import.meta.dirname;
  if (dir) return path.normalize(path.resolve(dir, "..", ".."));
  return Deno.cwd();
})();

export default async function build(options?: { timeout?: number }) {
  const timeout = AbortSignal.timeout(options?.timeout ?? 10000);
  const shortCircuit = new AbortController();

  aborted(timeout).then(() => {
    if (shortCircuit.signal.aborted) return;

    console.error("❌ Build took too long.");
    throw new BuildStatus(BuildState.timeout);
  });

  // TODO: Refactor with WICG Observables. See https://github.com/WICG/observable#readme and chrome://flags/#observable-api
  aborted(shortCircuit.signal, shortCircuit).then(() => {
    // Short circuit if the build was successful.
    if (shortCircuit.signal.reason instanceof BuildStatus && shortCircuit.signal.reason.state === BuildState.success) return;

    console.debug(shortCircuit.signal.reason);
    console.debug("⏳ Aborting build…");
    throw new BuildStatus(BuildState.aborted);
  });

  // Abort the build if it's taken too long, or if the Deno process is interrupted.
  const abortBuild = () => shortCircuit.abort();
  timeout.addEventListener("abort", abortBuild);
  Deno.addSignalListener("SIGINT", abortBuild);
  const abortSignal = AbortSignal.any([timeout, shortCircuit.signal]);

  // TODO: Prepend an animated spinner to this line
  console.log("⏳ Compiling website…");

  const buildIsomorphic = new Deno.Command(Deno.execPath(), {
    cwd: dirname,
    args: ["task", "build:isomorphic"]
  });

  const result = await Promise.race([
    aborted(abortSignal).then(() => {
      throw new Error("🛑 Build was aborted.");
    }),
    new Promise<BuildStatus>((resolve, reject) => {
      const markerName = Object.getPrototypeOf(build).name;
      const _startMark = performance.mark(markerName);
      try {
        buildIsomorphic.spawn().status
          .then(() => {
            console.log("⏳ Building website…");
            return site.build();
          })
          .then(() => {
            const result = new BuildStatus(performance.measure(markerName, { start: markerName }));
            shortCircuit.abort(result);
            resolve(result);
          }).catch((err: unknown) => { throw err; });
      } catch (err) {
        console.error(err instanceof Error ? err.message : err.toString());
        reject(new BuildStatus(BuildState.failed));
      }
    })
  ]);
  if (result.state === BuildState.success) {
    console.log(`⏱ Built website in ${formatMeasure(result.measure!)}.`);
    console.log(`✅ Finished build.`);
  }

  Deno.removeSignalListener("SIGINT", abortBuild);
  return result;
}

export function formatMeasure(measure: PerformanceMeasure) {
  const durationSeconds = measure.duration / 1000;
  if (Math.round(measure.duration % 100) % 1 !== 0) return `${(durationSeconds).toFixed(1)}s`;
  return `${durationSeconds.toFixed(0)}s`;
}

if (import.meta.main) await build().then(() => Deno.exit(0));

function aborted(signal: AbortSignal, _resource?: unknown) {
  return new Promise(resolve => {
    signal.addEventListener("abort", resolve);
  });
}

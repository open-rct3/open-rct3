import { getLogger, RotatingFileHandler, setup } from "@std/log";
import * as path from "@std/path";

export async function setupFileLogger(name: string, verbose: boolean): Promise<void> {
  const logDir = path.join(
    Deno.env.get("HOME") ?? Deno.env.get("USERPROFILE") ?? ".",
    ".logs",
    "OpenRCT3",
  );
  await Deno.mkdir(logDir, { recursive: true });
  setup({
    handlers: {
      file: new RotatingFileHandler("DEBUG", {
        filename: path.join(logDir, `${name}.log`),
        maxBytes: 10_000_000,
        maxBackupCount: 5,
        formatter: (record) => `[${new Date().toISOString()}] ${record.msg}`,
      }),
    },
    loggers: {
      default: { level: "DEBUG", handlers: ["file"] },
    },
  });
  if (!verbose) console.debug = (...args: unknown[]) => getLogger().debug(args.map(String).join(" "));
}

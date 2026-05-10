import { CallContext } from "@extism/extism";

export const hostFunctions = {
  "env": {
    "abort": (_ctx: CallContext, message: number, fileName: number, lineNumber: number, columnNumber: number) => {
      console.error("Plugin aborted!");
      console.error(`${message} at ${fileName}(${lineNumber}:${columnNumber})`);
      Deno.exit(1);
    },
  },
};

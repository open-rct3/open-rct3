/**
 * @module
 * Ambient types and global augmentations for AssemblyScript plugins.
 *
 * @remarks
 * Deno's Language Server delegates type analysis to an embedded TypeScript compiler
 * ({@link https://github.com/denoland/deno/blob/62a0d5d6041a30aa3cb334f6889818c03158fcee/cli/lsp/tsc.rs}).
 *
 * Because `no-default-lib="true"` drops Deno's import maps, we must explicitly map the
 * AssemblyScript references using `compilerOptions.paths` in `deno.json`.
 */

/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />

declare global {
  interface StringConstructor {
    readonly UTF16: {
      decode(buf: ArrayBuffer): string;
      encode(str: string): ArrayBuffer;
      byteLength(str: string): i32;
    };
  }
}

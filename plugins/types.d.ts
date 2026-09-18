/**
 * @module
 * Ambient TypeScript declarations and global augmentations for Deno type
 * checking of AssemblyScript plugins.
 *
 * @remarks
 * AssemblyScript natively provides `String.UTF16.decode`
 * ({@link https://www.assemblyscript.org/stdlib/string.html#utf-16}), while
 * TypeScript requires augmenting `StringConstructor` during type checking.
 * Global augmentations reside in ambient `.d.ts` files to keep modules
 * compatible with the AssemblyScript compiler.
 */

declare global {
  interface StringConstructor {
    UTF16: {
      decode(buf: ArrayBuffer): string;
    };
  }
}
export {};

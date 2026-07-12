// Little-endian primitive readers shared by viewer plugins that parse raw OVL resource bytes.
// Previously duplicated verbatim across int-viewer, mam-viewer, snd-viewer, and spl-viewer.

export function readU16LE(data: Uint8Array, offset: i32): u16 {
  return u16(data[offset]) | (u16(data[offset + 1]) << 8);
}

export function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24));
}

export function readF32LE(data: Uint8Array, offset: i32): f32 {
  let bits = readU32LE(data, offset);
  return f32.reinterpret_i32(i32(bits));
}

// A relocated pointer field's *raw*, unresolved value - same bit layout as readU32LE, but named
// distinctly so callers reading pointer-typed fields (which the plugin host cannot dereference;
// see shs-viewer's README note) don't read as if they'd resolved a real address.
export function readRawPointer(data: Uint8Array, offset: i32): u32 {
  return readU32LE(data, offset);
}

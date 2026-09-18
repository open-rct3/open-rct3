/// <reference no-default-lib="true" />

// Converts indexed (palettized) pixel data to RGBA.
// palette must be 1024 bytes (256 × 4 RGBA entries).
// alphaPixels may be empty (length 0) — palette alpha is used, or 255 if palette is empty.
export function convertIndexedToRgba(
  indexedPixels: Uint8Array,
  palette: Uint8Array,
  alphaPixels: Uint8Array,
  outputRgba: Uint8ClampedArray,
): void {
  const pixelCount = indexedPixels.length;
  const hasAlpha = alphaPixels.length > 0;
  for (let i = 0; i < pixelCount; i++) {
    const dst = i * 4;
    const index = i32(indexedPixels[i]);
    const src = index * 4;
    outputRgba[dst + 0] = palette[src + 2];
    outputRgba[dst + 1] = palette[src + 1];
    outputRgba[dst + 2] = palette[src + 0];
    outputRgba[dst + 3] = hasAlpha ? alphaPixels[i] : palette[src + 3];
  }
}

/// <reference no-default-lib="true" />

// Converts indexed (palettized) pixel data to RGBA.
// palette must be 768 bytes (256 × 3 RGB entries).
// alphaPixels may be empty (length 0) — opaque alpha is assumed in that case.
export function convertIndexedToRgba(
  indexedPixels: Uint8Array,
  palette: Uint8Array,
  alphaPixels: Uint8Array,
  outputRgba: Uint8ClampedArray,
): void {
  const pixelCount = indexedPixels.length;
  const hasAlpha = alphaPixels.length > 0;
  for (let i = 0; i < pixelCount; i++) {
    const index = i32(indexedPixels[i]);
    const dst = i * 4;
    outputRgba[dst + 0] = palette[index * 3 + 0];
    outputRgba[dst + 1] = palette[index * 3 + 1];
    outputRgba[dst + 2] = palette[index * 3 + 2];
    outputRgba[dst + 3] = hasAlpha ? alphaPixels[i] : 255;
  }
}

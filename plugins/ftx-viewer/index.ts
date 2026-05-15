/// <reference no-default-lib="true" />
import { Host } from "@extism/as-pdk";
import "../types.ts";
import { convertIndexedToRgba } from "../palette-converter.ts";

export function name(): i32 {
  Host.outputString("Flexi-Texture Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["ftx", "flt"]');
  return 0;
}

function decodeU32(data: Uint8Array, offset: i32): u32 {
  return u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24);
}

function base64Encode(data: Uint8Array): string {
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  let result = "";
  let i = 0;
  while (i < data.length) {
    const byte1 = data[i++];
    const byte2 = i < data.length ? data[i++] : -1;
    const byte3 = i < data.length ? data[i++] : -1;

    const enc1 = byte1 >> 2;
    const enc2 = ((byte1 & 3) << 4) | (byte2 != -1 ? byte2 >> 4 : 0);
    const enc3 = byte2 != -1 ? ((byte2 & 15) << 2) | (byte3 != -1 ? byte3 >> 6 : 0) : 64;
    const enc4 = byte3 != -1 ? byte3 & 63 : 64;

    result += chars.charAt(enc1);
    result += chars.charAt(enc2);
    result += enc3 != 64 ? chars.charAt(enc3) : "=";
    result += enc4 != 64 ? chars.charAt(enc4) : "=";
  }
  return result;
}

export function render(): i32 {
  const data = Host.input();
  if (data.length === 0) {
    Host.outputString("<p class='error'>Invalid flexi-texture data: empty input.</p>");
    return 0;
  }

  const scale = decodeU32(data, 0);
  const width = decodeU32(data, 4);
  const height = decodeU32(data, 8);
  const _fps = decodeU32(data, 12);
  const recolorable = decodeU32(data, 16);
  // Skip past offsets, frame count, and repeated scale, width, height, and recolorable flags
  let offset = 36 + 16;

  // Read palette and texture data
  const palette = data.slice(offset, offset + 1024);
  offset += 1024;

  const pixelCount = i32(width * height);
  if (data.length < offset + pixelCount) {
    Host.outputString("<p class='error'>Invalid flexi-texture data: missing texture data.</p>");
    return 0;
  }
  const textureData = data.slice(offset, offset + pixelCount);
  offset += pixelCount;

  let alphaData = new Uint8Array(0);
  if (data.length >= offset + pixelCount) {
    alphaData = data.slice(offset, offset + pixelCount);
  }

  const rgba = new Uint8ClampedArray(pixelCount * 4);
  convertIndexedToRgba(textureData, palette, alphaData, rgba);

  // File Header (14 bytes) + DIB Header (BITMAPV5HEADER, 124 bytes)
  const headerSize = 14 + 124;
  const fileSize = headerSize + rgba.length;
  const bmp = new Uint8Array(fileSize);

  // File Header
  bmp[0] = 0x42;
  bmp[1] = 0x4D; // "BM"
  const size = u32(fileSize);
  bmp[2] = u8(size);
  bmp[3] = u8(size >> 8);
  bmp[4] = u8(size >> 16);
  bmp[5] = u8(size >> 24);
  bmp[10] = u8(headerSize);
  bmp[11] = u8(headerSize >> 8); // Offset to pixel data

  // DIB Header (BITMAPV5HEADER)
  bmp[14] = 124; // Header size
  const w = i32(width);
  bmp[18] = u8(w);
  bmp[19] = u8(w >> 8);
  bmp[20] = u8(w >> 16);
  bmp[21] = u8(w >> 24);
  const h = i32(-height); // Top-down
  bmp[22] = u8(h);
  bmp[23] = u8(h >> 8);
  bmp[24] = u8(h >> 16);
  bmp[25] = u8(h >> 24);
  bmp[26] = 1;
  bmp[27] = 0; // Planes
  bmp[28] = 32;
  bmp[29] = 0; // Bit count
  bmp[30] = 3;
  bmp[31] = 0; // BI_BITFIELDS
  const imgSize = u32(rgba.length);
  bmp[34] = u8(imgSize);
  bmp[35] = u8(imgSize >> 8);
  bmp[36] = u8(imgSize >> 16);
  bmp[37] = u8(imgSize >> 24);

  // Red, Green, Blue, Alpha masks
  bmp[54] = 0xFF; // R
  bmp[59] = 0xFF; // G
  bmp[64] = 0xFF; // B
  bmp[69] = 0xFF; // A

  // RGBA → BGRA (BMP BI_BITFIELDS convention)
  for (let i = 0; i < pixelCount; i++) {
    const src = i * 4;
    const dst = headerSize + i * 4;
    bmp[dst + 0] = rgba[src + 2]; // B
    bmp[dst + 1] = rgba[src + 1]; // G
    bmp[dst + 2] = rgba[src + 0]; // R
    bmp[dst + 3] = rgba[src + 3]; // A
  }

  const base64 = base64Encode(bmp);
  const dataUrl = "data:image/bmp;base64," + base64;

  let html = "<div class='ftx-viewer'>";
  html += "  <div class='info'>";
  html += "    <span>Size: " + width.toString() + "x" + height.toString() + "</span>";
  html += "    <span>Scale: " + scale.toString() + "</span>";
  html += "    <span>Recolorable: " + recolorable.toString() + "</span>";
  html += "  </div>";
  html += "  <div class='image-container'>";
  html += "    <img src='" + dataUrl + "' alt='Flexi-texture' style='image-rendering: pixelated; max-width: 100%;' />";
  html += "  </div>";
  html += "</div>";

  Host.outputString(html);
  return 0;
}

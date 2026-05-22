/// <reference no-default-lib="true" />
import { Host } from "@extism/as-pdk";
import { encodeBase64 } from "../lib/base64.ts";
import { renderHexView } from "../lib/hexViewer.ts";
import "../types.ts";

export function name(): i32 {
  Host.outputString("Texture Viewer");
  return 0;
}

export function version(): i32 {
  Host.outputString("0.2.0");
  return 0;
}

export function file_types(): i32 {
  Host.outputString('["btbl", "tex"]');
  return 0;
}

export function render(): i32 {
  const data = Host.input();
  if (data.length < 8) {
    Host.outputString("<p class='error'>Invalid data: file too small.</p>");
    if (data.length > 0) Host.outputString(renderHexView(data));
    return 0;
  }

  let html = "";

  // Determine whether the source data is a plain texture or bitmap table
  const firstU32_sentinel = decodeU32(data, 0);
  if (firstU32_sentinel === 0x00070007) html += renderTex(data);
  else html += renderBitmapTable(data);

  // FIXME: ONLY render the hex-dump if the data is not a plain texture or bitmap table
  html += renderHexView(data);

  Host.outputString(html);
  return 0;
}

// Convert ABGR (from A8R8G8B8 format) to BGR for BMP
function abgrToBgr(abgr: u32): u32 {
  const _a = (abgr >> 24) & 0xFF;
  const b = (abgr >> 16) & 0xFF;
  const g = (abgr >> 8) & 0xFF;
  const r = abgr & 0xFF;
  return (r << 16) | (g << 8) | b;
}

// Decode 16-bit R5G6B5 to 24-bit BGR
function r5g6b5ToBgr(value: u16): u32 {
  const r = ((value >> 11) & 0x1F) << 3;
  const g = ((value >> 5) & 0x3F) << 2;
  const b = (value & 0x1F) << 3;
  return (r << 16) | (g << 8) | b;
}

// Decode 24-bit R8G8B8 to 24-bit BGR
function r8g8b8ToBgr(r: u8, g: u8, b: u8): u32 {
  return (r << 16) | (g << 8) | b;
}

// Create BMP file from raw pixel data
// Returns null if format is unsupported
function createBmp(width: u32, height: u32, format: u32, pixelData: Uint8Array): Uint8Array | null {
  if (width == 0 || height == 0) return null;

  const rowSize = ((width * 3 + 3) / 4) * 4; // Padded to 4 bytes
  const pixelDataSize = rowSize * height;
  const fileSize = 54 + pixelDataSize; // 14 byte header + 40 byte info + pixel data

  const bmp = new Uint8Array(fileSize);
  let offset = 0;

  // BITMAPFILEHEADER (14 bytes)
  // bfType
  bmp[offset++] = 0x42; // 'B'
  bmp[offset++] = 0x4D; // 'M'
  // bfSize
  bmp[offset++] = fileSize & 0xFF;
  bmp[offset++] = (fileSize >> 8) & 0xFF;
  bmp[offset++] = (fileSize >> 16) & 0xFF;
  bmp[offset++] = (fileSize >> 24) & 0xFF;
  // bfReserved1
  bmp[offset++] = 0; bmp[offset++] = 0;
  // bfReserved2
  bmp[offset++] = 0; bmp[offset++] = 0;
  // bfOffBits
  bmp[offset++] = 54; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;

  // BITMAPINFOHEADER (40 bytes)
  // biSize
  bmp[offset++] = 40; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biWidth
  bmp[offset++] = width & 0xFF;
  bmp[offset++] = (width >> 8) & 0xFF;
  bmp[offset++] = (width >> 16) & 0xFF;
  bmp[offset++] = (width >> 24) & 0xFF;
  // biHeight (negative for top-down)
  const negHeight = i32(-i32(height));
  bmp[offset++] = negHeight & 0xFF;
  bmp[offset++] = (negHeight >> 8) & 0xFF;
  bmp[offset++] = (negHeight >> 16) & 0xFF;
  bmp[offset++] = (negHeight >> 24) & 0xFF;
  // biPlanes
  bmp[offset++] = 1; bmp[offset++] = 0;
  // biBitCount
  bmp[offset++] = 24; bmp[offset++] = 0;
  // biCompression (BI_RGB = 0)
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biSizeImage
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biXPelsPerMeter
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biYPelsPerMeter
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biClrUsed
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;
  // biClrImportant
  bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0; bmp[offset++] = 0;

  // Pixel data - convert based on format
  let srcOffset = 0;
  for (let y = 0; y < i32(height); y++) {
    for (let x = 0; x < i32(width); x++) {
      let bgr: u32;

      if (format == 0x02) { // A8R8G8B8
        if (srcOffset + 4 > pixelData.length) return null;
        const abgr = u32(pixelData[srcOffset]) | (u32(pixelData[srcOffset + 1]) << 8) |
                    (u32(pixelData[srcOffset + 2]) << 16) | (u32(pixelData[srcOffset + 3]) << 24);
        bgr = abgrToBgr(abgr);
        srcOffset += 4;
      } else if (format == 0x04) { // R5G6B5
        if (srcOffset + 2 > pixelData.length) return null;
        const value = u16(pixelData[srcOffset]) | (u16(pixelData[srcOffset + 1]) << 8);
        bgr = r5g6b5ToBgr(value);
        srcOffset += 2;
      } else if (format == 0x01) { // R8G8B8
        if (srcOffset + 3 > pixelData.length) return null;
        bgr = r8g8b8ToBgr(pixelData[srcOffset], pixelData[srcOffset + 1], pixelData[srcOffset + 2]);
        srcOffset += 3;
      } else {
        return null; // Unsupported format
      }

      // Write BGR (BMP is bottom-up, so we write in reverse y order)
      const destY = height - 1 - u32(y);
      const paddedRowSize = ((width * 3 + 3) / 4) * 4;
      const pixelOffset = 54 + destY * paddedRowSize + x * 3;
      bmp[pixelOffset] = bgr & 0xFF;
      bmp[pixelOffset + 1] = (bgr >> 8) & 0xFF;
      bmp[pixelOffset + 2] = (bgr >> 16) & 0xFF;
    }
  }

  return bmp;
}

function decodeU32(data: Uint8Array, offset: i32): u32 {
  if (offset + 4 > data.length) return 0;
  return u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24);
}

function decodeF32(data: Uint8Array, offset: i32): f32 {
  if (offset + 4 > data.length) return 0.0;
  // @ts-ignore: dataStart is an AssemblyScript-specific property
  return load<f32>(data.dataStart + offset);
}

function toHex(value: u32, width: i32 = 8): string {
  let hex = value.toString(16).toUpperCase();
  while (hex.length < width) {
    hex = "0" + hex;
  }
  return "0x" + hex;
}

function getFormatName(format: u32): string {
  switch (format) {
    case 0x02: return "A8R8G8B8";
    case 0x12: return "Dxt1";
    case 0x13: return "Dxt3";
    default: return "Unknown (" + toHex(format) + ")";
  }
}

const ERROR_FORMAT_UNSUPPORTED = -1;

function getBitsPerPixel(format: u32): u32 {
  switch (format) {
    case 0x02: return 32;
    case 0x12: return 4;
    case 0x13: return 8;
    default: return ERROR_FORMAT_UNSUPPORTED;
  }
}

function renderTex(data: Uint8Array): string {
  const count = decodeU32(data, 32);
  const unk12 = decodeU32(data, 44);
  const flicCount = Math.max(count, unk12);

  let html = "<h3>Texture Header (Tex)</h3>";
  html += "<table><thead><tr><th>Field</th><th>Value</th></tr></thead><tbody>";
  html += "<tr><td>Count</td><td>" + count.toString() + "</td></tr>";
  html += "<tr><td>Unk10</td><td>" + toHex(decodeU32(data, 36)) + "</td></tr>";
  html += "<tr><td>Unk11</td><td>" + toHex(decodeU32(data, 40)) + "</td></tr>";
  html += "<tr><td>Unk12</td><td>" + unk12.toString() + "</td></tr>";
  html += "</tbody></table>";

  html += "<h3>Flic Entries (" + flicCount.toString() + ")</h3>";
  html += "<table><thead><tr><th>#</th><th>DataRelocation</th><th>Unk1</th><th>Unk2 (float)</th></tr></thead><tbody>";

  let offset = 48;
  for (let i = 0; i < i32(flicCount); i++) {
    if (offset + 12 > data.length) break;
    const dataReloc = decodeU32(data, offset);
    const unk1 = decodeU32(data, offset + 4);
    const unk2 = decodeF32(data, offset + 8);
    html += "<tr><td>" + i.toString() + "</td><td>" + toHex(dataReloc) + "</td><td>" + toHex(unk1) + "</td><td>" + unk2.toString() + "</td></tr>";
    offset += 12;
  }
  html += "</tbody></table>";

  return html;
}

function renderBitmapTable(data: Uint8Array): string {
  const unk = decodeU32(data, 0);
  const length = decodeU32(data, 4);

  let html = "<h3>Bitmap Table Header</h3>";
  html += "<table><thead><tr><th>Field</th><th>Value</th></tr></thead><tbody>";
  html += "<tr><td>Unk</td><td>" + toHex(unk) + "</td></tr>";
  html += "<tr><td>Length</td><td>" + length.toString() + "</td></tr>";
  html += "</tbody></table>";

  html += "<h3>Bitmap Entries</h3>";
  html += "<table><thead><tr><th>#</th><th>Preview</th><th>Format</th><th>Width</th><th>Height</th><th>MipCount</th><th>Data Size</th></tr></thead><tbody>";

  let offset = 16;
  for (let i = 0; i < i32(length); i++) {
    if (offset + 16 > data.length) break;
    const format = decodeU32(data, offset);
    const width = decodeU32(data, offset + 4);
    const height = decodeU32(data, offset + 8);
    const mipCount = decodeU32(data, offset + 12);

    const bpp = getBitsPerPixel(format);
    if (bpp == ERROR_FORMAT_UNSUPPORTED) continue;
    const size = width * height * bpp / 8;

    // Try to create BMP preview
    let preview = "";
    offset += 16; // FlicHeader size
    if (width > 0 && height > 0 && size > 0) {
      const pixelData = new Uint8Array(i32(size));
      for (let j = 0; j < i32(size) && (offset + j) < data.length; j++) {
        pixelData[j] = data[offset + j];
      }

      const bmp = createBmp(width, height, format, pixelData);
      if (bmp != null) {
        const base64 = encodeBase64(bmp);
        preview = "<img src=\"data:image/bmp;base64," + base64 + "\" style=\"max-width:128px;max-height:128px;\" />";
      } else {
        preview = "<span style=\"color:#666\">Unsupported: " + getFormatName(format) + "</span>";
      }
    }

    html += "<tr><td>" + i.toString() + "</td><td>" + preview + "</td><td>" + getFormatName(format) + "</td><td>" + width.toString() + "</td><td>" + height.toString() + "</td><td>" + mipCount.toString() + "</td><td>" + size.toString() + "</td></tr>";

    // Skip pixel data
    offset += i32(size * mipCount);
  }
  html += "</tbody></table>";

  return html;
}

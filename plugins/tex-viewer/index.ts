/// <reference no-default-lib="true" />
import { Host } from "@extism/as-pdk";
import "../types.ts";
import { renderHexView } from "../lib/hexViewer.ts";

export function name(): i32 {
  Host.outputString("Texture Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["btbl", "tex"]');
  return 0;
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
    case 0x01: return "R8G8B8";
    case 0x02: return "A8R8G8B8";
    case 0x03: return "X8R8G8B8";
    case 0x04: return "R5G6B5";
    case 0x05: return "X1R5G5B5";
    case 0x07: return "P8";
    case 0x08: return "A1R5G5B5";
    case 0x09: return "X4R4G4B4";
    case 0x0A: return "A4R4G4B4";
    case 0x0B: return "L8";
    case 0x0C: return "A8L8";
    case 0x0E: return "V8U8";
    case 0x10: return "Uyvy";
    case 0x11: return "Yuy2";
    case 0x12: return "Dxt1";
    case 0x13: return "Dxt3";
    case 0x14: return "Dxt5";
    case 0x15: return "R3G3B2";
    case 0x16: return "A8";
    case 0x100: return "D16";
    case 0x101: return "D32";
    case 0x102: return "D15S1";
    case 0x103: return "D24S8";
    default: return "Unknown (" + toHex(format) + ")";
  }
}

function getBitsPerPixel(format: u32): u32 {
  switch (format) {
    case 0x12: return 4;
    case 0x13:
    case 0x14:
    case 0x15:
    case 0x16:
    case 0x0B:
    case 0x07: return 8;
    case 0x04:
    case 0x05:
    case 0x08:
    case 0x09:
    case 0x0A:
    case 0x0C:
    case 0x0E:
    case 0x10:
    case 0x11:
    case 0x100:
    case 0x102: return 16;
    case 0x01: return 24;
    case 0x02:
    case 0x03:
    case 0x101:
    case 0x103: return 32;
    default: return 0;
  }
}

function renderTex(data: Uint8Array): string {
  const count = decodeU32(data, 32);
  const unk12 = decodeU32(data, 44);
  const flicCount = Math.max(count, unk12);

  let html = "<h3>Texture Header (Tex)</h3>";
  html += "<table><thead><tr><th>Field</th><th>Value</th></tr></thead><tbody>";
  for (let i = 0; i < 8; i++) {
    html += "<tr><td>Unk" + (i + 1).toString() + "</td><td>" + toHex(decodeU32(data, i * 4)) + "</td></tr>";
  }
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
  html += "<table><thead><tr><th>#</th><th>Format</th><th>Width</th><th>Height</th><th>MipCount</th><th>Data Size</th></tr></thead><tbody>";

  let offset = 16;
  for (let i = 0; i < i32(length); i++) {
    if (offset + 16 > data.length) break;
    const format = decodeU32(data, offset);
    const width = decodeU32(data, offset + 4);
    const height = decodeU32(data, offset + 8);
    const mipCount = decodeU32(data, offset + 12);

    const bpp = getBitsPerPixel(format);
    const size = width * height * bpp / 8;

    html += "<tr><td>" + i.toString() + "</td><td>" + getFormatName(format) + "</td><td>" + width.toString() + "</td><td>" + height.toString() + "</td><td>" + mipCount.toString() + "</td><td>" + size.toString() + "</td></tr>";

    offset += 16; // FlicHeader size
    // Skip pixel data
    offset += i32(size * mipCount);
  }
  html += "</tbody></table>";
  
  return html;
}

export function render(): i32 {
  const data = Host.input();
  if (data.length < 8) {
    Host.outputString("<p class='error'>Invalid data: file too small.</p>");
    if (data.length > 0) Host.outputString(renderHexView(data));
    return 0;
  }

  const firstU32 = decodeU32(data, 0);
  
  let html = "<div class='tex-viewer'>";
  if (firstU32 === 0x00070007) {
    html += renderTex(data);
  } else {
    html += renderBitmapTable(data);
  }

  html += renderHexView(data);
  html += "</div>";

  Host.outputString(html);
  return 0;
}

/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";

export function name(): i32 {
  Host.outputString("Spline Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["spl"]');
  return 0;
}

function toHexByte(value: u32): string {
  let hex = value.toString(16).toUpperCase();
  if (hex.length == 1) hex = "0" + hex;
  return hex;
}

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24));
}

function readF32LE(data: Uint8Array, offset: i32): f32 {
  let bits = readU32LE(data, offset);
  return f32.reinterpret_i32(i32(bits));
}

function renderSpline(data: Uint8Array): string {
  if (data.length < 20) {
    return "<p class='error'>Data too short to contain spline header (minimum 20 bytes required).</p>" +
      renderHexView(data);
  }

  let nodecount = readU32LE(data, 0);
  let cyclic = readU32LE(data, 4);
  let totallength = readF32LE(data, 8);
  let inv_totallength = readF32LE(data, 12);
  let max_y = readF32LE(data, 16);

  let html = "<div class='spline-viewer'>";
  html += "<h3>Spline Summary</h3>";
  html += "<table class='spline-summary'><tbody>";
  html += "<tr><td>Node Count</td><td>" + nodecount.toString() + "</td></tr>";
  html += "<tr><td>Cyclic</td><td>" + (cyclic != 0 ? "Yes" : "No") + "</td></tr>";
  html += "<tr><td>Total Length</td><td>" + totallength.toString() + "</td></tr>";
  html += "<tr><td>Inverse Total Length</td><td>" + inv_totallength.toString() + "</td></tr>";
  html += "<tr><td>Max Y</td><td>" + max_y.toString() + "</td></tr>";
  html += "</tbody></table>";

  // Segment count
  let segmentCount = nodecount - (cyclic != 0 ? 0 : 1);
  html += "<p class='info'>Segments: " + segmentCount.toString() + "</p>";

  html += "</div>";
  html += renderHexView(data);

  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderSpline(data);
  Host.outputString(html);
  return 0;
}

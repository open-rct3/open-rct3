/// <reference no-default-lib="true" />
import { Host } from "@extism/as-pdk";
import "../types.ts";

export function name(): string { return "Manifold Mesh Viewer"; }
export function version(): string { return "0.1.0"; }
export function file_types(): string { return '["mam"]'; }

function toHex(value: u32, width: i32 = 8): string {
  let hex = value.toString(16).toUpperCase();
  let padding = "";
  for (let j = 0; j < width - hex.length; j++) {
    padding += "0";
  }
  return "0x" + padding + hex;
}

function toHexByte(value: u32): string {
  let hex = value.toString(16).toUpperCase();
  if (hex.length == 1) hex = "0" + hex;
  return hex;
}

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) | (u32(data[offset + 3]) << 24));
}

function readF32LE(data: Uint8Array, offset: i32): f32 {
  let bits = readU32LE(data, offset);
  return f32.reinterpret_i32(i32(bits));
}

function readU16LE(data: Uint8Array, offset: i32): u16 {
  return u16(data[offset]) | (u16(data[offset + 1]) << 8);
}

function renderHexView(data: Uint8Array, startOffset: i32 = 0): string {
  let html = "<div class='hex-view'><table><thead><tr><th>Offset</th>";
  for (let h = 0; h < 16; h++) {
    html += "<th>" + h.toString(16).toUpperCase() + "</th>";
  }
  html += "<th>ASCII</th></tr></thead><tbody>";

  let rowCount = data.length / 16;
  if (rowCount > 32) rowCount = 32;  // Limit to 32 rows for performance

  for (let r = 0; r < rowCount; r++) {
    let offset = r * 16;
    html += "<tr><td>" + toHex(startOffset + offset, 4) + "</td>";

    let ascii = "";
    for (let i = 0; i < 16; i++) {
      let idx = offset + i;
      if (idx < data.length) {
        let byte = data[idx];
        html += "<td>" + toHexByte(byte) + "</td>";
        if (byte >= 32 && byte < 127) {
          ascii += String.fromCharCode(byte);
        } else {
          ascii += ".";
        }
      } else {
        html += "<td></td>";
      }
    }

    html += "<td>" + ascii + "</td></tr>";
  }

  if (rowCount * 16 < data.length) {
    html += "<tr><td colspan='18' class='truncated'>... data truncated (" + data.length.toString() + " bytes total)</td></tr>";
  }

  html += "</tbody></table></div>";
  return html;
}

function renderMesh(data: Uint8Array): string {
  // ManifoldMesh struct layout:
  // bbox_min (3 floats = 12 bytes)
  // unk04 (1 float = 4 bytes) — unknown field after bbox_min
  // bbox_max (3 floats = 12 bytes)
  // unk04 (1 float = 4 bytes) — unknown field after bbox_max
  // vertex_count (u32 = 4 bytes)
  // manifoldface_count (u32 = 4 bytes)
  // Total header: 12+4+12+4+4+4 = 40 bytes minimum

  if (data.length < 40) {
    return "<p class='error'>Data too short to contain manifold mesh header (minimum 40 bytes required).</p>" + renderHexView(data);
  }

  let bbox_min_x = readF32LE(data, 0);
  let bbox_min_y = readF32LE(data, 4);
  let bbox_min_z = readF32LE(data, 8);
  let bbox_min_unk = readF32LE(data, 12);

  let bbox_max_x = readF32LE(data, 16);
  let bbox_max_y = readF32LE(data, 20);
  let bbox_max_z = readF32LE(data, 24);
  let bbox_max_unk = readF32LE(data, 28);

  let vertex_count = readU32LE(data, 32);
  let manifoldface_count = readU32LE(data, 36);

  let html = "<div class='mesh-viewer'>";
  html += "<h3>Manifold Mesh Metadata</h3>";
  html += "<table class='mesh-summary'><tbody>";

  html += "<tr><td colspan='2'><strong>Bounding Box Min</strong></td></tr>";
  html += "<tr><td>  X</td><td>" + bbox_min_x.toString() + "</td></tr>";
  html += "<tr><td>  Y</td><td>" + bbox_min_y.toString() + "</td></tr>";
  html += "<tr><td>  Z</td><td>" + bbox_min_z.toString() + "</td></tr>";
  html += "<tr><td>  Unk</td><td>" + bbox_min_unk.toString() + "</td></tr>";

  html += "<tr><td colspan='2'><strong>Bounding Box Max</strong></td></tr>";
  html += "<tr><td>  X</td><td>" + bbox_max_x.toString() + "</td></tr>";
  html += "<tr><td>  Y</td><td>" + bbox_max_y.toString() + "</td></tr>";
  html += "<tr><td>  Z</td><td>" + bbox_max_z.toString() + "</td></tr>";
  html += "<tr><td>  Unk</td><td>" + bbox_max_unk.toString() + "</td></tr>";

  html += "<tr><td>Vertices</td><td>" + vertex_count.toString() + "</td></tr>";
  html += "<tr><td>Faces (Triangles)</td><td>" + manifoldface_count.toString() + "</td></tr>";

  let triangle_count = manifoldface_count;
  html += "<tr><td>Triangle Count</td><td>" + triangle_count.toString() + "</td></tr>";

  // Estimate data layout (vertices are typically 12 or 16 bytes each, indices are 2 bytes)
  let vertex_size_estimate = 12;  // 3 floats minimum
  let indices_size_estimate = manifoldface_count * 3 * 2;  // 3 indices per triangle, 2 bytes each
  let estimated_vertex_data = vertex_count * vertex_size_estimate;
  let estimated_total = 40 + estimated_vertex_data + indices_size_estimate;

  html += "<tr><td>Estimated Data Size</td><td>" + estimated_total.toString() + " bytes (actual: " + data.length.toString() + ")</td></tr>";

  html += "</tbody></table>";
  html += "</div>";

  html += renderHexView(data, 0);

  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderMesh(data);
  Host.outputString(html);
  return 0;
}

import { Host } from "@extism/as-pdk";

export function name(): string { return "Spline Viewer"; }
export function version(): string { return "0.1.0"; }
export function file_types(): string { return '["spl"]'; }

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

function renderHexView(data: Uint8Array): string {
  let html = "<div class='hex-view'><table><thead><tr><th>Offset</th>";
  for (let h = 0; h < 16; h++) {
    html += "<th>" + h.toString(16).toUpperCase() + "</th>";
  }
  html += "<th>ASCII</th></tr></thead><tbody>";

  let rowCount = data.length / 16;
  for (let r = 0; r < rowCount; r++) {
    let offset = r * 16;
    html += "<tr><td>" + toHex(offset, 4) + "</td>";

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

  html += "</tbody></table></div>";
  return html;
}

function renderSpline(data: Uint8Array): string {
  if (data.length < 20) {
    return "<p class='error'>Data too short to contain spline header (minimum 20 bytes required).</p>" + renderHexView(data);
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

import { Host } from "@extism/as-pdk";
import { Ovl } from "../lib/ovl.ts";
import "../types.ts";

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

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24));
}

function readF32LE(data: Uint8Array, offset: i32): f32 {
  let bits = readU32LE(data, offset);
  return f32.reinterpret_i32(i32(bits));
}

function renderSpline(data: Uint8Array): string {
  if (data.length < 32) {
    return "<p class='error'>Data too short to contain spline header (minimum 32 bytes required).</p>";
  }

  const nodecount = readU32LE(data, 0);
  const nodesPtr = readU32LE(data, 4);
  const cyclic = readU32LE(data, 8);
  const totallength = readF32LE(data, 12);
  const inv_totallength = readF32LE(data, 16);
  const lengthsPtr = readU32LE(data, 20);
  const dataPtr = readU32LE(data, 24);
  const max_y = readF32LE(data, 28);

  let html = "<div class='spline-viewer'>";
  html += "<h3>Spline Summary</h3>";
  html += "<table class='spline-summary'><tbody>";
  html += "<tr><td>Node Count</td><td>" + nodecount.toString() + "</td></tr>";
  html += "<tr><td>Cyclic</td><td>" + (cyclic != 0 ? "Yes" : "No") + "</td></tr>";
  html += "<tr><td>Total Length</td><td>" + totallength.toString() + "</td></tr>";
  html += "<tr><td>Inverse Total Length</td><td>" + inv_totallength.toString() + "</td></tr>";
  html += "<tr><td>Max Y</td><td>" + max_y.toString() + "</td></tr>";
  html += "</tbody></table>";

  const segmentCount = nodecount - (cyclic != 0 ? 0 : 1);

  html += "<h3>Nodes (" + nodecount.toString() + ")</h3>";
  const nodesData = Ovl.resolvePointer(i64(nodesPtr));
  if (nodesData != null) {
    html += "<table class='nodes'><thead><tr><th>Node</th><th>Pos X</th><th>Pos Y</th><th>Pos Z</th></tr></thead><tbody>";
    for (let i = 0; i < i32(nodecount); i++) {
      const offset = i * 36;
      const posX = readF32LE(nodesData, offset);
      const posY = readF32LE(nodesData, offset + 4);
      const posZ = readF32LE(nodesData, offset + 8);
      html += "<tr><td>" + i.toString() + "</td><td>" + posX.toString() + "</td><td>" + posY.toString() + "</td><td>" + posZ.toString() + "</td></tr>";
    }
    html += "</tbody></table>";
  } else {
    html += "<p class='error'>Failed to resolve nodes pointer.</p>";
  }

  html += "<h3>Segments (" + segmentCount.toString() + ")</h3>";
  const lengthsData = Ovl.resolvePointer(i64(lengthsPtr));
  if (lengthsData != null) {
    html += "<table class='segment-lengths'><thead><tr><th>Segment</th><th>Length</th></tr></thead><tbody>";
    for (let i = 0; i < i32(segmentCount); i++) {
      const segLen = readF32LE(lengthsData, i * 4);
      html += "<tr><td>" + i.toString() + "</td><td>" + segLen.toString() + "</td></tr>";
    }
    html += "</tbody></table>";
  } else {
    html += "<p class='error'>Failed to resolve segment lengths pointer.</p>";
  }

  const dataData = Ovl.resolvePointer(i64(dataPtr));
  if (dataData != null && segmentCount <= 50) {
    html += "<h3>Segment Distance Samples (Bezier Curve Interpolation)</h3>";
    html += "<p class='info'>14 normalized distance markers per segment at 1/15th intervals along the cubic bezier curve.</p>";
    html += "<table class='segment-data'><thead><tr><th>Segment</th><th>Distance Samples (0-255)</th></tr></thead><tbody>";
    for (let i = 0; i < i32(segmentCount); i++) {
      const offset = i * 14;
      let dataStr = "";
      for (let j = 0; j < 14; j++) {
        dataStr += dataData[offset + j].toString();
        if (j < 13) dataStr += ", ";
      }
      html += "<tr><td>" + i.toString() + "</td><td>" + dataStr + "</td></tr>";
    }
    html += "</tbody></table>";
  } else if (dataData != null) {
    html += "<h3>Segment Distance Samples</h3>";
    html += "<p class='info'>" + segmentCount.toString() + " segments with distance sampling data (14 bytes each).</p>";
  }

  html += "</div>";

  return html;
}

export function render(): i32 {
  const data = Host.input();
  const html = renderSpline(data);
  Host.outputString(html);
  return 0;
}

/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";
import { readF32LE, readU32LE } from "../lib/binaryReader.ts";
import { NOT_FOUND, Ovl } from "../lib/ovl.ts";

export function name(): i32 {
  Host.outputString("Static Shape Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["shs"]');
  return 0;
}

function escapeHtml(value: string): string {
  return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;");
}

// StaticShapeMesh is 40 bytes (rct3-importer's staticshape.h): support_type (0), ftx_ref* (4,
// relocated ptr), txs_ref* (8, relocated ptr), transparency (12), texture_flags (16), sides (20),
// vertex_count (24), index_count (28), vertexes* (32, relocated ptr), indices* (36, relocated ptr).
function renderMeshRow(index: i32, meshAddr: i64): string {
  const meshBytes = Ovl.resolvePointer(meshAddr);
  if (meshBytes == null || meshBytes.length < 40) {
    return "<tr><td>" + index.toString() + "</td><td colspan='5' class='error'>Failed to resolve mesh data.</td></tr>";
  }

  const supportType = readU32LE(meshBytes, 0);
  const sides = readU32LE(meshBytes, 20);
  const vertexCount = readU32LE(meshBytes, 24);
  const indexCount = readU32LE(meshBytes, 28);

  // ftx_ref/txs_ref are assignSymbolReference-driven cross-resource references (ManagerSHS.cpp),
  // resolved via the symbol-reference table - NOT getRelocationSource, which only resolves pointers
  // to other data within the archive's own blocks.
  const ftxSymbol = Ovl.resolveSymbolReference(u32(meshAddr) + 4);
  const txsSymbol = Ovl.resolveSymbolReference(u32(meshAddr) + 8);

  let html = "<tr>";
  html += "<td>" + index.toString() + "</td>";
  html += "<td>" + vertexCount.toString() + "</td>";
  html += "<td>" + indexCount.toString() + "</td>";
  html += "<td>" + supportType.toString() + "</td>";
  html += "<td>" + sides.toString() + "</td>";
  html += "<td>" + (ftxSymbol != null ? escapeHtml(ftxSymbol.name) : "(none)") + "</td>";
  html += "<td>" + (txsSymbol != null ? escapeHtml(txsSymbol.name) : "(none)") + "</td>";
  html += "</tr>";
  return html;
}

// StaticShape is 56 bytes (rct3-importer's staticshape.h - confirmed against actual field sum,
// not the reference implementation's incidental "60 bytes" mislabel):
//   bounding_box_min (12), bounding_box_max (12), total_vertex_count (4), total_index_count (4),
//   mesh_count2 (4), mesh_count (4), sh* (4, relocated ptr), effect_count (4),
//   effect_positions* (4, relocated ptr), effect_names* (4, relocated ptr)
function renderStaticShape(data: Uint8Array): string {
  if (data.length < 56) {
    return "<p class='error'>Data too short to contain a StaticShape header (minimum 56 bytes required).</p>" +
      renderHexView(data);
  }

  let bboxMinX = readF32LE(data, 0);
  let bboxMinY = readF32LE(data, 4);
  let bboxMinZ = readF32LE(data, 8);
  let bboxMaxX = readF32LE(data, 12);
  let bboxMaxY = readF32LE(data, 16);
  let bboxMaxZ = readF32LE(data, 20);
  let totalVertexCount = readU32LE(data, 24);
  let totalIndexCount = readU32LE(data, 28);
  let meshCount = readU32LE(data, 36);
  let effectCount = readU32LE(data, 44);

  let html = "<div class='mesh-viewer'>";
  html += "<h3>Static Shape Metadata</h3>";
  html += "<table class='mesh-summary'><tbody>";

  html += "<tr><td colspan='2'><strong>Bounding Box Min</strong></td></tr>";
  html += "<tr><td>  X</td><td>" + bboxMinX.toString() + "</td></tr>";
  html += "<tr><td>  Y</td><td>" + bboxMinY.toString() + "</td></tr>";
  html += "<tr><td>  Z</td><td>" + bboxMinZ.toString() + "</td></tr>";

  html += "<tr><td colspan='2'><strong>Bounding Box Max</strong></td></tr>";
  html += "<tr><td>  X</td><td>" + bboxMaxX.toString() + "</td></tr>";
  html += "<tr><td>  Y</td><td>" + bboxMaxY.toString() + "</td></tr>";
  html += "<tr><td>  Z</td><td>" + bboxMaxZ.toString() + "</td></tr>";

  html += "<tr><td>Total Vertex Count (on-disk header)</td><td>" + totalVertexCount.toString() + "</td></tr>";
  html += "<tr><td>Total Index Count (on-disk header)</td><td>" + totalIndexCount.toString() + "</td></tr>";
  html += "<tr><td>Mesh Count</td><td>" + meshCount.toString() + "</td></tr>";
  html += "<tr><td>Effect Count</td><td>" + effectCount.toString() + "</td></tr>";
  html += "</tbody></table>";

  // Per-mesh breakdown, resolved live against the currently open archive via the "ovl" host
  // functions (see plugins/lib/ovl.ts) - not just the inline header above.
  const shapeAddr = Ovl.currentResourceAddress();
  if (shapeAddr == NOT_FOUND) {
    html += "<p class='error'>Could not determine this resource's own archive address - " +
      "per-mesh data unavailable.</p>";
  } else if (meshCount == 0) {
    html += "<p class='note'>No meshes.</p>";
  } else {
    const shArrayAddr = Ovl.getRelocationSource(u32(shapeAddr) + 40);
    if (shArrayAddr == NOT_FOUND) {
      html += "<p class='error'>Failed to resolve sh[] (mesh pointer array).</p>";
    } else {
      html += "<h3>Meshes (" + meshCount.toString() + ")</h3>";
      html += "<table class='mesh-summary'><thead><tr>" +
        "<th>#</th><th>Vertices</th><th>Indices</th><th>SupportType</th><th>Sides</th>" +
        "<th>FtxRef</th><th>TxsRef</th></tr></thead><tbody>";

      for (let i: u32 = 0; i < meshCount; i++) {
        const slotAddr = u32(shArrayAddr) + i * 4;
        const meshAddr = Ovl.getRelocationSource(slotAddr);
        html += meshAddr != NOT_FOUND
          ? renderMeshRow(i32(i), meshAddr)
          : "<tr><td>" + i.toString() + "</td><td colspan='6' class='error'>Failed to resolve mesh pointer.</td></tr>";
      }

      html += "</tbody></table>";
    }
  }

  html += "</div>";
  html += renderHexView(data, 0);

  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderStaticShape(data);
  Host.outputString(html);
  return 0;
}

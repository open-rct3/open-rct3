import { Host } from "@extism/as-pdk";
import { readF32LE, readU32LE } from "../lib/binaryReader.ts";
import { renderHexView } from "../lib/hexViewer.ts";
import { NOT_FOUND, Ovl } from "../lib/ovl.ts";
import { renderSplineProjections, SplineNode, TrackSplineInfo } from "../lib/svg.ts";
import "../types.ts";

export function name(): i32 {
  Host.outputString("Track Section Viewer");
  return 0;
}

export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}

export function file_types(): i32 {
  Host.outputString('["tks"]');
  return 0;
}

function escapeHtml(value: string): string {
  return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;");
}

function formatF32(val: f32): string {
  const rounded = Math.round(f64(val) * 100.0) / 100.0;
  return rounded.toString();
}

function formatHex32(val: u32): string {
  let h = val.toString(16);
  while (h.length < 8) {
    h = "0" + h;
  }
  return "0x" + h;
}

function formatBin32(val: u32): string {
  let s = "";
  for (let i: i32 = 31; i >= 0; i--) {
    s += ((val >> i) & 1) != 0 ? "1" : "0";
    if (i > 0 && i % 8 == 0) s += " ";
  }
  return s;
}

function slopeName(slope: u32): string {
  if (slope == 0) return "Flat (0)";
  if (slope == 1) return "Gentle Up (1)";
  if (slope == 2) return "Medium Up (2)";
  if (slope == 3) return "Steep Up (3)";
  if (slope == 4) return "Vertical Up (4)";
  if (slope == 5) return "Vertical Down (5)";
  if (slope == 6) return "Steep Down (6)";
  if (slope == 7) return "Medium Down (7)";
  if (slope == 8) return "Gentle Down (8)";
  return "Slope " + slope.toString();
}

function bankName(bank: u32): string {
  if (bank == 0) return "Flat (0)";
  if (bank == 1) return "Left (1)";
  if (bank == 2) return "Medium Left (2)";
  if (bank == 3) return "Inverted Left (3)";
  if (bank == 4) return "Inverted (4)";
  if (bank == 5) return "Right (5)";
  if (bank == 6) return "Medium Right (6)";
  if (bank == 7) return "Bank Right (7)";
  return "Bank " + bank.toString();
}

function directionName(dir: u32): string {
  if (dir == 0) return "Straight (0)";
  if (dir == 1) return "Left (1)";
  if (dir == 2) return "Right (2)";
  return "Direction " + dir.toString();
}

function resolveSplineResource(role: string, ptr: u32, color: string, cssClass: string): TrackSplineInfo {
  if (ptr == 0) {
    return new TrackSplineInfo(role, 0, "", color, cssClass, false, 0, false, 0.0, 0.0, 0.0, new Array<SplineNode>());
  }

  let symbolName = "";
  let splineBytes: Uint8Array | null = null;

  // 1. Try finding symbol by pointer
  const sym = Ovl.findSymbol(i64(ptr));
  if (sym != null) {
    symbolName = sym.name;
    // 2. Try reading resource by name & tag
    splineBytes = Ovl.readResource(sym.name, sym.tag.length > 0 ? sym.tag : "spl");
  }

  // 3. Fallback: resolve relocated pointer directly
  if (splineBytes == null) {
    splineBytes = Ovl.resolvePointer(i64(ptr));
  }

  if (splineBytes == null || splineBytes.length < 32) {
    return new TrackSplineInfo(role, ptr, symbolName, color, cssClass, false, 0, false, 0.0, 0.0, 0.0, new Array<SplineNode>());
  }

  // Spline Binary Header (32 bytes):
  // nodeCount (0), nodesPtr (4), cyclic (8), totalLength (12), invTotalLength (16), lengthsPtr (20), dataPtr (24), maxY (28)
  const nodeCount = readU32LE(splineBytes, 0);
  const nodesPtr = readU32LE(splineBytes, 4);
  const cyclic = readU32LE(splineBytes, 8) != 0;
  const totalLength = readF32LE(splineBytes, 12);
  const invTotalLength = readF32LE(splineBytes, 16);
  const maxY = readF32LE(splineBytes, 28);

  const nodes = new Array<SplineNode>();

  if (nodeCount > 0 && nodeCount < 10000) {
    let nodesData: Uint8Array | null = null;
    if (nodesPtr != 0) {
      nodesData = Ovl.resolvePointer(i64(nodesPtr));
    }
    // Fallback if nodes are packed sequentially after header (e.g. self-contained or fixture)
    if (nodesData == null && splineBytes.length >= 32 + i32(nodeCount) * 36) {
      nodesData = splineBytes.slice(32);
    }

    if (nodesData != null && nodesData.length >= i32(nodeCount) * 36) {
      for (let i: u32 = 0; i < nodeCount; i++) {
        const offset = i32(i) * 36;
        const px = readF32LE(nodesData, offset + 0);
        const py = readF32LE(nodesData, offset + 4);
        const pz = readF32LE(nodesData, offset + 8);
        const c1x = readF32LE(nodesData, offset + 12);
        const c1y = readF32LE(nodesData, offset + 16);
        const c1z = readF32LE(nodesData, offset + 20);
        const c2x = readF32LE(nodesData, offset + 24);
        const c2y = readF32LE(nodesData, offset + 28);
        const c2z = readF32LE(nodesData, offset + 32);
        nodes.push(new SplineNode(px, py, pz, c1x, c1y, c1z, c2x, c2y, c2z));
      }
    }
  }

  return new TrackSplineInfo(
    role,
    ptr,
    symbolName,
    color,
    cssClass,
    true,
    nodeCount,
    cyclic,
    totalLength,
    invTotalLength,
    maxY,
    nodes
  );
}

function renderTrackSection(data: Uint8Array): string {
  const HEADER_SIZE = 140;

  if (data.length < HEADER_SIZE) {
    return "<p class='error'>Data too short for TrackSection header (minimum " +
      HEADER_SIZE.toString() + " bytes required).</p>" +
      renderHexView(data);
  }

  // Parse TrackSectionBinary fields (matching OpenCobra.OVL.Files.TrackSectionBinary)
  const internalNamePtr = readU32LE(data, 0);
  const sceneryItemRef = readU32LE(data, 4);
  const entryCurve = readU32LE(data, 8);
  const exitCurve = readU32LE(data, 12);
  const specialCurves = readU32LE(data, 16);
  const direction = readU32LE(data, 20);
  const entryFlags = readU32LE(data, 24);
  const exitFlags = readU32LE(data, 28);

  const splineLeftRef = readU32LE(data, 32);
  const splineRightRef = readU32LE(data, 36);
  const joinSplineLeftRef = readU32LE(data, 40);
  const joinSplineRightRef = readU32LE(data, 44);
  const extraSplineLeftRef = readU32LE(data, 48);
  const extraSplineRightRef = readU32LE(data, 52);

  const entrySlope = readU32LE(data, 72);
  const entryBank = readU32LE(data, 76);
  const exitSlope = readU32LE(data, 100);
  const exitBank = readU32LE(data, 104);

  const speedCount = readU32LE(data, 112);
  const speedsPtr = readU32LE(data, 116);
  const towerRideBaseFlag = readU32LE(data, 120);
  const towerRideBase = readF32LE(data, 124);
  const waterSplash1 = readF32LE(data, 128);
  const waterSplash2 = readF32LE(data, 132);
  const reverserVal = readF32LE(data, 136);

  const entryDirection = direction & 0x3;
  const exitDirection = (direction >> 2) & 0x3;

  // Resolve internal name string
  let internalName: string = "";
  if (internalNamePtr != 0) {
    const resolvedName = Ovl.resolveString(i64(internalNamePtr));
    if (resolvedName != null) {
      internalName = resolvedName;
    }
  }

  // Resolve current TrackSection symbol name
  let sectionSymbolName: string = "";
  const currentAddr = Ovl.currentResourceAddress();
  if (currentAddr != NOT_FOUND) {
    const sym = Ovl.findSymbol(currentAddr);
    if (sym != null) {
      sectionSymbolName = sym.name;
    }
  }

  // Resolve scenery item reference
  let sceneryItemName: string = "";
  if (sceneryItemRef != 0) {
    const scenerySym = Ovl.findSymbol(i64(sceneryItemRef));
    if (scenerySym != null) {
      sceneryItemName = scenerySym.name;
    }
  }

  // Resolve all 6 spline references
  const splines = new Array<TrackSplineInfo>();
  splines.push(resolveSplineResource("Left Spline", splineLeftRef, "#2563eb", "spl-left"));
  splines.push(resolveSplineResource("Right Spline", splineRightRef, "#dc2626", "spl-right"));
  splines.push(resolveSplineResource("Join Left", joinSplineLeftRef, "#0891b2", "spl-join-left"));
  splines.push(resolveSplineResource("Join Right", joinSplineRightRef, "#d97706", "spl-join-right"));
  splines.push(resolveSplineResource("Extra Left", extraSplineLeftRef, "#7c3aed", "spl-extra-left"));
  splines.push(resolveSplineResource("Extra Right", extraSplineRightRef, "#059669", "spl-extra-right"));

  // Optional Soaked loop spline
  if (data.length >= 144) {
    const loopSplineRef = readU32LE(data, 140);
    if (loopSplineRef != 0) {
      splines.push(resolveSplineResource("Loop Spline", loopSplineRef, "#db2777", "spl-loop"));
    }
  }

  // Count resolved splines and determine 3D bounding box
  let minX: f32 = 1e9;
  let maxX: f32 = -1e9;
  let minY: f32 = 1e9;
  let maxY: f32 = -1e9;
  let minZ: f32 = 1e9;
  let maxZ: f32 = -1e9;
  let totalResolvedNodes: i32 = 0;
  let resolvedSplineCount: i32 = 0;

  for (let s = 0; s < splines.length; s++) {
    const spl = splines[s];
    if (spl.resolved) resolvedSplineCount++;
    for (let n = 0; n < spl.nodes.length; n++) {
      const node = spl.nodes[n];
      if (node.posX < minX) minX = node.posX;
      if (node.posX > maxX) maxX = node.posX;
      if (node.posY < minY) minY = node.posY;
      if (node.posY > maxY) maxY = node.posY;
      if (node.posZ < minZ) minZ = node.posZ;
      if (node.posZ > maxZ) maxZ = node.posZ;
      totalResolvedNodes++;
    }
  }

  // Build HTML output
  let html = "<div class='tks-viewer' style='font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,Helvetica,Arial,sans-serif; color:#1e293b; padding:12px;'>";

  // Section Header & Ownership Relationship Card
  html += "<div class='tks-header' style='background:#f8fafc; border:1px solid #e2e8f0; border-radius:8px; padding:12px 16px; margin-bottom:16px;'>";
  html += "<div style='display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:8px;'>";
  html += "<div>";
  html += "<span style='font-size:11px; text-transform:uppercase; color:#64748b; font-weight:700; letter-spacing:0.5px;'>Track Section</span>";
  const displayTitle = internalName.length > 0 ? escapeHtml(internalName) : (sectionSymbolName.length > 0 ? escapeHtml(sectionSymbolName) : "Unnamed Track Section");
  html += "<h2 style='margin:2px 0 0 0; color:#0f172a; font-size:18px;'>" + displayTitle + "</h2>";
  html += "</div>";

  // Visual badges linking TrackSection to its SceneryItem and Splines
  html += "<div style='display:flex; gap:6px; flex-wrap:wrap; align-items:center;'>";
  if (sceneryItemRef != 0) {
    const sceneryLabel = sceneryItemName.length > 0 ? sceneryItemName : formatHex32(sceneryItemRef);
    html += "<span style='font-size:11px; font-weight:600; padding:3px 8px; border-radius:4px; border:1px solid #0284c7; color:#0369a1; background:#f0f9ff;'>" +
      "SceneryItem: " + escapeHtml(sceneryLabel) + "</span>";
  }
  for (let s = 0; s < splines.length; s++) {
    const spl = splines[s];
    if (spl.rawPtr == 0) continue;
    const label = spl.symbolName.length > 0 ? spl.symbolName : "0x" + spl.rawPtr.toString(16);
    html += "<span style='font-size:11px; font-weight:600; padding:3px 8px; border-radius:4px; border:1px solid " +
      spl.color + "; color:" + spl.color + "; background:#ffffff;'>" +
      spl.role + ": " + escapeHtml(label) + "</span>";
  }
  html += "</div></div></div>";

  // Interactive Layer Visibility Toolbar
  html += "<div class='tks-toolbar' style='background:#f1f5f9; border:1px solid #cbd5e1; border-radius:6px; padding:10px 14px; margin-bottom:16px; display:flex; flex-wrap:wrap; gap:14px; align-items:center;'>";
  html += "<span style='font-size:12px; font-weight:700; color:#475569; text-transform:uppercase;'>Toggles:</span>";
  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-left' onchange=\"toggleLayer('spl-left', this.checked)\">";
  html += "<span style='display:inline-block; width:12px; height:12px; border-radius:2px; background:#2563eb;'></span> Left Spline</label>";

  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-right' onchange=\"toggleLayer('spl-right', this.checked)\">";
  html += "<span style='display:inline-block; width:12px; height:12px; border-radius:2px; background:#dc2626;'></span> Right Spline</label>";

  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-join' onchange=\"toggleLayer('spl-join-left', this.checked); toggleLayer('spl-join-right', this.checked);\">";
  html += "<span style='display:inline-block; width:12px; height:12px; border-radius:2px; background:#0891b2;'></span> Join Splines</label>";

  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-extra' onchange=\"toggleLayer('spl-extra-left', this.checked); toggleLayer('spl-extra-right', this.checked);\">";
  html += "<span style='display:inline-block; width:12px; height:12px; border-radius:2px; background:#7c3aed;'></span> Extra Splines</label>";

  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-handles' onchange=\"toggleLayer('cp-handle', this.checked)\"> Bezier Handles</label>";

  html += "<label style='font-size:13px; font-weight:500; cursor:pointer; display:inline-flex; align-items:center; gap:5px;'>";
  html += "<input type='checkbox' checked id='chk-meta' onchange=\"toggleId('tks-meta-table', this.checked)\"> Metadata Table</label>";
  html += "</div>";

  // 2D Spline Visualizations (Side-by-side)
  if (totalResolvedNodes >= 2) {
    html += renderSplineProjections(splines, minX, maxX, minY, maxY, minZ, maxZ);
  } else if (resolvedSplineCount > 0) {
    html += "<p class='note' style='background:#eff6ff; border:1px solid #bfdbfe; padding:10px; border-radius:6px; font-size:13px;'>";
    html += "Spline references resolved, but insufficient node geometry found to display 2D track curves.</p>";
  } else {
    html += "<p class='note' style='background:#fef3c7; border:1px solid #fde68a; padding:10px; border-radius:6px; font-size:13px; color:#92400e;'>";
    html += "<strong>Note:</strong> Referenced spline resources could not be resolved from the current archive. Spline reference addresses are shown in the summary tables below.</p>";
  }

  // TrackSection Metadata Summary Table
  html += "<div id='tks-meta-table'>";
  html += "<h3 style='font-size:15px; margin:16px 0 8px 0; color:#0f172a; border-bottom:2px solid #e2e8f0; padding-bottom:4px;'>TrackSection Summary</h3>";
  html += "<table style='width:100%; border-collapse:collapse; margin-bottom:16px; font-size:13px;'>";
  html += "<tbody>";

  // Identifiers & Object References
  html += "<tr style='background:#f8fafc;'><td colspan='2' style='padding:6px 10px; font-weight:700; border:1px solid #e2e8f0;'>Identification & References</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0; width:220px;'>Internal Name (internalNamePtr)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>" +
    (internalName.length > 0 ? escapeHtml(internalName) + " (Ptr: " + formatHex32(internalNamePtr) + ")" : formatHex32(internalNamePtr)) + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Scenery Item Ref (sceneryItemRef)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'><code style='background:#f1f5f9; padding:2px 4px; border-radius:3px; font-weight:600;'>" +
    formatHex32(sceneryItemRef) + "</code> (" + sceneryItemRef.toString() + ")" +
    (sceneryItemName.length > 0 ? " &mdash; <strong>" + escapeHtml(sceneryItemName) + "</strong>" : (sceneryItemRef != 0 ? " &mdash; <em>(Unresolved)</em>" : " &mdash; <em>(None)</em>")) +
    "</td></tr>";

  // Connection & Behavior Flags
  html += "<tr style='background:#f8fafc;'><td colspan='2' style='padding:6px 10px; font-weight:700; border:1px solid #e2e8f0;'>Connection & Behavior Flags</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Entry Flags (entryFlags)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'><code style='background:#f1f5f9; padding:2px 4px; border-radius:3px; font-weight:600;'>" +
    formatHex32(entryFlags) + "</code> (" + entryFlags.toString() + ") <span style='font-family:monospace; font-size:11px; color:#64748b; margin-left:8px;'>" + formatBin32(entryFlags) + "</span></td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Exit Flags (exitFlags)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'><code style='background:#f1f5f9; padding:2px 4px; border-radius:3px; font-weight:600;'>" +
    formatHex32(exitFlags) + "</code> (" + exitFlags.toString() + ") <span style='font-family:monospace; font-size:11px; color:#64748b; margin-left:8px;'>" + formatBin32(exitFlags) + "</span></td></tr>";

  // Track Geometry & Types
  html += "<tr style='background:#f8fafc;'><td colspan='2' style='padding:6px 10px; font-weight:700; border:1px solid #e2e8f0;'>Track Geometry & Curves</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Special Curves (Type)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + formatHex32(specialCurves) + " (" + specialCurves.toString() + ")</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Entry Curve / Exit Curve</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + entryCurve.toString() + " / " + exitCurve.toString() + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Direction Profile</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Entry: " + directionName(entryDirection) + " | Exit: " + directionName(exitDirection) + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Slope Profile (Height)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Entry: " + slopeName(entrySlope) + " | Exit: " + slopeName(exitSlope) + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Bank Profile</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Entry: " + bankName(entryBank) + " | Exit: " + bankName(exitBank) + "</td></tr>";

  // Height & Effects
  html += "<tr style='background:#f8fafc;'><td colspan='2' style='padding:6px 10px; font-weight:700; border:1px solid #e2e8f0;'>Height Modifiers & Special Ride Effects</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Tower Ride Base (Height)</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + towerRideBase.toString() + " (Flag: " + towerRideBaseFlag.toString() + ")</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Water Splash Modifiers</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>1: " + waterSplash1.toString() + " | 2: " + waterSplash2.toString() + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Reverser Value</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + reverserVal.toString() + "</td></tr>";
  html += "<tr><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Speed Modifiers</td><td style='padding:6px 10px; border:1px solid #e2e8f0;'>Count: " + speedCount.toString() + " (Ptr: " + formatHex32(speedsPtr) + ")</td></tr>";

  html += "</tbody></table></div>";

  // Referenced Splines Table
  html += "<h3 style='font-size:15px; margin:16px 0 8px 0; color:#0f172a; border-bottom:2px solid #e2e8f0; padding-bottom:4px;'>Referenced Splines</h3>";
  html += "<table style='width:100%; border-collapse:collapse; margin-bottom:16px; font-size:13px;'>";
  html += "<thead><tr style='background:#f1f5f9;'>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Role</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Pointer</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Symbol Name (Spline ID)</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Nodes</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Length</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Cyclic</th>";
  html += "<th style='padding:6px 10px; border:1px solid #e2e8f0;'>Status</th>";
  html += "</tr></thead><tbody>";

  for (let s = 0; s < splines.length; s++) {
    const spl = splines[s];
    html += "<tr>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'><span style='display:inline-block; width:10px; height:10px; border-radius:2px; background:" +
      spl.color + "; margin-right:6px;'></span>" + spl.role + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0; font-family:monospace;'>" + formatHex32(spl.rawPtr) + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + (spl.symbolName.length > 0 ? escapeHtml(spl.symbolName) : "(Unresolved)") + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + (spl.resolved ? spl.nodeCount.toString() : "-") + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + (spl.resolved ? formatF32(spl.totalLength) : "-") + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'>" + (spl.resolved ? (spl.cyclic ? "Yes" : "No") : "-") + "</td>";
    html += "<td style='padding:6px 10px; border:1px solid #e2e8f0;'>" +
      (spl.rawPtr == 0 ? "<span style='color:#94a3b8;'>Empty</span>" :
        (spl.resolved ? "<span style='color:#16a34a; font-weight:600;'>Resolved</span>" :
          "<span style='color:#dc2626;'>Unresolved</span>")) + "</td>";
    html += "</tr>";
  }
  html += "</tbody></table>";

  // Hex dump
  html += renderHexView(data);

  // Embedded client script for interactive toggles
  html += "<script>";
  html += "function toggleLayer(cls, show) {";
  html += "  var elements = document.getElementsByClassName(cls);";
  html += "  for (var i = 0; i < elements.length; i++) {";
  html += "    elements[i].style.display = show ? '' : 'none';";
  html += "  }";
  html += "}";
  html += "function toggleId(id, show) {";
  html += "  var el = document.getElementById(id);";
  html += "  if (el) {";
  html += "    el.style.display = show ? '' : 'none';";
  html += "  }";
  html += "}";
  html += "</script>";

  html += "</div>";
  return html;
}

export function render(): i32 {
  const data = Host.input();
  const html = renderTrackSection(data);
  Host.outputString(html);
  return 0;
}

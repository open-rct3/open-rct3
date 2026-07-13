/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";
import { readF32LE, readI32LE, readU16LE, readU32LE } from "../lib/binaryReader.ts";
import { NOT_FOUND, Ovl, OvlSymbol } from "../lib/ovl.ts";

export function name(): i32 {
  Host.outputString("Scenery Item Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["sid"]');
  return 0;
}

function escapeHtml(value: string): string {
  return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;");
}

// Placement enum (OpenCobra.OVL.Placement / SIZE_* defines) - see cSidPosition::positioningtype.
function placementName(value: u16): string {
  if (value == 0) return "FullTile";
  if (value == 1) return "PathEdgeInner";
  if (value == 2) return "PathEdgeOuter";
  if (value == 3) return "Wall";
  if (value == 4) return "Quarter";
  if (value == 5) return "Half";
  if (value == 6) return "PathCenter";
  if (value == 7) return "Corner";
  if (value == 8) return "PathEdgeJoin";
  return "Unknown (" + value.toString() + ")";
}

// Fetches a "txt" symbol's own decoded UTF-16LE null-terminated text content (ManagerTXT.cpp - a
// bare wide string with no header struct), given its resolved OvlSymbol.
function readTxtContent(symbol: OvlSymbol | null): string | null {
  if (symbol == null) return null;
  const bytes = Ovl.readResource(symbol.name, symbol.tag);
  if (bytes == null) return null;
  let end: i32 = 0;
  while (end + 1 < bytes.length && (bytes[end] != 0 || bytes[end + 1] != 0)) end += 2;
  return String.UTF16.decode(bytes.slice(0, end).buffer);
}

// Renders the Placement/footprint grid as SVG - occupied tiles shaded, anchor tile marked, and a
// short annotation for edge/quarter/wall placements per the plan's Dumper Plugin layout.
function renderPlacementDiagram(placement: u16, squaresX: u32, squaresZ: u32): string {
  const cell = 40;
  const cols: u32 = placement == 0 ? max(squaresX, 1) : 1;
  const rows: u32 = placement == 0 ? max(squaresZ, 1) : 1;
  const width = cols * cell + 20;
  const height = rows * cell + 20;

  let svg = "<svg width='" + width.toString() + "' height='" + height.toString() +
    "' viewBox='0 0 " + width.toString() + " " + height.toString() + "' xmlns='http://www.w3.org/2000/svg'>";

  for (let z: u32 = 0; z < rows; z++) {
    for (let x: u32 = 0; x < cols; x++) {
      const px = 10 + x * cell;
      const py = 10 + z * cell;
      svg += "<rect x='" + px.toString() + "' y='" + py.toString() + "' width='" + cell.toString() +
        "' height='" + cell.toString() + "' fill='#cfe8ff' stroke='#6699cc'/>";
    }
  }

  // Anchor marker at the origin tile.
  svg += "<circle cx='" + (10 + cell / 2).toString() + "' cy='" + (10 + cell / 2).toString() +
    "' r='4' fill='#cc3333'/>";

  // Sub-tile annotation for non-FullTile placements.
  if (placement == 4) { // Quarter
    svg += "<rect x='10' y='10' width='" + (cell / 2).toString() + "' height='" + (cell / 2).toString() +
      "' fill='#ffcc66' stroke='#996600'/>";
  } else if (placement == 5) { // Half
    svg += "<rect x='10' y='10' width='" + cell.toString() + "' height='" + (cell / 2).toString() +
      "' fill='#ffcc66' stroke='#996600'/>";
  } else if (placement == 1 || placement == 2 || placement == 8) { // PathEdgeInner/Outer/Join
    svg += "<line x1='10' y1='10' x2='" + (10 + cell).toString() + "' y2='10' stroke='#cc3333' stroke-width='4'/>";
  } else if (placement == 3) { // Wall
    svg += "<line x1='" + (10 + cell).toString() + "' y1='10' x2='" + (10 + cell).toString() +
      "' y2='" + (10 + cell).toString() + "' stroke='#663300' stroke-width='4'/>";
  } else if (placement == 7) { // Corner
    svg += "<circle cx='10' cy='10' r='6' fill='#ffcc66' stroke='#996600'/>";
  } else if (placement == 6) { // PathCenter
    svg += "<circle cx='" + (10 + cell / 2).toString() + "' cy='" + (10 + cell / 2).toString() +
      "' r='6' fill='#66cc99' stroke='#227755'/>";
  }

  svg += "</svg>";
  return "<div class='placement-diagram'>" + svg + "<p>" + placementName(placement) +
    " (" + squaresX.toString() + "x" + squaresZ.toString() + " tiles)</p></div>";
}

class LodSummary {
  constructor(
    public name: string,
    public meshType: u32,
    public distance: f32,
    public staticShapeRef: string | null,
    public boneShapeRef: string | null,
    public ftsRef: string | null,
    public txsRef: string | null,
  ) {}
}

function meshTypeName(value: u32): string {
  if (value == 0) return "StaticShape";
  if (value == 3) return "BoneShape";
  if (value == 4) return "Billboard";
  return "Unknown (" + value.toString() + ")";
}

// SceneryItemVisualLOD is 72 bytes: type(0), lod_name*(4), shs_ref*(8), unk2(12), bsh_ref*(16),
// unk4(20), ftx_ref*(24), txs_ref*(28), unk7-12(32-55), distance(56), animation_count(60).
function readLod(lodAddress: i64): LodSummary | null {
  const bytes = Ovl.resolvePointer(lodAddress);
  if (bytes == null || bytes.length < 72) return null;

  const meshType = readU32LE(bytes, 0);
  const nameAddr = Ovl.getRelocationSource(u32(lodAddress) + 4);
  const lodName = nameAddr != NOT_FOUND ? Ovl.resolveString(nameAddr) : null;
  const staticShapeRef = Ovl.resolveSymbolReference(u32(lodAddress) + 8);
  const boneShapeRef = Ovl.resolveSymbolReference(u32(lodAddress) + 16);
  const ftsRef = Ovl.resolveSymbolReference(u32(lodAddress) + 24);
  const txsRef = Ovl.resolveSymbolReference(u32(lodAddress) + 28);
  const distance = readF32LE(bytes, 56);

  return new LodSummary(
    lodName != null ? lodName : "",
    meshType,
    distance,
    staticShapeRef != null ? staticShapeRef.name : null,
    boneShapeRef != null ? boneShapeRef.name : null,
    ftsRef != null ? ftsRef.name : null,
    txsRef != null ? txsRef.name : null,
  );
}

// SceneryItemVisual_V is 52 bytes: sivflags(0), sway(4), brightness(8), unk4(12), scale(16),
// lod_count(20), lods*(24).
function renderSvdSection(svdName: string): string {
  const svdAddress = Ovl.symbolAddress(svdName, "svd");
  if (svdAddress == NOT_FOUND) {
    return "<tr><td colspan='6' class='error'>Could not resolve SVD '" + escapeHtml(svdName) + "'.</td></tr>";
  }

  const bytes = Ovl.resolvePointer(svdAddress);
  if (bytes == null || bytes.length < 52) {
    return "<tr><td colspan='6' class='error'>Failed to read SVD '" + escapeHtml(svdName) + "'.</td></tr>";
  }

  const lodCount = readU32LE(bytes, 20);
  if (lodCount == 0) return "<tr><td colspan='6' class='note'>" + escapeHtml(svdName) + ": no LODs.</td></tr>";

  const lodArrayAddr = Ovl.getRelocationSource(u32(svdAddress) + 24);
  if (lodArrayAddr == NOT_FOUND) {
    return "<tr><td colspan='6' class='error'>" + escapeHtml(svdName) + ": failed to resolve lods[].</td></tr>";
  }

  let html = "";
  for (let i: u32 = 0; i < lodCount; i++) {
    const slotAddr = u32(lodArrayAddr) + i * 4;
    const lodAddr = Ovl.getRelocationSource(slotAddr);
    if (lodAddr == NOT_FOUND) {
      html += "<tr><td>" + escapeHtml(svdName) + "</td><td colspan='5' class='error'>Failed to resolve LOD " +
        i.toString() + ".</td></tr>";
      continue;
    }
    const lod = readLod(lodAddr);
    if (lod == null) {
      html += "<tr><td>" + escapeHtml(svdName) + "</td><td colspan='5' class='error'>Failed to read LOD " +
        i.toString() + ".</td></tr>";
      continue;
    }
    html += "<tr>";
    html += "<td>" + escapeHtml(svdName) + "</td>";
    html += "<td>" + escapeHtml(lod.name) + "</td>";
    html += "<td>" + meshTypeName(lod.meshType) + "</td>";
    html += "<td>" + lod.distance.toString() + "</td>";
    // shs/fts/txs are decoded refs (resolved symbol names); bsh/ban stay undecoded per the plan's
    // Dependencies section (no BSH/BAN decoder yet).
    const refs: string[] = [];
    if (lod.staticShapeRef != null) refs.push("shs: " + escapeHtml(lod.staticShapeRef!));
    if (lod.boneShapeRef != null) refs.push("bsh: " + escapeHtml(lod.boneShapeRef!) + " (undecoded — BSH)");
    if (lod.ftsRef != null) refs.push("fts: " + escapeHtml(lod.ftsRef!));
    if (lod.txsRef != null) refs.push("txs: " + escapeHtml(lod.txsRef!));
    html += "<td>" + (refs.length > 0 ? refs.join("<br/>") : "(none)") + "</td>";
    html += "</tr>";
  }
  return html;
}

function renderMetadataRow(label: string, value: string): string {
  return "<tr><td>" + label + "</td><td>" + value + "</td></tr>";
}

function toHexColor(argb: u32): string {
  const r = (argb >> 16) & 0xFF;
  const g = (argb >> 8) & 0xFF;
  const b = argb & 0xFF;
  return "#" + r.toString(16).padStart(2, "0") + g.toString(16).padStart(2, "0") + b.toString(16).padStart(2, "0");
}

function renderColorSwatch(label: string, argb: u32): string {
  const hex = toHexColor(argb);
  return "<tr><td>" + label + "</td><td><div style='display:inline-block;width:20px;height:20px;background:" +
    hex + ";border:1px solid #ccc;'></div> " + hex + "</td></tr>";
}

// SceneryItem_V is 212 bytes (see sceneryrevised.h / OpenCobra.OVL.Files.SceneryItems.cs, which
// this mirrors): position_type(8,u16), structure_version(10,u16), squares_x(16), squares_z(20),
// position(28-39), size(40-51), cost(60,i32), removal_cost(64,i32), type(72), svd_count(80),
// svds_ref*(84), icon_ref*(88), group_icon_ref*(92), group_name_ref*(96), param_count(104),
// sound_count(112), name_ref*(120), default_col1-3(140-151), anr_count(184).
function renderSceneryItem(data: Uint8Array): string {
  if (data.length < 212) {
    return "<p class='error'>Data too short to contain a SceneryItem_V struct (minimum 212 bytes required).</p>" +
      renderHexView(data);
  }

  const positionType = readU16LE(data, 8);
  const structureVersion = readU16LE(data, 10);
  const squaresX = readU32LE(data, 16);
  const squaresZ = readU32LE(data, 20);
  const cost = readI32LE(data, 60);
  const removalCost = readI32LE(data, 64);
  const sceneryType = readU32LE(data, 72);
  const svdCount = readU32LE(data, 80);
  const primaryColor = readU32LE(data, 140);
  const secondaryColor = readU32LE(data, 144);
  const tertiaryColor = readU32LE(data, 148);

  let addonPack: u32 = 0;
  let genericAddon: u32 = 0;
  if (structureVersion >= 1 && data.length >= 228) {
    addonPack = readU32LE(data, 220);
    genericAddon = readU32LE(data, 224);
  }

  const sidAddress = Ovl.currentResourceAddress();
  let nameText: string | null = null;
  let iconName: string | null = null;
  let groupText: string | null = null;
  let groupIconName: string | null = null;
  let svdNames: string[] = [];

  if (sidAddress != NOT_FOUND) {
    nameText = readTxtContent(Ovl.resolveSymbolReference(u32(sidAddress) + 120));
    const icon = Ovl.resolveSymbolReference(u32(sidAddress) + 88);
    if (icon != null) iconName = icon.name;
    groupText = readTxtContent(Ovl.resolveSymbolReference(u32(sidAddress) + 96));
    const groupIcon = Ovl.resolveSymbolReference(u32(sidAddress) + 92);
    if (groupIcon != null) groupIconName = groupIcon.name;

    if (svdCount > 0) {
      const svdArrayAddr = Ovl.getRelocationSource(u32(sidAddress) + 84);
      if (svdArrayAddr != NOT_FOUND) {
        for (let i: u32 = 0; i < svdCount; i++) {
          const symbol = Ovl.resolveSymbolReference(u32(svdArrayAddr) + i * 4);
          if (symbol != null) svdNames.push(symbol.name);
        }
      }
    }
  }

  let html = "<div class='sid-viewer'>";

  html += "<h3>Scenery Item Metadata</h3>";
  html += "<table class='sid-summary'><tbody>";
  html += renderMetadataRow("Name", nameText != null ? escapeHtml(nameText!) : "<em>(not resolvable — likely in a shared pack catalog)</em>");
  html += renderMetadataRow("Icon", iconName != null ? escapeHtml(iconName!) : "<em>(none)</em>");
  html += renderMetadataRow("Group", groupText != null ? escapeHtml(groupText!) : "<em>(none)</em>");
  html += renderMetadataRow("Group Icon", groupIconName != null ? escapeHtml(groupIconName!) : "<em>(none)</em>");
  html += renderMetadataRow("Scenery Type", sceneryType.toString());
  html += renderMetadataRow("Cost", cost.toString());
  html += renderMetadataRow("Removal Cost", removalCost.toString());
  html += renderMetadataRow("Structure Version", structureVersion.toString());
  html += renderMetadataRow("Addon Pack", addonPack == 0 ? "Vanilla" : addonPack == 1 ? "Soaked" : addonPack == 2 ? "Wild" : addonPack.toString());
  html += renderMetadataRow("Generic Addon", genericAddon.toString());
  html += renderColorSwatch("Primary Color", primaryColor);
  html += renderColorSwatch("Secondary Color", secondaryColor);
  html += renderColorSwatch("Tertiary Color", tertiaryColor);
  html += "</tbody></table>";

  html += "<h3>Placement</h3>";
  html += renderPlacementDiagram(positionType, squaresX, squaresZ);

  html += "<h3>Resolved Visuals (SVD LODs)</h3>";
  if (svdNames.length == 0) {
    html += "<p class='note'>No resolvable SvdRefs (either this item has none, or its SVDs live in " +
      "an OVL file not currently loaded).</p>";
  } else {
    html += "<table class='lod-summary'><thead><tr>" +
      "<th>SVD</th><th>LOD</th><th>Mesh Type</th><th>Distance</th><th>Refs</th></tr></thead><tbody>";
    for (let i = 0; i < svdNames.length; i++) html += renderSvdSection(svdNames[i]);
    html += "</tbody></table>";
  }

  html += "</div>";
  html += renderHexView(data, 0);

  return html;
}

export function render(): i32 {
  const data = Host.input();
  const html = renderSceneryItem(data);
  Host.outputString(html);
  return 0;
}

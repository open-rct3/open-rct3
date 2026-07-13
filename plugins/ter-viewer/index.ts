/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";
import { readF32LE, readU32LE } from "../lib/binaryReader.ts";
import { NOT_FOUND, Ovl } from "../lib/ovl.ts";

export function name(): i32 {
  Host.outputString("Terrain Type Viewer");
  return 0;
}

export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}

export function file_types(): i32 {
  Host.outputString('["ter"]');
  return 0;
}

function escapeHtml(value: string): string {
  return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;");
}

function toHexColor(argb: u32): string {
  // ARGB format: 0xAARRGGBB, convert to #RRGGBB
  let r = (argb >> 16) & 0xFF;
  let g = (argb >> 8) & 0xFF;
  let b = argb & 0xFF;
  let hex = "#" + r.toString(16).padStart(2, "0") + g.toString(16).padStart(2, "0") + b.toString(16).padStart(2, "0");
  return hex.toUpperCase();
}

function terrainTypeToString(type: u32): string {
  if (type == 0) return "Ground Unblended";
  if (type == 1) return "Cliff";
  if (type == 2) return "Ground Blended";
  return "Unknown (" + type.toString() + ")";
}

function renderTerrainType(data: Uint8Array): string {
  if (data.length < 60) {
    return "<p class='error'>Data too short to contain a TerrainType struct (minimum 60 bytes required).</p>" +
      renderHexView(data);
  }

  let version = readU32LE(data, 0);
  let unk02 = readU32LE(data, 4);
  let addon = readU32LE(data, 8);
  let number = readU32LE(data, 12);
  let type = readU32LE(data, 16);
  let textureRefPtr = readU32LE(data, 20);
  let descriptionNamePtr = readU32LE(data, 24);
  let iconNamePtr = readU32LE(data, 28);
  let colourSimple = readU32LE(data, 32);
  let colourMap = readU32LE(data, 36);
  let invWidth = readF32LE(data, 40);
  let invHeight = readF32LE(data, 44);
  let unk13 = readF32LE(data, 48);
  let unk14 = readF32LE(data, 52);
  let unk15 = readF32LE(data, 56);

  // Resolve symbol references
  let textureRefName: string = "(none)";
  let descriptionName: string = "(none)";
  let iconName: string = "(none)";

  let resourceAddr = Ovl.currentResourceAddress();
  if (resourceAddr != NOT_FOUND) {
    if (textureRefPtr != 0) {
      let textureAddr = Ovl.getRelocationSource(u32(resourceAddr) + 20);
      if (textureAddr != NOT_FOUND) {
        let symbol = Ovl.findSymbol(textureAddr);
        if (symbol != null) textureRefName = escapeHtml(symbol.name);
      }
    }
    if (descriptionNamePtr != 0) {
      let descAddr = Ovl.getRelocationSource(u32(resourceAddr) + 24);
      if (descAddr != NOT_FOUND) {
        let symbol = Ovl.findSymbol(descAddr);
        if (symbol != null) descriptionName = escapeHtml(symbol.name);
      }
    }
    if (iconNamePtr != 0) {
      let iconAddr = Ovl.getRelocationSource(u32(resourceAddr) + 28);
      if (iconAddr != NOT_FOUND) {
        let symbol = Ovl.findSymbol(iconAddr);
        if (symbol != null) iconName = escapeHtml(symbol.name);
      }
    }
  }

  let html = "<div class='terrain-viewer'>";
  html += "<h3>Terrain Type Metadata</h3>";
  html += "<table class='terrain-summary'><tbody>";

  html += "<tr><td>Version</td><td>" + version.toString() + "</td></tr>";
  html += "<tr><td>Addon</td><td>" + addon.toString() + "</td></tr>";
  html += "<tr><td>Number</td><td>" + number.toString() + "</td></tr>";
  html += "<tr><td>Type</td><td>" + terrainTypeToString(type) + "</td></tr>";

  html += "<tr><td colspan='2'><strong>Colors</strong></td></tr>";
  let simpleHex = toHexColor(colourSimple);
  let mapHex = toHexColor(colourMap);
  html += "<tr><td>Colour (Simple)</td><td><div style='display:inline-block;width:20px;height:20px;background:" +
    simpleHex + ";border:1px solid #ccc;'></div> " + simpleHex + " (0x" + colourSimple.toString(16).padStart(8, "0").toUpperCase() + ")</td></tr>";
  html += "<tr><td>Colour (Map)</td><td><div style='display:inline-block;width:20px;height:20px;background:" +
    mapHex + ";border:1px solid #ccc;'></div> " + mapHex + " (0x" + colourMap.toString(16).padStart(8, "0").toUpperCase() + ")</td></tr>";

  html += "<tr><td colspan='2'><strong>Texture Mapping</strong></td></tr>";
  html += "<tr><td>InvWidth</td><td>" + invWidth.toString() + "</td></tr>";
  html += "<tr><td>InvHeight</td><td>" + invHeight.toString() + "</td></tr>";

  html += "<tr><td colspan='2'><strong>References</strong></td></tr>";
  html += "<tr><td>Texture</td><td>" + textureRefName + "</td></tr>";
  html += "<tr><td>Description</td><td>" + descriptionName + "</td></tr>";
  html += "<tr><td>Icon</td><td>" + iconName + "</td></tr>";

  html += "<tr><td colspan='2'><strong>Unknown Fields</strong></td></tr>";
  html += "<tr><td>Unk02</td><td>" + unk02.toString() + "</td></tr>";
  html += "<tr><td>Unk13</td><td>" + unk13.toString() + "</td></tr>";
  html += "<tr><td>Unk14</td><td>" + unk14.toString() + "</td></tr>";
  html += "<tr><td>Unk15</td><td>" + unk15.toString() + "</td></tr>";

  html += "</tbody></table>";
  html += "</div>";

  html += renderHexView(data, 0);

  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderTerrainType(data);
  Host.outputString(html);
  return 0;
}

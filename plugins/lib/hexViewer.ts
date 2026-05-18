import { Host } from "@extism/as-pdk";

function toHex(value: u32, width: i32 = 8): string {
  let hex = value.toString(16).toUpperCase();
  while (hex.length < width) {
    hex = "0" + hex;
  }
  return "0x" + hex;
}

export function renderHexView(data: Uint8Array, startOffset: i32 = 0): string {
  let html = "<h3>Hex View</h3>";
  html += "<div class='hex-view' style='font-family: monospace; font-size: 12px;'><table><thead><tr><th>Offset</th>";
  for (let h = 0; h < 16; h++) {
    html += "<th>" + h.toString(16).toUpperCase().padStart(2, "0") + "</th>";
  }
  html += "<th>ASCII</th></tr></thead><tbody>";

  let rowCount = (data.length + 15) / 16;
  if (rowCount > 1024) rowCount = 1024; // Limit for performance

  for (let r = 0; r < rowCount; r++) {
    let offset = r * 16;
    html += "<tr><td>" + toHex(startOffset + offset, 4) + "</td>";

    let ascii = "";
    for (let i = 0; i < 16; i++) {
      let idx = offset + i;
      if (idx < data.length) {
        let byte = data[idx];
        html += "<td>" + byte.toString(16).toUpperCase().padStart(2, "0") + "</td>";
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
    html += "<tr><td colspan='18' class='truncated' style='text-align: center; color: #666;'>... data truncated (" +
      data.length.toString() + " bytes total)</td></tr>";
  }

  html += "</tbody></table></div>";
  return html;
}

/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Config, Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";

export function name(): i32 {
  Host.outputString("Integer Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["int"]');
  return 0;
}

function toHex(value: u32, width: i32 = 8): string {
  let hex = value.toString(16).toUpperCase();
  let padding = "";
  for (let j = 0; j < width - hex.length; j++) {
    padding += "0";
  }
  return "0x" + padding + hex;
}

function toBinary(value: u32): string {
  let bin = value.toString(2);
  let padding = "";
  for (let j = 0; j < 32 - bin.length; j++) {
    padding += "0";
  }
  return padding + bin;
}

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24));
}

function renderIntTable(data: Uint8Array, signed: bool): string {
  if (data.length == 0) {
    return "<p class='empty'>No data to display.</p><p class='hint'>Could not decipher data. If this was unexpected, you may have found a bug. See the <a href='https://github.com/open-rct3/open-rct3/issues?q=state%3Aopen%20label%3Aplugin' target='_blank'>open plugin issues</a> or <a href='https://github.com/open-rct3/open-rct3/issues/new' target='_blank'>report a new issue</a>.</p>";
  }

  let count = data.length / 4;
  if (count == 0 || data.length % 4 != 0) {
    return "<p class='error'>Data length is not a multiple of 4 bytes. Showing hex view instead.</p>" +
      renderHexView(data);
  }

  let html =
    "<div class='int-table-wrapper'><table class='int-table'><thead><tr><th>#</th><th>Decimal</th><th>Hex</th><th>Binary</th></tr></thead><tbody>";

  for (let i = 0; i < count; i++) {
    let offset = i * 4;
    let value = readU32LE(data, offset);
    let signedVal: i32 = 0;
    if (value >> 31 != 0) {
      signedVal = -1 - i32(u32.MAX_VALUE - value);
    } else {
      signedVal = i32(value);
    }
    let displayVal = signed ? signedVal.toString() : value.toString();

    html += "<tr><td>" + i.toString() + "</td><td>" + displayVal + "</td><td>" + toHex(value) +
      "</td><td class='binary'>" + toBinary(value) + "</td></tr>";
  }

  html += "</tbody></table></div>";
  return html;
}

export function render(): i32 {
  let data = Host.input();
  let signed = Config.get("signed") == "true";

  let html = renderIntTable(data, signed);
  html += renderHexView(data);

  Host.outputString(html);
  return 0;
}

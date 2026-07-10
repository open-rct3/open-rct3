/// <reference no-default-lib="true" />
/// <reference types="assemblyscript/types" />
import { Host } from "@extism/as-pdk";
import { renderHexView } from "../lib/hexViewer.ts";

export function name(): i32 {
  Host.outputString("Text Viewer");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.1.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["txt"]');
  return 0;
}

function decodeText(data: Uint8Array, encoding: string): string {
  let result = "";
  let offset = 0;

  if (encoding == "utf-16le") {
    while (offset < data.length - 1) {
      let char = u32(data[offset]) | (u32(data[offset + 1]) << 8);
      if (char == 0) break;
      // Note: AssemblyScript's String.fromCharCode is not variadic (fixed overloads, max 2 args),
      // unlike JS/TS — so multi-byte chars must be appended one code unit at a time.
      if (char < 0x80) {
        result += String.fromCharCode(char);
      } else if (char < 0x800) {
        result += String.fromCharCode(
          0xC0 | (char >> 6),
          0x80 | (char & 0x3F),
        );
      } else {
        result += String.fromCharCode(0xE0 | (char >> 12));
        result += String.fromCharCode(0x80 | ((char >> 6) & 0x3F));
        result += String.fromCharCode(0x80 | (char & 0x3F));
      }
      offset += 2;
    }
  } else {
    while (offset < data.length) {
      let byte = data[offset];
      if (byte == 0) break;
      result += String.fromCharCode(byte);
      offset++;
    }
  }

  return result;
}

function escapeHtml(text: string): string {
  let escaped = "";
  for (let i = 0; i < text.length; i++) {
    let ch = text.charCodeAt(i);
    if (ch == 38) escaped += "&amp;";
    else if (ch == 60) escaped += "&lt;";
    else if (ch == 62) escaped += "&gt;";
    else if (ch == 34) escaped += "&quot;";
    else if (ch == 39) escaped += "&#39;";
    else escaped += String.fromCharCode(ch);
  }
  return escaped;
}

function renderText(data: Uint8Array, encoding: string): string {
  if (data.length == 0) {
    return "<p class='empty'>No data to display.</p><p class='hint'>Could not decipher data. If this was unexpected, you may have found a bug. See the <a href='https://github.com/open-rct3/open-rct3/issues?q=state%3Aopen%20label%3Aplugin' target='_blank'>open plugin issues</a> or <a href='https://github.com/open-rct3/open-rct3/issues/new' target='_blank'>report a new issue</a>.</p>";
  }

  let text = decodeText(data, encoding);
  let escaped = escapeHtml(text);

  if (escaped.length == 0 || escaped.indexOf("<") != -1 && escaped.indexOf(">") != -1 && escaped.indexOf("�") != -1) {
    return "<p class='error'>Could not decode as " + encoding + ". Showing hex view instead.</p>" + renderHexView(data);
  }

  let html = `<div class='text-viewer'><pre>${escaped}</pre></div>`;
  html += `<p class='encoding'>Decoded as ${encoding.toUpperCase()}</p>`;

  return html;
}

export function render(): i32 {
  let data = Host.input();

  let html = renderText(data, "ascii");
  const asciiText = decodeText(data, "ascii");

  if (asciiText.length == 0 || asciiText.indexOf("�") != -1) {
    html = renderText(data, "utf-16le");
  } else {
    let utf16Text = decodeText(data, "utf-16le");
    if (utf16Text.length > asciiText.length * 2) {
      html = renderText(data, "utf-16le");
    }
  }

  Host.outputString(html);
  return 0;
}

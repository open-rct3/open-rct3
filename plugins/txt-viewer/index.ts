import { Host } from "@extism/as-pdk";

export function name(): string { return "Text Viewer"; }
export function version(): string { return "0.1.0"; }
export function file_types(): string { return '["txt"]'; }

function renderHexView(data: Uint8Array): string {
  let html = "<div class='hex-view'><table><thead><tr><th>Offset</th>";
  for (let h = 0; h < 16; h++) {
    html += "<th>" + h.toString(16).toUpperCase() + "</th>";
  }
  html += "<th>ASCII</th></tr></thead><tbody>";

  let rowCount = data.length / 16;
  for (let r = 0; r < rowCount; r++) {
    let offset = r * 16;
    html += "<tr><td>" + offset.toString(16).toUpperCase().padStart(4, '0') + "</td>";

    let ascii = "";
    for (let i = 0; i < 16; i++) {
      let idx = offset + i;
      if (idx < data.length) {
        let byte = data[idx];
        html += "<td>" + byte.toString(16).toUpperCase().padStart(2, '0') + "</td>";
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
          0x80 | (char & 0x3F)
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

  let html = "<div class='text-viewer'><pre>" + escaped + "</pre></div>";
  html += "<p class='encoding'>Decoded as " + encoding.toUpperCase() + "</p>";
  html += renderHexView(data);

  return html;
}

export function render(): i32 {
  let data = Host.input();

  let html = renderText(data, "ascii");
  let asciiText = decodeText(data, "ascii");

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

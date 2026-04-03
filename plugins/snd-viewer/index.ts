import { Host } from "@extism/as-pdk";

export function name(): string { return "Sound Viewer"; }
export function version(): string { return "0.1.0"; }
export function file_types(): string { return '["snd"]'; }

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

function readU16LE(data: Uint8Array, offset: i32): u16 {
  return u16(data[offset]) | (u16(data[offset + 1]) << 8);
}

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) | (u32(data[offset + 3]) << 24));
}

function renderHexView(data: Uint8Array, startOffset: i32 = 0): string {
  let html = "<div class='hex-view'><table><thead><tr><th>Offset</th>";
  for (let h = 0; h < 16; h++) {
    html += "<th>" + h.toString(16).toUpperCase() + "</th>";
  }
  html += "<th>ASCII</th></tr></thead><tbody>";

  let rowCount = data.length / 16;
  for (let r = 0; r < rowCount; r++) {
    let offset = r * 16;
    html += "<tr><td>" + toHex(startOffset + offset, 4) + "</td>";

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

function getFormatName(formatTag: u16): string {
  if (formatTag == 1) return "PCM";
  if (formatTag == 2) return "ADPCM";
  if (formatTag == 0x0011) return "IMA ADPCM";
  return "Unknown (" + formatTag.toString() + ")";
}

function renderSound(data: Uint8Array): string {
  if (data.length < 18) {
    return "<p class='error'>Data too short to contain WAVEFORMATEX header (minimum 18 bytes required).</p>" + renderHexView(data);
  }

  // Parse WAVEFORMATEX header (18 bytes)
  let formatTag = readU16LE(data, 0);
  let channels = readU16LE(data, 2);
  let sampleRate = readU32LE(data, 4);
  let avgBytesPerSec = readU32LE(data, 8);
  let blockAlign = readU16LE(data, 12);
  let bitsPerSample = readU16LE(data, 14);
  let extraSize = readU16LE(data, 16);

  let html = "<div class='sound-viewer'>";
  html += "<h3>Audio Format (WAVEFORMATEX)</h3>";
  html += "<table class='sound-format'><tbody>";
  html += "<tr><td>Format Tag</td><td>" + getFormatName(formatTag) + " (" + formatTag.toString() + ")</td></tr>";
  html += "<tr><td>Channels</td><td>" + channels.toString() + (channels == 1 ? " (Mono)" : channels == 2 ? " (Stereo)" : "") + "</td></tr>";
  html += "<tr><td>Sample Rate</td><td>" + sampleRate.toString() + " Hz</td></tr>";
  html += "<tr><td>Avg Bytes per Second</td><td>" + avgBytesPerSec.toString() + "</td></tr>";
  html += "<tr><td>Block Align</td><td>" + blockAlign.toString() + "</td></tr>";
  html += "<tr><td>Bits per Sample</td><td>" + bitsPerSample.toString() + "</td></tr>";
  html += "<tr><td>Extra Size</td><td>" + extraSize.toString() + " bytes</td></tr>";
  html += "</tbody></table>";

  // Calculate header size (18 + extraSize)
  let headerSize = 18 + i32(extraSize);
  if (data.length >= headerSize) {
    let audioDataSize = data.length - headerSize;
    html += "<p class='info'>Audio Data: " + audioDataSize.toString() + " bytes</p>";

    if (formatTag == 1 && bitsPerSample == 16 && audioDataSize > 0) {
      let sampleCount = audioDataSize / 2;
      let duration = f64(sampleCount) / f64(sampleRate);
      html += "<p class='info'>Sample Count: " + sampleCount.toString() + "</p>";
      html += "<p class='info'>Duration: " + duration.toString() + " seconds</p>";
    }
  }

  html += "</div>";
  html += renderHexView(data, 0);

  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderSound(data);
  Host.outputString(html);
  return 0;
}

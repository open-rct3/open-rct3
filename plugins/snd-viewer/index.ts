/// <reference no-default-lib="true" />
import "../types.ts";
import { Host } from "@extism/as-pdk";

export function name(): i32 {
  Host.outputString("Sound Player");
  return 0;
}
export function version(): i32 {
  Host.outputString("0.2.0");
  return 0;
}
export function file_types(): i32 {
  Host.outputString('["snd"]');
  return 0;
}

function readU16LE(data: Uint8Array, offset: i32): u16 {
  return u16(data[offset]) | (u16(data[offset + 1]) << 8);
}

function readU32LE(data: Uint8Array, offset: i32): u32 {
  return (u32(data[offset]) | (u32(data[offset + 1]) << 8) | (u32(data[offset + 2]) << 16) |
    (u32(data[offset + 3]) << 24));
}

function writeU32LE(data: Uint8Array, offset: i32, value: u32): void {
  data[offset] = u8(value & 0xFF);
  data[offset + 1] = u8((value >> 8) & 0xFF);
  data[offset + 2] = u8((value >> 16) & 0xFF);
  data[offset + 3] = u8((value >> 24) & 0xFF);
}

function constructWavFile(waveformatex: Uint8Array, extraSize: u16, formatTag: u16, audioData: Uint8Array): Uint8Array {
  let fmtChunkSize: i32;
  let fmtDataLength: i32;
  if (formatTag == 1) {
    fmtChunkSize = 16;
    fmtDataLength = 16;
  } else {
    fmtChunkSize = 18 + i32(extraSize);
    fmtDataLength = 18 + i32(extraSize);
  }

  const wavHeaderSize = 12 + 8 + fmtChunkSize + 8;
  const totalSize = wavHeaderSize + audioData.length;

  const wavFile = new Uint8Array(totalSize);
  let offset = 0;

  wavFile[offset++] = 82; // 'R'
  wavFile[offset++] = 73; // 'I'
  wavFile[offset++] = 70; // 'F'
  wavFile[offset++] = 70; // 'F'
  writeU32LE(wavFile, offset, u32(totalSize - 8));
  offset += 4;

  wavFile[offset++] = 87; // 'W'
  wavFile[offset++] = 65; // 'A'
  wavFile[offset++] = 86; // 'V'
  wavFile[offset++] = 69; // 'E'

  wavFile[offset++] = 102; // 'f'
  wavFile[offset++] = 109; // 'm'
  wavFile[offset++] = 116; // 't'
  wavFile[offset++] = 32;  // ' '
  writeU32LE(wavFile, offset, u32(fmtChunkSize));
  offset += 4;

  wavFile.set(waveformatex.subarray(0, fmtDataLength), offset);
  offset += fmtDataLength;

  wavFile[offset++] = 100; // 'd'
  wavFile[offset++] = 97;  // 'a'
  wavFile[offset++] = 116; // 't'
  wavFile[offset++] = 97;  // 'a'
  writeU32LE(wavFile, offset, u32(audioData.length));
  offset += 4;

  wavFile.set(audioData, offset);

  return wavFile;
}

function base64Encode(data: Uint8Array): string {
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
  let result = "";
  let i = 0;
  while (i < data.length) {
    const byte1 = data[i++];
    const byte2 = i < data.length ? data[i++] : -1;
    const byte3 = i < data.length ? data[i++] : -1;

    const enc1 = byte1 >> 2;
    const enc2 = ((byte1 & 3) << 4) | (byte2 != -1 ? byte2 >> 4 : 0);
    const enc3 = byte2 != -1 ? ((byte2 & 15) << 2) | (byte3 != -1 ? byte3 >> 6 : 0) : 64;
    const enc4 = byte3 != -1 ? byte3 & 63 : 64;

    result += chars.charAt(enc1);
    result += chars.charAt(enc2);
    result += enc3 != 64 ? chars.charAt(enc3) : "=";
    result += enc4 != 64 ? chars.charAt(enc4) : "=";
  }
  return result;
}

function getFormatName(formatTag: u16): string {
  if (formatTag == 1) return "PCM";
  if (formatTag == 2) return "ADPCM";
  if (formatTag == 0x0011) return "IMA ADPCM";
  return "Unknown (" + formatTag.toString() + ")";
}

function renderSound(data: Uint8Array): string {
  if (data.length < 18) {
    return "<p class='error'>Data too short to contain WAVE audio header (minimum 18 bytes required).</p>";
  }

  // Parse WAVEFORMATEX header (18 bytes)
  const formatTag = readU16LE(data, 0);
  const channels = readU16LE(data, 2);
  const sampleRate = readU32LE(data, 4);
  const avgBytesPerSec = readU32LE(data, 8);
  const blockAlign = readU16LE(data, 12);
  const bitsPerSample = readU16LE(data, 14);
  const extraSize = readU16LE(data, 16);
  const headerSize = 18 + i32(extraSize);
  const audioDataSize = data.length - headerSize;

  let html = "<div class='sound-viewer'>";
  html += `<h3>Wave ${getFormatName(formatTag)} Audio</h3>`;

  // Render an <audio> element with the audio data
  if (audioDataSize > 0) {
    const audioData = new Uint8Array(audioDataSize);
    audioData.set(data.subarray(headerSize));
    const fmtData = data.subarray(0, 18 + i32(extraSize));
    const wavFile = constructWavFile(fmtData, extraSize, formatTag, audioData);
    const base64Audio = base64Encode(wavFile);
    html += `<audio controls volume="0.5"><source src='data:audio/wav;base64,${base64Audio}'></audio>`;
    html += `<script>document.querySelector('audio').volume = 0.5;</script>`;
  } else if (formatTag != 1) {
    html += `<p class='warning'>${getFormatName(formatTag)} requires a decoder library for browser playback.</p>`;
  }

  html += "<table class='sound-format'><tbody>";
  html += "<tr><td>Format Tag</td><td>" + getFormatName(formatTag) + " (" + formatTag.toString() + ")</td></tr>";
  html += "<tr><td>Channels</td><td>" + channels.toString() +
    (channels == 1 ? " (Mono)" : channels == 2 ? " (Stereo)" : "") + "</td></tr>";
  html += "<tr><td>Sample Rate</td><td>" + sampleRate.toString() + " Hz</td></tr>";
  html += "<tr><td>Avg Bytes per Second</td><td>" + avgBytesPerSec.toString() + "</td></tr>";
  html += "<tr><td>Block Align</td><td>" + blockAlign.toString() + "</td></tr>";
  html += "<tr><td>Bits per Sample</td><td>" + bitsPerSample.toString() + "</td></tr>";
  html += "<tr><td>Extra Size</td><td>" + extraSize.toString() + " bytes</td></tr>";
  html += "</tbody></table>";

  // Calculate header size (18 + extraSize)
  if (data.length >= headerSize) {
    html += `<p class='info'>Audio Data: ${audioDataSize} bytes</p>`;

    if (formatTag == 1 && bitsPerSample == 16 && audioDataSize > 0) {
      const sampleCount = audioDataSize / 2;
      const duration = f64(sampleCount) / f64(sampleRate);
      html += `<p class='info'>Sample Count: ${sampleCount}</p>`;
      html += `<p class='info'>Duration: ${duration} seconds</p>`;
    }
  }

  html += "</div>";
  return html;
}

export function render(): i32 {
  let data = Host.input();
  let html = renderSound(data);
  Host.outputString(html);
  return 0;
}

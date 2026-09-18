// Base64 encoding table
const BASE64_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

export function encodeBase64(data: Uint8Array): string {
  let result = "";
  let i = 0;
  while (i < data.length) {
    const b1 = data[i++];
    const b2 = i < data.length ? data[i++] : 0;
    const b3 = i < data.length ? data[i++] : 0;

    result += BASE64_CHARS.charAt(b1 >> 2);
    result += BASE64_CHARS.charAt(((b1 & 0x03) << 4) | (b2 >> 4));
    result += i < data.length + 2 ? BASE64_CHARS.charAt(((b2 & 0x0F) << 2) | (b3 >> 6)) : "=";
    result += i < data.length + 1 ? BASE64_CHARS.charAt(b3 & 0x3F) : "=";
  }
  return result;
}

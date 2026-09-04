// Color utilities and extensions for vectors, integer CSS hex codes, OpenGL, and RGBA values.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Globalization;
using System.Numerics;
using Drawing = System.Drawing;

namespace OpenRCT3;

/// <summary>
/// Color conversion extensions between <see cref="Drawing.Color"/>, normalized
/// vectors, CSS hex integer codes, and RGBA components.
/// </summary>
/// <remarks>
/// Domain-agnostic color operations live in
/// <see cref="OpenCobra.GDK.Numerics.Color"/>.
/// </remarks>
public static class Color {
  public static Drawing.Color FromRgb(int rgb) =>
    Drawing.Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

  public static Drawing.Color FromRgba(int rgba) =>
    Drawing.Color.FromArgb(
      (byte)(unchecked((uint)rgba) & 0xFF),
      (byte)((unchecked((uint)rgba) >> 24) & 0xFF),
      (byte)((unchecked((uint)rgba) >> 16) & 0xFF),
      (byte)((unchecked((uint)rgba) >> 8) & 0xFF));

  public static Drawing.Color FromRgba(byte r, byte g, byte b, byte a = 255) =>
    Drawing.Color.FromArgb(a, r, g, b);

  public static Drawing.Color FromCss(string hex) {
    var span = hex.AsSpan().Trim();
    if (span.StartsWith("#")) span = span[1..];
    if (span.Length == 6) {
      var r = byte.Parse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      var g = byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      var b = byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      return Drawing.Color.FromArgb(255, r, g, b);
    }
    if (span.Length == 8) {
      var r = byte.Parse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      var g = byte.Parse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      var b = byte.Parse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      var a = byte.Parse(span[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      return Drawing.Color.FromArgb(a, r, g, b);
    }
    throw new FormatException($"Invalid CSS color format: '{hex}'.");
  }

  #region Color Conversion Extensions
  public static Vector4 ToVector4(this Drawing.Color color) =>
    new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

  public static Vector3 ToVector3(this Drawing.Color color) =>
    new(color.R / 255f, color.G / 255f, color.B / 255f);

  /// <summary>
  /// Converts a <see cref="Drawing.Color"/> into a normalized OpenGL <see cref="Vector4"/> (RGBA).
  /// </summary>
  public static Vector4 ToGl(this Drawing.Color color) => color.ToVector4();

  public static string ToCss(this Drawing.Color color, bool includeAlpha = false) =>
    includeAlpha
      ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
      : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

  public static int ToRgb(this Drawing.Color color) =>
    (color.R << 16) | (color.G << 8) | color.B;

  public static int ToRgba(this Drawing.Color color) =>
    (color.R << 24) | (color.G << 16) | (color.B << 8) | color.A;

  /// <summary>
  /// Converts a <see cref="Drawing.Color"/> to a packed RGBA unsigned 32-bit
  /// integer value.
  /// </summary>
  public static uint ToRgbaUint(this Drawing.Color color) =>
    ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A;

  /// <summary>
  /// Converts a <see cref="Drawing.Color"/> to a packed ABGR unsigned 32-bit
  /// integer value.
  /// </summary>
  public static uint ToAbgrUint(this Drawing.Color color) =>
    (uint)color.R | ((uint)color.G << 8) | ((uint)color.B << 16) | ((uint)color.A << 24);

  public static Drawing.Color ToColor(this Vector4 vec) =>
    Drawing.Color.FromArgb(
      Convert.ToByte(Math.Clamp(vec.W * 255f, 0f, 255f)),
      Convert.ToByte(Math.Clamp(vec.X * 255f, 0f, 255f)),
      Convert.ToByte(Math.Clamp(vec.Y * 255f, 0f, 255f)),
      Convert.ToByte(Math.Clamp(vec.Z * 255f, 0f, 255f)));

  public static Drawing.Color ToColor(this Vector3 vec) =>
    Drawing.Color.FromArgb(
      255,
      Convert.ToByte(Math.Clamp(vec.X * 255f, 0f, 255f)),
      Convert.ToByte(Math.Clamp(vec.Y * 255f, 0f, 255f)),
      Convert.ToByte(Math.Clamp(vec.Z * 255f, 0f, 255f)));
  #endregion
}

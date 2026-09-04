// Color utilities and extensions for vectors, integer CSS hex codes, OpenGL, and RGBA values.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Globalization;
using System.Numerics;
using Drawing = System.Drawing;

namespace OpenRCT3;

/// <summary>
/// Color conversion methods and extensions between <see cref="Drawing.Color"/>, normalized vectors,
/// CSS hex integer codes, and RGBA components.
/// </summary>
public static class Color {
  public static Drawing.Color FromRgb(int rgb) =>
    Drawing.Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

  public static Drawing.Color FromRgba(int rgba) {
    var u = (uint)rgba;
    return Drawing.Color.FromArgb((byte)(u & 0xFF), (byte)((u >> 24) & 0xFF), (byte)((u >> 16) & 0xFF), (byte)((u >> 8) & 0xFF));
  }

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

  public static uint ToRgbaUint(this Drawing.Color color) =>
    (uint)((color.R << 24) | (color.G << 16) | (color.B << 8) | color.A);

  /// <summary>
  /// Converts a <see cref="Drawing.Color"/> into an unsigned 32-bit integer packed RGBA value (e.g. for ImGui).
  /// </summary>
  public static uint ToUint(this Drawing.Color color) =>
    (uint)((color.R) | (color.G << 8) | (color.B << 16) | (color.A << 24));

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

  /// <summary>Calculates the WCAG 2.1 relative luminance of an ImGui packed ABGR color.</summary>
  public static double CalculateLuminance(uint color) {
    static double ChannelLuminance(byte c) {
      var s = c / 255.0;
      return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
    var r = ChannelLuminance(Convert.ToByte(color & 0xFF));
    var g = ChannelLuminance(Convert.ToByte((color >> 8) & 0xFF));
    var b = ChannelLuminance(Convert.ToByte((color >> 16) & 0xFF));
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  }

  /// <summary>Calculates the WCAG 2.1 relative luminance of a <see cref="Drawing.Color"/>.</summary>
  public static double CalculateLuminance(Drawing.Color color) => CalculateLuminance(color.ToUint());

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two ImGui packed ABGR colors.</summary>
  public static double CalculateContrastRatio(uint color1, uint color2) {
    var l1 = CalculateLuminance(color1);
    var l2 = CalculateLuminance(color2);
    var lighter = Math.Max(l1, l2);
    var darker = Math.Min(l1, l2);
    return (lighter + 0.05) / (darker + 0.05);
  }

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two <see cref="Drawing.Color"/> instances.</summary>
  public static double CalculateContrastRatio(Drawing.Color color1, Drawing.Color color2) =>
    CalculateContrastRatio(color1.ToUint(), color2.ToUint());

  /// <summary>Composites a foreground color over a background color taking alpha transparency into account.</summary>
  public static uint BlendOver(uint foreground, uint background) {
    var a = Convert.ToByte((foreground >> 24) & 0xFF) / 255f;
    var invA = 1f - a;
    var r = Convert.ToByte(Math.Clamp((foreground & 0xFF) * a + (background & 0xFF) * invA, 0f, 255f));
    var g = Convert.ToByte(Math.Clamp(((foreground >> 8) & 0xFF) * a + (((background >> 8) & 0xFF) * invA), 0f, 255f));
    var b = Convert.ToByte(Math.Clamp(((foreground >> 16) & 0xFF) * a + (((background >> 16) & 0xFF) * invA), 0f, 255f));
    return (uint)(r | (g << 8) | (b << 16) | (0xFF << 24));
  }

  /// <summary>Composites a foreground <see cref="Drawing.Color"/> over a background <see cref="Drawing.Color"/>.</summary>
  public static Drawing.Color BlendOver(Drawing.Color foreground, Drawing.Color background) {
    var blended = BlendOver(foreground.ToUint(), background.ToUint());
    return FromRgba(unchecked((int)blended));
  }

  /// <summary>
  /// Resolves an accessible label color satisfying the WCAG 2.1 Level AA minimum contrast ratio (4.5:1).
  /// </summary>
  public static uint ResolveLabelColor(uint lineColor, uint backgroundColor = 0xFF1E1E1E, uint? fillColor = null) {
    var opaqueColor = (lineColor & 0x00FFFFFF) | 0xFF000000;
    var bgContrast = CalculateContrastRatio(opaqueColor, backgroundColor);
    var fillContrast = fillColor.HasValue && fillColor.Value != 0
      ? CalculateContrastRatio(opaqueColor, BlendOver(fillColor.Value, backgroundColor))
      : 21.0;

    if (bgContrast >= 4.5 && fillContrast >= 4.5)
      return opaqueColor;

    var whiteContrast = Math.Min(
      CalculateContrastRatio(0xFFFFFFFF, backgroundColor),
      fillColor.HasValue && fillColor.Value != 0
        ? CalculateContrastRatio(0xFFFFFFFF, BlendOver(fillColor.Value, backgroundColor))
        : 21.0
    );
    var blackContrast = Math.Min(
      CalculateContrastRatio(0xFF000000, backgroundColor),
      fillColor.HasValue && fillColor.Value != 0
        ? CalculateContrastRatio(0xFF000000, BlendOver(fillColor.Value, backgroundColor))
        : 21.0
    );

    return whiteContrast >= blackContrast ? 0xFFFFFFFF : 0xFF000000;
  }
}

// Domain-agnostic color utilities for WCAG 2.1 operations and color-space conversion.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Globalization;
using System.Numerics;
using Drawing = System.Drawing;

namespace OpenCobra.GDK.Numerics;

/// <summary>
/// Provides WCAG 2.1 luminance, contrast ratio, alpha-blending, accessible label-color resolution,
/// and Drawing.Color <-> ImGui ABGR uint conversion helpers. All methods are static and domain-agnostic.
/// </summary>
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

  /// <summary>Calculates the WCAG 2.1 relative luminance of a Drawing.Color.</summary>
  public static double CalculateLuminance(Drawing.Color color) => CalculateLuminance(ToUint(color));

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two ImGui packed ABGR colors.</summary>
  public static double CalculateContrastRatio(uint color1, uint color2) {
    var l1 = CalculateLuminance(color1);
    var l2 = CalculateLuminance(color2);
    var lighter = Math.Max(l1, l2);
    var darker = Math.Min(l1, l2);
    return (lighter + 0.05) / (darker + 0.05);
  }

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two Drawing.Color instances.</summary>
  public static double CalculateContrastRatio(Drawing.Color color1, Drawing.Color color2) =>
    CalculateContrastRatio(ToUint(color1), ToUint(color2));

  /// <summary>Composites a foreground color over a background color using alpha-blending.</summary>
  public static uint BlendOver(uint foreground, uint background) {
    var a = Convert.ToByte((foreground >> 24) & 0xFF) / 255f;
    var invA = 1f - a;
    var r = Convert.ToByte(Math.Clamp((foreground & 0xFF) * a + (background & 0xFF) * invA, 0f, 255f));
    var g = Convert.ToByte(Math.Clamp(((foreground >> 8) & 0xFF) * a + ((background >> 8) & 0xFF) * invA, 0f, 255f));
    var b = Convert.ToByte(Math.Clamp(((foreground >> 16) & 0xFF) * a + ((background >> 16) & 0xFF) * invA, 0f, 255f));
    return (uint)(r | (g << 8) | (b << 16) | (0xFF << 24));
  }

  /// <summary>Composites a foreground Drawing.Color over a background Drawing.Color.</summary>
  public static Drawing.Color BlendOver(Drawing.Color foreground, Drawing.Color background) {
    var blended = BlendOver(ToUint(foreground), ToUint(background));
    return Drawing.Color.FromArgb(
      Convert.ToByte((blended >> 24) & 0xFF),
      Convert.ToByte(blended & 0xFF),
      Convert.ToByte((blended >> 8) & 0xFF),
      Convert.ToByte((blended >> 16) & 0xFF));
  }

  /// <summary>
  /// Resolves an accessible label color satisfying WCAG 2.1 Level AA minimum contrast (4.5:1).
  /// Returns either white (0xFFFFFFFF) or black (0xFF000000) whichever provides higher contrast.
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

  /// <summary>Packs a Drawing.Color into an ImGui ABGR uint (R|G<<8|B<<16|A<<24).</summary>
  public static uint ToUint(Drawing.Color color) =>
    (uint)(color.R | (color.G << 8) | (color.B << 16) | (color.A << 24));
}

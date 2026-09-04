// Immediate GUI graphing widget for time-series and telemetry data.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using Hexa.NET.ImGui;
using OpenCobra.GDK.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>Renders rolling telemetry and time-series line graphs in an immediate GUI context.</summary>
public static class Graph {
  /// <summary>Formats an axis limit label rounded to the nearest whole integer.</summary>
  public static string FormatAxisLabel(float value) => $"{Convert.ToInt32(MathF.Round(value))}";

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

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two colors.</summary>
  public static double CalculateContrastRatio(uint color1, uint color2) {
    var l1 = CalculateLuminance(color1);
    var l2 = CalculateLuminance(color2);
    var lighter = Math.Max(l1, l2);
    var darker = Math.Min(l1, l2);
    return (lighter + 0.05) / (darker + 0.05);
  }

  /// <summary>Composites a foreground color over a background color taking alpha into account.</summary>
  public static uint BlendOver(uint foreground, uint background) {
    var a = Convert.ToByte((foreground >> 24) & 0xFF) / 255f;
    var invA = 1f - a;
    var r = Convert.ToByte(Math.Clamp((foreground & 0xFF) * a + (background & 0xFF) * invA, 0f, 255f));
    var g = Convert.ToByte(Math.Clamp(((foreground >> 8) & 0xFF) * a + (((background >> 8) & 0xFF) * invA), 0f, 255f));
    var b = Convert.ToByte(Math.Clamp(((foreground >> 16) & 0xFF) * a + (((background >> 16) & 0xFF) * invA), 0f, 255f));
    return (uint)(r | (g << 8) | (b << 16) | (0xFF << 24));
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

  /// <summary>Parameters configuring the display and layout of a <see cref="Graph.Polyline"/> render.</summary>
  public readonly record struct Plot(
    IReadOnlyList<float> Values,
    int Capacity,
    Vector2 Size,
    uint LineColor,
    uint FillColor = 0,
    float TargetScale = 0f,
    float Thickness = 2f,
    bool ShowXAxis = false,
    bool ShowYAxis = true
  ) {
    public Plot(
      IReadOnlyList<float> values,
      int capacity,
      Size<int> size,
      uint lineColor,
      uint fillColor = 0,
      float targetScale = 0f,
      float thickness = 2f,
      bool showXAxis = false,
      bool showYAxis = true
    ) : this(values, capacity, new Vector2(size.Width, size.Height), lineColor, fillColor, targetScale, thickness, showXAxis, showYAxis) { }

    public Plot(
      IReadOnlyList<float> values,
      int capacity,
      Size size,
      uint lineColor,
      uint fillColor = 0,
      float targetScale = 0f,
      float thickness = 2f,
      bool showXAxis = false,
      bool showYAxis = true
    ) : this(values, capacity, new Vector2(size.Width, size.Height), lineColor, fillColor, targetScale, thickness, showXAxis, showYAxis) { }

    public Plot(
      IReadOnlyList<float> values,
      int capacity,
      Size<float> size,
      uint lineColor,
      uint fillColor = 0,
      float targetScale = 0f,
      float thickness = 2f,
      bool showXAxis = false,
      bool showYAxis = true
    ) : this(values, capacity, new Vector2(size.Width, size.Height), lineColor, fillColor, targetScale, thickness, showXAxis, showYAxis) { }
  }

  /// <summary>Renders a rolling polyline graph with an optional filled area below the curve.</summary>
  public static void Polyline(Plot plot) {
    if (plot.Capacity <= 1)
      throw new ArgumentOutOfRangeException(nameof(plot), "Capacity must be greater than one.");

    var cursorScreenPos = ImGui.GetCursorScreenPos();
    ImGui.Dummy(plot.Size);

    var values = plot.Values;
    var count = values.Count;
    if (count < 2) return;

    var scale = plot.TargetScale > 0f ? plot.TargetScale : 1f;

    Span<Vector2> points = stackalloc Vector2[count];
    var width = plot.Size.X;
    var height = plot.Size.Y;
    var spanDivisor = (float)(plot.Capacity - 1);

    for (var i = 0; i < count; i++) {
      var x = cursorScreenPos.X + i / spanDivisor * width;
      var normalized = Math.Clamp(values[i] / scale, 0f, 1f);
      var y = cursorScreenPos.Y + height - (normalized * height);
      points[i] = new Vector2(x, y);
    }

    var drawList = ImGui.GetWindowDrawList();
    var axisColor = (plot.LineColor & 0x00FFFFFF) | 0x44000000;
    var labelColor = ResolveLabelColor(plot.LineColor, 0xFF1E1E1E, plot.FillColor != 0 ? plot.FillColor : null);

    // Y-axis line and min/max labels
    if (plot.ShowYAxis) {
      var axisStart = new Vector2(cursorScreenPos.X, cursorScreenPos.Y);
      var axisEnd = new Vector2(cursorScreenPos.X, cursorScreenPos.Y + height);
      drawList.AddLine(axisStart, axisEnd, axisColor, 1f);

      // Max value label at the top of the Y-axis
      var maxLabel = FormatAxisLabel(scale);
      drawList.AddText(new Vector2(cursorScreenPos.X + 3f, cursorScreenPos.Y), labelColor, maxLabel);
    }

    // X-axis baseline
    if (plot.ShowXAxis) {
      var baselineStart = new Vector2(cursorScreenPos.X, cursorScreenPos.Y + height);
      var baselineEnd = new Vector2(cursorScreenPos.X + width, cursorScreenPos.Y + height);
      drawList.AddLine(baselineStart, baselineEnd, axisColor, 1f);
    }

    if (plot.FillColor != 0) {
      var baselineY = cursorScreenPos.Y + height;
      for (var i = 0; i < count - 1; i++) {
        var p0 = new Vector2(points[i].X, baselineY);
        var p1 = points[i];
        var p2 = points[i + 1];
        var p3 = new Vector2(points[i + 1].X, baselineY);
        drawList.AddQuadFilled(p0, p1, p2, p3, plot.FillColor);
      }
    }

    unsafe {
      fixed (Vector2* pLine = points) {
        drawList.AddPolyline(pLine, count, plot.LineColor, ImDrawFlags.None, plot.Thickness);
      }
    }
  }
}

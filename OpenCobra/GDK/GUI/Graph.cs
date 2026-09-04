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
    var labelColor = Color.ResolveLabelColor(plot.LineColor, 0xFF1E1E1E, plot.FillColor != 0 ? plot.FillColor : null);

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

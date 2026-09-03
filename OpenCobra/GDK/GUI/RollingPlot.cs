// Rolling polyline plot widget for telemetry and time-series data.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>Renders a rolling time-series polyline graph filling available parent bounds in whole pixels.</summary>
public class RollingPlot(
  int capacity,
  uint lineColor,
  uint fillColor = 0,
  float targetScale = 0f,
  float thickness = 2f
) : IWidget {
  private readonly List<float> samples = [];
  private readonly uint DefaultHeight = 24;
  private float currentScale = targetScale > 0f ? targetScale : 1f;

  public int Capacity { get; } = capacity;
  public float CurrentScale => currentScale;
  public uint LineColor { get; set; } = lineColor;
  public uint FillColor { get; set; } = fillColor;
  /// <summary>
  /// Minimum vertical scale for values, expanding dynamically if values exceed this threshold.
  /// </summary>
  /// <remarks>
  /// When plotting samples, the vertical range normalizes against the greater of this value or the maximum
  /// observed sample point. Transitions in scale are smoothed exponentially between frames to preserve visual
  /// stability and prevent abrupt squishing from temporary spikes.
  /// </remarks>
  public float TargetScale { get; set; } = targetScale;
  public float Thickness { get; set; } = thickness;
  public bool ShowXAxis { get; set; } = false;
  public bool ShowYAxis { get; set; } = true;
  public int Count => samples.Count;
  public IReadOnlyList<float> Samples => samples;

  /// <summary>Pushes a new sample into the rolling plot history, evicting the oldest sample if capacity is reached.</summary>
  public void Push(float value) {
    if (samples.Count >= Capacity && samples.Count > 0)
      samples.RemoveAt(0);
    samples.Add(value);
  }

  /// <summary>Clears all recorded samples.</summary>
  public void Clear() {
    samples.Clear();
    currentScale = TargetScale > 0f ? TargetScale : 1f;
  }

  /// <summary>Updates the smoothed vertical scale target based on currently retained samples.</summary>
  /// <returns>The updated smoothed scale value.</returns>
  public float UpdateScale() {
    var targetMax = TargetScale;
    foreach (var sample in samples) {
      if (sample > targetMax) targetMax = sample;
    }
    if (targetMax <= 0f) targetMax = 1f;

    // Smooth vertical scale transition with exponential decay for visual stability
    currentScale += (targetMax - currentScale) * 0.1f;
    return currentScale;
  }

  /// <summary>Renders the rolling plot filling the layout constraints passed by the parent.</summary>
  /// <param name="constraints">The layout boundaries passed down by the parent container.</param>
  /// <returns>The resolved size in whole pixels allocated for the plot.</returns>
  public Size<int> Render(BoxConstraints constraints) {
    // Ensure the minimum height is at least the default height to avoid flat plots
    constraints = constraints with { MinHeight = Convert.ToInt32(Math.Max(DefaultHeight, constraints.MinHeight)) };

    var width = constraints.MaxWidth == int.MaxValue ? constraints.MinWidth : constraints.MaxWidth;
    var height = constraints.MaxHeight == int.MaxValue ? constraints.MinHeight : constraints.MaxHeight;
    var resolvedSize = constraints.Constrain(new Size<int>(width, height));

    UpdateScale();

    Graph.Polyline(new Graph.Plot(
      samples,
      Capacity,
      resolvedSize,
      LineColor,
      FillColor,
      currentScale,
      Thickness,
      ShowXAxis,
      ShowYAxis
    ));
    return resolvedSize;
  }
}

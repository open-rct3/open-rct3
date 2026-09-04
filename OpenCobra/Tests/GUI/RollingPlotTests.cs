// Unit tests for BoxConstraints layout and RollingPlot widget.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Numerics;

namespace OVL.Tests.GUI;

[TestFixture]
public class RollingPlotTests {
  [Test]
  [Description("Clamps arbitrary dimensions to box constraints bounds.")]
  public void ConstrainToBounds() {
    var constraints = new BoxConstraints(MinWidth: 10, MaxWidth: 100, MinHeight: 20, MaxHeight: 50);
    var small = constraints.Constrain(new Size<int>(5, 10));
    var large = constraints.Constrain(new Size<int>(200, 100));
    var normal = constraints.Constrain(new Size<int>(50, 30));

    using (Assert.EnterMultipleScope()) {
      Assert.That(small.Width, Is.EqualTo(10));
      Assert.That(small.Height, Is.EqualTo(20));

      Assert.That(large.Width, Is.EqualTo(100));
      Assert.That(large.Height, Is.EqualTo(50));

      Assert.That(normal.Width, Is.EqualTo(50));
      Assert.That(normal.Height, Is.EqualTo(30));
    }
  }

  [Test]
  [Description("Pushes samples and evicts the oldest entry when capacity is reached.")]
  public void EvictOldestSampleAtCapacity() {
    var plot = new RollingPlot(capacity: 3, lineColor: 0xFFFFFFFF);
    Assert.That(plot.Count, Is.Zero);

    plot.Push(10f);
    plot.Push(20f);
    using (Assert.EnterMultipleScope()) {
      Assert.That(plot.Count, Is.EqualTo(2));
      Assert.That(plot.Samples[0], Is.EqualTo(10f));
      Assert.That(plot.Samples[1], Is.EqualTo(20f));
    }

    plot.Push(30f);
    plot.Push(40f);
    using (Assert.EnterMultipleScope()) {
      Assert.That(plot.Count, Is.EqualTo(3));
      Assert.That(plot.Samples[0], Is.EqualTo(20f));
      Assert.That(plot.Samples[1], Is.EqualTo(30f));
      Assert.That(plot.Samples[2], Is.EqualTo(40f));
    }
  }

  [Test]
  [Description("Clears all recorded samples while retaining smoothed scale.")]
  public void ClearSamples() {
    var plot = new RollingPlot(capacity: 5, lineColor: 0xFFFFFFFF, targetScale: 33.33f);
    plot.Push(1f);
    plot.Push(2f);
    Assert.That(plot.CurrentScale, Is.EqualTo(33.33f));
    plot.Clear();

    using (Assert.EnterMultipleScope()) {
      Assert.That(plot.Count, Is.Zero);
      Assert.That(plot.Samples, Is.Empty);
      Assert.That(plot.CurrentScale, Is.EqualTo(33.33f));
    }
  }

  [Test]
  [Description("Decays smoothed vertical scale smoothly after transient spikes fall off.")]
  public void SmoothScaleDecay() {
    var plot = new RollingPlot(capacity: 2, lineColor: 0xFFFFFFFF, targetScale: 10f);
    // Push a spike
    plot.Push(100f);
    plot.UpdateScale();
    // Scale should have started moving towards 100
    Assert.That(plot.CurrentScale, Is.GreaterThan(10f));

    // Push two normal values to evict the 100f spike
    plot.Push(10f);
    plot.Push(10f);

    // At this point, the 100f sample is evicted from the ring
    Assert.That(plot.Samples, Does.Not.Contain(100f));

    var scaleBeforeEvictedRender = plot.CurrentScale;
    plot.UpdateScale();

    // Scale must now decay downwards back towards 10f, ignoring the evicted spike
    Assert.That(plot.CurrentScale, Is.LessThan(scaleBeforeEvictedRender));
  }

  [Test]
  [Description("Initializes axis visibility with default states (X hidden, Y visible).")]
  public void DefaultAxisVisibility() {
    var plot = new RollingPlot(capacity: 10, lineColor: 0xFFFFFFFF);
    using (Assert.EnterMultipleScope()) {
      Assert.That(plot.ShowXAxis, Is.False);
      Assert.That(plot.ShowYAxis, Is.True);
    }
  }

  [Test]
  [Description("Maps integer dimensions and axis flags to the plot parameters record.")]
  public void MapDimensionsFromIntSize() {
    var samples = new float[] { 1f, 2f, 3f };
    var parameters = new Graph.Plot(
      samples,
      capacity: 10,
      size: new Size<int>(100, 50),
      lineColor: 0xFF00FF00,
      fillColor: 0x3300FF00,
      targetScale: 25f,
      thickness: 1.5f,
      showXAxis: true,
      showYAxis: false
    );

    using (Assert.EnterMultipleScope()) {
      Assert.That(parameters.Size.X, Is.EqualTo(100f));
      Assert.That(parameters.Size.Y, Is.EqualTo(50f));
      Assert.That(parameters.ShowXAxis, Is.True);
      Assert.That(parameters.ShowYAxis, Is.False);
      Assert.That(parameters.TargetScale, Is.EqualTo(25f));
    }
  }

  [Test]
  [Description("Returns null summary statistics when the sample buffer is empty.")]
  public void EmptyStatisticsIsNull() {
    var plot = new RollingPlot(capacity: 10, lineColor: 0xFFFFFFFF);
    Assert.That(plot.Summary, Is.Null);
  }

  [Test]
  [Description("Calculates accurate min, max, average, and standard deviation summary statistics.")]
  public void CalculateSummaryStatistics() {
    var plot = new RollingPlot(capacity: 10, lineColor: 0xFFFFFFFF);
    plot.Push(10f);
    plot.Push(20f);
    plot.Push(30f);

    var stats = plot.Summary;
    Assert.That(stats, Is.Not.Null);
    using (Assert.EnterMultipleScope()) {
      Assert.That(stats!.Value.Min, Is.EqualTo(10f));
      Assert.That(stats!.Value.Max, Is.EqualTo(30f));
      Assert.That(stats!.Value.Average, Is.EqualTo(20.0).Within(1e-5));
      // Population variance = ((10-20)^2 + (20-20)^2 + (30-20)^2) / 3 = 200 / 3 ≈ 66.6667; sqrt ≈ 8.164965
      Assert.That(stats!.Value.StandardDeviation, Is.EqualTo(Math.Sqrt(200.0 / 3.0)).Within(1e-5));
    }
  }

  [Test]
  [Description("Rounds floating-point axis limit values to the nearest whole integer.")]
  public void RoundAxisLabels() {
    using (Assert.EnterMultipleScope()) {
      Assert.That(Graph.FormatAxisLabel(33.33f), Is.EqualTo("33"));
      Assert.That(Graph.FormatAxisLabel(33.6f), Is.EqualTo("34"));
      Assert.That(Graph.FormatAxisLabel(0.4f), Is.EqualTo("0"));
      Assert.That(Graph.FormatAxisLabel(0f), Is.EqualTo("0"));
    }
  }

  [Test]
  [Description("Maps floating-point dimensions and axis flags to the plot parameters record.")]
  public void MapDimensionsFromFloatSize() {
    var samples = new float[] { 1f, 2f };
    var parameters = new Graph.Plot(
      samples,
      capacity: 5,
      size: new Size<float>(120f, 60f),
      lineColor: 0xFFFFFFFF,
      fillColor: 0x00000000,
      targetScale: 40f,
      thickness: 2f,
      showXAxis: false,
      showYAxis: true
    );

    using (Assert.EnterMultipleScope()) {
      Assert.That(parameters.Size.X, Is.EqualTo(120f));
      Assert.That(parameters.Size.Y, Is.EqualTo(60f));
      Assert.That(parameters.ShowXAxis, Is.False);
      Assert.That(parameters.ShowYAxis, Is.True);
      Assert.That(parameters.TargetScale, Is.EqualTo(40f));
    }
  }

  [Test]
  [Description("Maps unified Size dimensions and axis flags to the plot parameters record.")]
  public void MapDimensionsFromUnifiedSize() {
    var samples = new float[] { 1f, 2f };
    var parameters = new Graph.Plot(
      samples,
      capacity: 5,
      size: new Size(150, 75),
      lineColor: 0xFFFFFFFF,
      fillColor: 0x00000000,
      targetScale: 50f,
      thickness: 1f,
      showXAxis: true,
      showYAxis: true
    );

    using (Assert.EnterMultipleScope()) {
      Assert.That(parameters.Size.X, Is.EqualTo(150f));
      Assert.That(parameters.Size.Y, Is.EqualTo(75f));
      Assert.That(parameters.ShowXAxis, Is.True);
      Assert.That(parameters.ShowYAxis, Is.True);
      Assert.That(parameters.TargetScale, Is.EqualTo(50f));
    }
  }

  [Test]
  [Description("Throws ArgumentOutOfRangeException when plot capacity is less than or equal to one.")]
  public void ValidateMinimumCapacity() {
    var samples = new float[] { 1f, 2f };
    var invalidPlot = new Graph.Plot(
      samples,
      capacity: 1,
      size: new Size<int>(100, 50),
      lineColor: 0xFFFFFFFF
    );

    Assert.Throws<ArgumentOutOfRangeException>(new Action(() => Graph.Polyline(invalidPlot)));
  }

  [Test]
  [Description("Calculates the standard maximum 21:1 contrast ratio between black and white.")]
  public void ContrastRatioBlackAndWhite() {
    var ratio = Graph.CalculateContrastRatio(0xFFFFFFFFu, 0xFF000000u);
    Assert.That(ratio, Is.EqualTo(21.0).Within(0.01));
  }

  [Test]
  [Description("Resolves accessible label colors maintaining WCAG AA 4.5:1 contrast against window background and plot fill.")]
  public void ContrastRatioAgainstBackgroundAndFill() {
    var lineColor = 0xFF50AF4Cu; // #4CAF50 in ImGui ABGR
    var fillColor = 0x5950AF4Cu; // 35% alpha #4CAF50 fill
    var windowBg = 0xFF1E1E1Eu;  // Standard dark ImGui window background

    var resolvedColor = Graph.ResolveLabelColor(lineColor, windowBg, fillColor);
    var effectiveFillBg = Graph.BlendOver(fillColor, windowBg);

    var windowContrast = Graph.CalculateContrastRatio(resolvedColor, windowBg);
    var fillContrast = Graph.CalculateContrastRatio(resolvedColor, effectiveFillBg);

    using (Assert.EnterMultipleScope()) {
      Assert.That(windowContrast, Is.GreaterThanOrEqualTo(4.5), "Label contrast against window background must satisfy WCAG AA.");
      Assert.That(fillContrast, Is.GreaterThanOrEqualTo(4.5), "Label contrast against plot fill must satisfy WCAG AA.");
    }
  }
}

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
  public void BoxConstraints_Constrain_ClampsToBounds() {
    var constraints = new BoxConstraints(MinWidth: 10, MaxWidth: 100, MinHeight: 20, MaxHeight: 50);
    var small = constraints.Constrain(new Size<int>(5, 10));
    var large = constraints.Constrain(new Size<int>(200, 100));
    var normal = constraints.Constrain(new Size<int>(50, 30));

    Assert.That(small.Width, Is.EqualTo(10));
    Assert.That(small.Height, Is.EqualTo(20));

    Assert.That(large.Width, Is.EqualTo(100));
    Assert.That(large.Height, Is.EqualTo(50));

    Assert.That(normal.Width, Is.EqualTo(50));
    Assert.That(normal.Height, Is.EqualTo(30));
  }

  [Test]
  public void RollingPlot_PushAndCapacity_EvictsOldestSample() {
    var plot = new RollingPlot(capacity: 3, lineColor: 0xFFFFFFFF);
    Assert.That(plot.Count, Is.EqualTo(0));

    plot.Push(10f);
    plot.Push(20f);
    Assert.That(plot.Count, Is.EqualTo(2));
    Assert.That(plot.Samples[0], Is.EqualTo(10f));
    Assert.That(plot.Samples[1], Is.EqualTo(20f));

    plot.Push(30f);
    plot.Push(40f);
    Assert.That(plot.Count, Is.EqualTo(3));
    Assert.That(plot.Samples[0], Is.EqualTo(20f));
    Assert.That(plot.Samples[1], Is.EqualTo(30f));
    Assert.That(plot.Samples[2], Is.EqualTo(40f));
  }

  [Test]
  public void RollingPlot_Clear_EmptiesSamples() {
    var plot = new RollingPlot(capacity: 5, lineColor: 0xFFFFFFFF, targetScale: 33.33f);
    plot.Push(1f);
    plot.Push(2f);
    Assert.That(plot.CurrentScale, Is.EqualTo(33.33f));
    plot.Clear();

    Assert.That(plot.Count, Is.EqualTo(0));
    Assert.That(plot.Samples, Is.Empty);
    Assert.That(plot.CurrentScale, Is.EqualTo(33.33f));
  }

  [Test]
  public void RollingPlot_WhenSpikeFallsOff_AdjustsScaleSmoothly() {
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
  public void RollingPlot_AxisProperties_HaveExpectedDefaults() {
    var plot = new RollingPlot(capacity: 10, lineColor: 0xFFFFFFFF);
    Assert.That(plot.ShowXAxis, Is.False);
    Assert.That(plot.ShowYAxis, Is.True);
  }

  [Test]
  public void PlotLinesParameters_WithSizeInt_MapsDimensionsCorrectly() {
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

    Assert.That(parameters.Size.X, Is.EqualTo(100f));
    Assert.That(parameters.Size.Y, Is.EqualTo(50f));
    Assert.That(parameters.ShowXAxis, Is.True);
    Assert.That(parameters.ShowYAxis, Is.False);
    Assert.That(parameters.TargetScale, Is.EqualTo(25f));
  }
}

// Track Segment Decoding Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TrackDataExtractorsTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void ExtractSplines_FromTrackOVL_ReturnsValidData() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var trackPath = Path.Combine(rct3Path, "tracks", "coasters", "LoopingRC", "LoopingRC.common.ovl");
    if (!File.Exists(trackPath)) Assert.Inconclusive($"Track OVL not found");

    using var ovl = Ovl.Load(trackPath);
    var splines = TrackData.ExtractSplines(ovl);

    using (Assert.EnterMultipleScope()) {
      Assert.That(splines, Is.Not.Empty, "LoopingRC track should contain splines");
      Assert.That(splines.All(s => s.NodeCount > 0), "All splines must have nodes");
      Assert.That(splines.All(s => s.Nodes.Length == (int)s.NodeCount), "Node arrays match counts");
      Assert.That(splines.All(s => s.TotalLength >= 0f), "Total lengths must be non-negative");
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void ExtractTrackSections_FromTrackOVL_ValidatesReferences() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var trackPath = Path.Combine(rct3Path, "tracks", "coasters", "Track1", "Track1.common.ovl");
    if (!File.Exists(trackPath)) Assert.Inconclusive($"Track OVL not found");

    using var ovl = Ovl.Load(trackPath);
    var sections = TrackData.ExtractTrackSections(ovl);

    using (Assert.EnterMultipleScope()) {
      Assert.That(sections, Is.Not.Empty, "Track1 should contain track sections");
      Assert.That(sections.All(s => s.SplineRefs.Length == 6), "Each section has 6 spline refs");
      Assert.That(sections.Count(s => s.IsValid), Is.GreaterThan(0), "At least some sections should have valid local spline references");
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void ExtractSplines_FromTrack25_VerifiesGeometry() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var trackPath = Path.Combine(rct3Path, "tracks", "coasters", "Track25", "Track25.common.ovl");
    if (!File.Exists(trackPath)) Assert.Inconclusive($"Track25 OVL not found");

    using var ovl = Ovl.Load(trackPath);
    var splines = TrackData.ExtractSplines(ovl);
    Assert.That(splines, Is.Not.Empty, "Track25 should contain splines");

    var straightSplines = splines.Where(s => s.Id.Contains("straight", StringComparison.OrdinalIgnoreCase)).ToList();
    var curvedSplines = splines.Where(s => s.Id.Contains("curve", StringComparison.OrdinalIgnoreCase) ||
                                          s.Id.Contains("helix", StringComparison.OrdinalIgnoreCase)).ToList();

    if (straightSplines.Count > 0) {
      var straight = straightSplines.First();
      var segmentSum = straight.SegmentLengths.Sum();
      Assert.That(segmentSum, Is.GreaterThan(0f), "Straight segment lengths should sum to positive value");
      Assert.That(Math.Abs(segmentSum - straight.TotalLength), Is.LessThan(0.01f), "Segment lengths should sum to TotalLength");
    }

    if (curvedSplines.Count > 0) {
      var curved = curvedSplines.First();
      TestContext.WriteLine($"\nAnalyzing curved spline: {curved.Id}");
      TestContext.WriteLine($"  Nodes: {curved.NodeCount}, Cyclic: {curved.Cyclic}");
      TestContext.WriteLine($"  TotalLength: {curved.TotalLength:F3}");

      var deviations = new List<float>();
      var nodeDistances = new List<float>();
      var cpMagnitudes = new List<float>();

      // Validate Segment decoding
      TestContext.WriteLine($"\n  Segment data validation:");
      for (var segIdx = 0; segIdx < curved.Segments.Length; segIdx++) {
        var segment = curved.Segments[segIdx];
        var segmentLength = curved.SegmentLengths[segIdx];
        var distances = segment.GetCumulativeDistances(segmentLength);

        // Check decoded distances
        Assert.That(distances.Length, Is.EqualTo(14), $"Segment {segIdx} should have 14 samples");

        // Sample values should be monotonically increasing
        for (var i = 1; i < distances.Length; i++) {
          Assert.That(distances[i], Is.GreaterThanOrEqualTo(distances[i - 1]),
            $"Segment {segIdx} sample {i} distance not monotonic: {distances[i]} < {distances[i-1]}");
        }

        // Decoded distances should be within segment bounds (with tolerance for rounding)
        Assert.That(distances[0], Is.GreaterThanOrEqualTo(-0.1f),
          $"Segment {segIdx} first sample {distances[0]} should be >= 0");
        Assert.That(distances[13], Is.LessThanOrEqualTo(segmentLength + 0.1f),
          $"Segment {segIdx} last sample {distances[13]} exceeds segment length {segmentLength}");

        TestContext.WriteLine($"    Segment {segIdx}: len={segmentLength:F3}, samples=[{distances[0]:F3}..{distances[13]:F3}]");
      }

      for (var i = 0; i < curved.Nodes.Length - 1; i++) {
        var p0 = curved.Nodes[i];
        var p1 = curved.Nodes[i + 1];
        var cp1Abs = p0 + curved.ControlPoint2[i];
        var cp2Abs = p1 + curved.ControlPoint1[i + 1];

        var lineVec = p1 - p0;
        var lineLen = lineVec.Length();
        nodeDistances.Add(lineLen);
        cpMagnitudes.Add(curved.ControlPoint2[i].Length());
        cpMagnitudes.Add(curved.ControlPoint1[i + 1].Length());

        if (lineLen > 0.01f) {
          var lineDir = lineVec / lineLen;
          var deviationCp1 = (cp1Abs - p0) - (float)System.Numerics.Vector3.Dot(cp1Abs - p0, lineDir) * lineDir;
          var deviationCp2 = (cp2Abs - p0) - (float)System.Numerics.Vector3.Dot(cp2Abs - p0, lineDir) * lineDir;
          deviations.Add(deviationCp1.Length());
          deviations.Add(deviationCp2.Length());
        }
      }

      if (deviations.Count > 0) {
        var maxDev = deviations.Max();
        var avgDev = deviations.Average();
        var maxNodeDist = nodeDistances.Max();
        var avgCpMag = cpMagnitudes.Average();

        TestContext.WriteLine($"  Node spacing: min={nodeDistances.Min():F3}, max={maxNodeDist:F3}, avg={nodeDistances.Average():F3}");
        TestContext.WriteLine($"  Control point magnitude: avg={avgCpMag:F3}, max={cpMagnitudes.Max():F3}");
        TestContext.WriteLine($"  Control point as % of node spacing: {(avgCpMag / maxNodeDist * 100):F1}%");
        TestContext.WriteLine($"  Control point deviation from line:");
        TestContext.WriteLine($"    Max: {maxDev:F4}, Avg: {avgDev:F4}");
        TestContext.WriteLine($"    Relative to total length: max={maxDev / curved.TotalLength:P2}, avg={avgDev / curved.TotalLength:P2}");
        TestContext.WriteLine($"    Relative to node spacing: max={maxDev / maxNodeDist:P2}");
      }

      var hasNontrivialControlPoints = curved.ControlPoint1.Any(cp => Math.Abs(cp.X) > 0.01f || Math.Abs(cp.Y) > 0.01f || Math.Abs(cp.Z) > 0.01f) ||
                                       curved.ControlPoint2.Any(cp => Math.Abs(cp.X) > 0.01f || Math.Abs(cp.Y) > 0.01f || Math.Abs(cp.Z) > 0.01f);
      Assert.That(hasNontrivialControlPoints, Is.True, "Curved splines should have non-trivial control points");
      Assert.That(deviations.Count > 0 && deviations.Any(d => d > 0.001f), Is.True, "Curved splines should deviate from straight lines");

      Assert.That(curved.Segments.Length == curved.SegmentLengths.Length, Is.True, "Number of segments should match segment lengths");
      Assert.That(curved.Segments.Any(seg => seg.Samples.Any(b => b != 0)), Is.True, "Segment data should contain non-zero bytes");
    }
  }
}

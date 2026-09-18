// Spline Decoding Unit Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.IO;
using System.Numerics;
using NUnit.Framework;
using OpenCobra.OVL.Files;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class SplinesTests {
  private static byte[] CreateSyntheticSpline(
    uint nodeCount = 2,
    bool cyclic = false,
    float totalLength = 10.0f,
    float invTotalLength = 0.1f,
    float maxY = 5.0f
  ) {
    var header = new byte[32];
    Buffer.BlockCopy(BitConverter.GetBytes(nodeCount), 0, header, 0, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(cyclic ? 1u : 0u), 0, header, 8, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(totalLength), 0, header, 12, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(invTotalLength), 0, header, 16, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(maxY), 0, header, 28, 4);
    return header;
  }

  private static byte[] CreateSyntheticNodes(int count, float xStep = 10.0f) {
    var nodes = new byte[count * 36];
    for (var i = 0; i < count; i++) {
      var offset = i * 36;
      Buffer.BlockCopy(BitConverter.GetBytes(i * xStep), 0, nodes, offset, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 4, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 8, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(-1.0f), 0, nodes, offset + 12, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 16, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 20, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, nodes, offset + 24, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 28, 4);
      Buffer.BlockCopy(BitConverter.GetBytes(0.0f), 0, nodes, offset + 32, 4);
    }
    return nodes;
  }

  [Test]
  public void ParseSpline_ValidBinary_DecodesAllFieldsCorrectly() {
    var header = CreateSyntheticSpline(nodeCount: 2, cyclic: false, totalLength: 10.0f, maxY: 3.5f);
    var nodes = CreateSyntheticNodes(2, xStep: 10.0f);
    var lengths = BitConverter.GetBytes(10.0f);
    var data = new byte[14];
    data[13] = 255;

    var spline = TrackData.ParseSpline(header, nodes, lengths, data, "test_spline");

    using (Assert.EnterMultipleScope()) {
      Assert.That(spline.Id, Is.EqualTo("test_spline"));
      Assert.That(spline.NodeCount, Is.EqualTo(2u));
      Assert.That(spline.Cyclic, Is.False);
      Assert.That(spline.TotalLength, Is.EqualTo(10.0f));
      Assert.That(spline.MaxY, Is.EqualTo(3.5f));
      Assert.That(spline.Nodes, Has.Length.EqualTo(2));
      Assert.That(spline.Nodes[1].X, Is.EqualTo(10.0f));
      Assert.That(spline.ControlPoint1[0].X, Is.EqualTo(-1.0f));
      Assert.That(spline.ControlPoint2[0].X, Is.EqualTo(1.0f));
      Assert.That(spline.SegmentLengths, Has.Length.EqualTo(1));
      Assert.That(spline.SegmentLengths[0], Is.EqualTo(10.0f));
      Assert.That(spline.Segments, Has.Length.EqualTo(1));
    }
  }

  [Test]
  public void ParseSpline_HeaderTooShort_ThrowsInvalidDataException() {
    var truncated = new byte[20];
    Assert.Throws<InvalidDataException>(new System.Action(() => {
      TrackData.ParseSpline(truncated);
    }));
  }

  [Test]
  public void ParseSpline_NodesTruncated_ThrowsInvalidDataException() {
    var header = CreateSyntheticSpline(nodeCount: 2);
    var truncatedNodes = new byte[35];
    Assert.Throws<InvalidDataException>(new System.Action(() => {
      TrackData.ParseSpline(header, truncatedNodes);
    }));
  }

  [Test]
  public void ParseSpline_NonFiniteCoordinates_ThrowsInvalidDataException() {
    var header = CreateSyntheticSpline(nodeCount: 1);
    var badNodes = new byte[36];
    Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, badNodes, 0, 4);

    Assert.Throws<InvalidDataException>(new System.Action(() => {
      TrackData.ParseSpline(header, badNodes);
    }));
  }

  [Test]
  public void Segment_GetCumulativeDistances_ComputesMonotonicDistances() {
    var segment = new Segment {
      Samples = [0, 20, 50, 80, 100, 120, 140, 160, 180, 200, 220, 240, 250, 255]
    };
    var distances = segment.GetCumulativeDistances(100.0f);

    using (Assert.EnterMultipleScope()) {
      Assert.That(distances, Has.Length.EqualTo(14));
      Assert.That(distances[0], Is.EqualTo(0.0f));
      Assert.That(distances[13], Is.EqualTo(100.0f));
      for (var i = 1; i < distances.Length; i++) {
        Assert.That(distances[i], Is.GreaterThanOrEqualTo(distances[i - 1]));
      }
    }
  }
}

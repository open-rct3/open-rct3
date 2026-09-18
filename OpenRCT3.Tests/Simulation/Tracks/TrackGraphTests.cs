// TrackGraphTests
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenRCT3.Simulation.Tracks;

namespace OpenRCT3.Tests.Simulation.Tracks;

[TestFixture]
public class TrackGraphTests {
  [Test]
  public void Constructor_AcceptsC1ContinuousBranchingDag() {
    var root = new TrackNode("root");
    var junction = new TrackNode("junction");
    var leftExit = new TrackNode("left-exit");
    var rightExit = new TrackNode("right-exit");
    var incoming = Piece(
      Vector3.Zero,
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f)
    );
    var straightBranch = Piece(
      new(10f, 0f, 0f),
      new(20f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f)
    );
    var curvedBranch = Piece(
      new(10f, 0f, 0f),
      new(20f, 5f, 0f),
      new(10f, 0f, 0f),
      new(10f, 5f, 0f)
    );

    var graph = new TrackGraph(
      [root, junction, leftExit, rightExit],
      [
        new("incoming", root, junction, incoming),
        new("straight", junction, leftExit, straightBranch),
        new("curve", junction, rightExit, curvedBranch),
      ]
    );

    Assert.That(graph.Nodes, Has.Count.EqualTo(4));
    Assert.That(graph.Edges, Has.Count.EqualTo(3));
    Assert.That(graph.GetOutgoing(junction), Has.Count.EqualTo(2));
  }

  [Test]
  public void Constructor_RejectsCycles() {
    var first = new TrackNode("first");
    var second = new TrackNode("second");
    var piece = Piece(
      Vector3.Zero,
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f)
    );

    Assert.Throws<ArgumentException>(new Action(() => new TrackGraph(
      [first, second],
      [new("out", first, second, piece), new("back", second, first, piece)]
    )));
  }

  [Test]
  public void Constructor_RejectsNonC1RailJoin() {
    var first = new TrackNode("first");
    var join = new TrackNode("join");
    var last = new TrackNode("last");
    var incoming = Piece(
      Vector3.Zero,
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f)
    );
    var mismatched = Piece(
      new(10f, 0f, 0f),
      new(20f, 1f, 0f),
      new(10f, 1f, 0f),
      new(10f, 1f, 0f)
    );

    Assert.Throws<ArgumentException>(new Action(() => new TrackGraph(
      [first, join, last],
      [new("incoming", first, join, incoming), new("outgoing", join, last, mismatched)]
    )));
  }

  [Test]
  public void Constructor_RejectsTinyOppositeJoinTangents() {
    var first = new TrackNode("first");
    var join = new TrackNode("join");
    var last = new TrackNode("last");
    var tinyTangent = new Vector3(0.00001f, 0f, 0f);
    var joinPosition = new Vector3(0.00001f, 0f, 0f);
    var incoming = Piece(Vector3.Zero, joinPosition, tinyTangent, tinyTangent);
    var outgoing = Piece(joinPosition, Vector3.Zero, -tinyTangent, -tinyTangent);

    Assert.Throws<ArgumentException>(new Action(() => new TrackGraph(
      [first, join, last],
      [new("incoming", first, join, incoming), new("outgoing", join, last, outgoing)]
    )));
  }

  [Test]
  public void ExtremeFiniteBanks_DoNotCorruptSamplesOrBypassJoinValidation() {
    var first = new TrackNode("first");
    var join = new TrackNode("join");
    var last = new TrackNode("last");
    var incoming = Piece(
      Vector3.Zero,
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      float.MaxValue,
      float.MaxValue
    );
    var outgoing = Piece(
      new(10f, 0f, 0f),
      new(20f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      -float.MaxValue,
      -float.MaxValue
    );

    var sample = incoming.SampleRail(RailSide.Left, incoming.Length * 0.5f);

    Assert.That(float.IsFinite(sample.BankRadians), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.X), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.Y), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.Z), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.W), Is.True);
    Assert.Throws<ArgumentException>(new Action(() => new TrackGraph(
      [first, join, last],
      [new("incoming", first, join, incoming), new("outgoing", join, last, outgoing)]
    )));
  }

  [Test]
  public void Constructor_RejectsUnregisteredEdgeEndpoint() {
    var registered = new TrackNode("registered");
    var missing = new TrackNode("missing");
    var piece = Piece(
      Vector3.Zero,
      new(10f, 0f, 0f),
      new(10f, 0f, 0f),
      new(10f, 0f, 0f)
    );

    Assert.Throws<ArgumentException>(new Action(() => new TrackGraph(
      [registered],
      [new("invalid", registered, missing, piece)]
    )));
  }

  private static TrackPiece Piece(
    Vector3 start,
    Vector3 end,
    Vector3 startTangent,
    Vector3 endTangent,
    float startBank = 0f,
    float endBank = 0f
  ) {
    var halfGauge = Vector3.UnitZ * 0.5f;
    return new(TrackPieceGeometry.FromHandAuthored([
      new(0f, start - halfGauge, startTangent, start + halfGauge, startTangent, startBank),
      new(1f, end - halfGauge, endTangent, end + halfGauge, endTangent, endBank),
    ]));
  }
}

// Tracked Ride Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Rides;
using OpenRCT3.Rides.TrackSpline;

namespace OpenRCT3.Tests.Rides;

[TestFixture]
public class TrackedRideTests {
  private class TestRide : TrackedRide { }

  [Test]
  public void Center_EmptyGraph_YieldsNothing() {
    var ride = new TestRide {
      Name = "Test",
      Price = 0m,
      Track = TrackChaining.CreateGraph()
    };

    var centerSamples = ride.Center.ToList();

    Assert.That(centerSamples, Is.Empty);
  }

  [Test]
  public void Center_SingleNode_YieldsNodeSamples() {
    var graph = TrackChaining.CreateGraph();
    var piece = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(piece.LeftRail, piece.RightRail, length: 10f);
    TrackChaining.AddRootPiece(graph, piece);
    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    var ride = new TestRide { Name = "Test", Price = 0m, Track = graph };
    var centerSamples = ride.Center.ToList();

    Assert.That(centerSamples, Is.Not.Empty);
    Assert.That(centerSamples, Has.Count.EqualTo(piece.LeftRail.BakedSamples.Count));
  }

  [Test]
  public void Center_MultipleNodePath_StopsAtEndpoint() {
    var graph = TrackChaining.CreateGraph();

    var straight1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight1.LeftRail, straight1.RightRail, length: 10f);
    var nodeA = TrackChaining.AddRootPiece(graph, straight1);

    var straight2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight2.LeftRail, straight2.RightRail, length: 10f);
    var nodeB = TrackChaining.ChainPiece(graph, nodeA, straight2)!;

    var straight3 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight3.LeftRail, straight3.RightRail, length: 10f);
    TrackChaining.ChainPiece(graph, nodeB, straight3);

    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    var ride = new TestRide { Name = "Test", Price = 0m, Track = graph };
    var centerSamples = ride.Center.ToList();

    var expectedSampleCount =
      straight1.LeftRail.BakedSamples.Count +
      straight2.LeftRail.BakedSamples.Count +
      straight3.LeftRail.BakedSamples.Count;

    Assert.That(centerSamples, Has.Count.EqualTo(expectedSampleCount));
  }

  [Test]
  public void Center_BranchingTrack_OnlyFollowsFirstBranch() {
    var graph = TrackChaining.CreateGraph();

    var root = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(root.LeftRail, root.RightRail, length: 10f);
    var rootNode = TrackChaining.AddRootPiece(graph, root);

    var branch1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(branch1.LeftRail, branch1.RightRail, length: 5f);
    var branch1Node = TrackChaining.ChainPiece(graph, rootNode, branch1);

    var branch2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(branch2.LeftRail, branch2.RightRail, length: 5f);
    TrackChaining.ChainPiece(graph, rootNode, branch2);

    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    var ride = new TestRide { Name = "Test", Price = 0m, Track = graph };
    var centerSamples = ride.Center.ToList();

    var expectedSampleCount =
      root.LeftRail.BakedSamples.Count +
      branch1.LeftRail.BakedSamples.Count;

    Assert.That(centerSamples, Has.Count.EqualTo(expectedSampleCount));
  }

  [Test]
  public void Center_SamplesAreCentered() {
    var graph = TrackChaining.CreateGraph();
    var piece = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(piece.LeftRail, piece.RightRail, length: 10f);
    TrackChaining.AddRootPiece(graph, piece);
    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    var ride = new TestRide { Name = "Test", Price = 0m, Track = graph };
    var centerSamples = ride.Center.ToList();
    var leftSamples = piece.LeftRail.BakedSamples;
    var rightSamples = piece.RightRail.BakedSamples;

    for (int i = 0; i < centerSamples.Count; i++) {
      var center = centerSamples[i];
      var left = leftSamples[i];
      var right = rightSamples[i];

      var expectedX = (left.Position.X + right.Position.X) / 2f;
      var expectedY = (left.Position.Y + right.Position.Y) / 2f;
      var expectedZ = (left.Position.Z + right.Position.Z) / 2f;

      using (Assert.EnterMultipleScope()) {
        Assert.That(center.Position.X, Is.EqualTo(expectedX).Within(0.001f));
        Assert.That(center.Position.Y, Is.EqualTo(expectedY).Within(0.001f));
        Assert.That(center.Position.Z, Is.EqualTo(expectedZ).Within(0.001f));
      }
    }
  }
}

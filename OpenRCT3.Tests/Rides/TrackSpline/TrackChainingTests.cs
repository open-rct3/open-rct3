// Track Chaining Tests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using System.Numerics;
using OpenRCT3.Rides.TrackSpline;

namespace OpenRCT3.Tests.Rides.TrackSpline;

[TestFixture]
public class TrackChainingTests {
  [SetUp]
  public void Setup() {
    ArcLength.ClearCache();
  }

  [Test]
  public void CreateGraph_IsEmpty() {
    var graph = TrackChaining.CreateGraph();
    Assert.That(graph.RootNode, Is.Null);
    Assert.That(graph.NodesById.Count, Is.EqualTo(0));
  }

  [Test]
  public void AddRootPiece_CreatesNode() {
    var graph = TrackChaining.CreateGraph();
    var piece = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(piece.LeftRail, piece.RightRail, length: 10f);

    var node = TrackChaining.AddRootPiece(graph, piece, position: new Vector3(5, 10, 15));

    Assert.That(node, Is.Not.Null);
    Assert.That(graph.RootNode, Is.EqualTo(node));
    Assert.That(graph.NodesById.Count, Is.EqualTo(1));
    Assert.That(piece.Position, Is.EqualTo(new Vector3(5, 10, 15)));
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void ChainPiece_ValidContinuity_CreatesEdge() {
    var graph = TrackChaining.CreateGraph();

    // Root piece: straight
    var straight1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight1.LeftRail, straight1.RightRail, length: 10f);
    var node1 = TrackChaining.AddRootPiece(graph, straight1);

    // Second piece: another straight (aligned tangents)
    var straight2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight2.LeftRail, straight2.RightRail, length: 10f);
    var node2 = TrackChaining.ChainPiece(graph, node1, straight2, validateContinuity: true);

    Assert.That(node2, Is.Not.Null);
    Assert.That(node1.OutgoingEdges.Count, Is.EqualTo(1));
    Assert.That(graph.NodesById.Count, Is.EqualTo(2));
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void BakeGraph_BakesAllPieces() {
    var graph = TrackChaining.CreateGraph();

    var straight1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight1.LeftRail, straight1.RightRail, length: 10f);
    var node1 = TrackChaining.AddRootPiece(graph, straight1);

    var straight2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight2.LeftRail, straight2.RightRail, length: 10f);
    var node2 = TrackChaining.ChainPiece(graph, node1, straight2);

    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    Assert.That(node1.Piece.IsBaked, Is.True);
    Assert.That(node2!.Piece.IsBaked, Is.True);
    Assert.That(node1.Piece.LeftRail.BakedSamples.Count, Is.GreaterThan(0));
    Assert.That(node2.Piece.RightRail.BakedSamples.Count, Is.GreaterThan(0));
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void ChainMultiplePieces_BuildsValidGraph() {
    var graph = TrackChaining.CreateGraph();

    // Chain: straight → curve → straight
    var s1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s1.LeftRail, s1.RightRail, length: 10f);
    var n1 = TrackChaining.AddRootPiece(graph, s1);

    var curve = new TrackPiece { PieceType = TrackPieceType.Curve };
    ProceduralPieces.GenerateCurve(curve.LeftRail, curve.RightRail, radius: 5f, arcAngle: 1.57f);
    var n2 = TrackChaining.ChainPiece(graph, n1, curve, validateContinuity: false); // Disable check for curve
    Assert.That(n2, Is.Not.Null);

    var s2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s2.LeftRail, s2.RightRail, length: 10f);
    var n3 = TrackChaining.ChainPiece(graph, n2!, s2, validateContinuity: false);

    Assert.That(graph.NodesById.Count, Is.EqualTo(3));
    Assert.That(n1.OutgoingEdges.Count, Is.EqualTo(1));
    Assert.That(n2.OutgoingEdges.Count, Is.EqualTo(1));
  }
}

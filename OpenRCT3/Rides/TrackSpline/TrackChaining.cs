// Track Piece Chaining and Graph Construction
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Build a track graph by chaining pieces together with C1 (tangent) continuity validation.
/// Pieces are transformed from local space into world space as they're added to the graph.
/// </summary>
public static class TrackChaining {
  /// <summary>
  /// Create an empty track graph.
  /// </summary>
  public static TrackGraph CreateGraph() => new();

  /// <summary>
  /// Add the first piece (root) to an empty graph at world origin.
  /// </summary>
  public static TrackGraphNode AddRootPiece(
    TrackGraph graph,
    TrackPiece piece,
    Vector3 position = default,
    float heading = 0f,
    float bank = 0f) {
    piece.PieceId = graph.NextPieceId++;
    piece.Position = position;
    piece.Heading = heading;
    piece.Bank = bank;
    piece.IsBaked = false;

    var node = new TrackGraphNode { Piece = piece };
    graph.RootNode = node;
    graph.NodesById[piece.PieceId] = node;

    return node;
  }

  /// <summary>
  /// Chain a new piece onto an existing node. Validates C1 continuity at the boundary.
  /// Returns the new node, or null if continuity check fails.
  /// </summary>
  public static TrackGraphNode? ChainPiece(
    TrackGraph graph,
    TrackGraphNode previousNode,
    TrackPiece newPiece,
    bool validateContinuity = true) {
    var prevPiece = previousNode.Piece;

    // Compute exit point and tangent of previous piece (at t=1 in local space)
    var prevExitPos = GetPieceExitPosition(prevPiece);
    var prevExitTangent = GetPieceExitTangent(prevPiece);

    // New piece's entry position and tangent (at t=0 in local space)
    var newEntryPos = new Vector3(0, 0, 0); // Pieces start at origin in local space
    var newEntryTangent = GetPieceEntryTangent(newPiece);

    // Place new piece at previous piece's exit
    newPiece.Position = prevExitPos;
    newPiece.Heading = (float)Math.Atan2(prevExitTangent.Z, prevExitTangent.X);
    newPiece.Bank = 0f; // TODO: derive from piece geometry

    // Validate C1 continuity: tangents should match direction
    if (validateContinuity) {
      var tangentDot = Vector3.Dot(Vector3.Normalize(prevExitTangent), Vector3.Normalize(newEntryTangent));
      if (tangentDot < 0.99f) {
        // Tangents don't align (C1 continuity broken)
        return null;
      }
    }

    newPiece.PieceId = graph.NextPieceId++;
    newPiece.IsBaked = false;

    var newNode = new TrackGraphNode { Piece = newPiece, IncomingEdge = new() { IsContinuousAtStart = true } };
    var edge = new TrackGraphEdge { TargetNode = newNode, IsContinuousAtStart = true };

    previousNode.OutgoingEdges.Add(edge);
    graph.NodesById[newPiece.PieceId] = newNode;

    return newNode;
  }

  /// <summary>
  /// Bake all pieces in the graph (compute baked samples for all rails).
  /// </summary>
  public static void BakeGraph(TrackGraph graph, bool useTestTolerance = false) {
    foreach (var node in graph.NodesById.Values) {
      SplineBaker.BakeRailSpline(node.Piece.LeftRail, useTestTolerance: useTestTolerance);
      SplineBaker.BakeRailSpline(node.Piece.RightRail, useTestTolerance: useTestTolerance);
      node.Piece.IsBaked = true;
    }
  }

  /// <summary>
  /// Get the exit position of a piece in local space (end of piece).
  /// Approximate by taking the average of the last control point of each rail.
  /// </summary>
  private static Vector3 GetPieceExitPosition(TrackPiece piece) {
    if (piece.LeftRail.ControlPoints.Count == 0 || piece.RightRail.ControlPoints.Count == 0) {
      return piece.Position;
    }

    var leftExit = piece.LeftRail.ControlPoints[^1].Position;
    var rightExit = piece.RightRail.ControlPoints[^1].Position;
    return (leftExit + rightExit) * 0.5f;
  }

  /// <summary>
  /// Get the exit tangent of a piece in local space.
  /// Average the tangents of left and right rails.
  /// </summary>
  private static Vector3 GetPieceExitTangent(TrackPiece piece) {
    if (piece.LeftRail.ControlPoints.Count == 0 || piece.RightRail.ControlPoints.Count == 0) {
      return Vector3.UnitX;
    }

    var leftTangent = piece.LeftRail.ControlPoints[^1].Tangent;
    var rightTangent = piece.RightRail.ControlPoints[^1].Tangent;
    return Vector3.Normalize((leftTangent + rightTangent) * 0.5f);
  }

  /// <summary>
  /// Get the entry tangent of a piece in local space.
  /// Average the tangents of left and right rails at the start.
  /// </summary>
  private static Vector3 GetPieceEntryTangent(TrackPiece piece) {
    if (piece.LeftRail.ControlPoints.Count == 0 || piece.RightRail.ControlPoints.Count == 0) {
      return Vector3.UnitX;
    }

    var leftTangent = piece.LeftRail.ControlPoints[0].Tangent;
    var rightTangent = piece.RightRail.ControlPoints[0].Tangent;
    return Vector3.Normalize((leftTangent + rightTangent) * 0.5f);
  }
}

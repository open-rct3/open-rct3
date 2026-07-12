// Ride Track Spline Data Model
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// A control point on a rail spline: position, tangent direction, and bank rotation.
/// </summary>
public struct RailControlPoint {
  /// <summary>Position in local piece space.</summary>
  public Vector3 Position;

  /// <summary>Tangent vector (unit direction for Catmull-Rom interpolation).</summary>
  public Vector3 Tangent;

  /// <summary>Bank angle, in radians. Rotation about the forward (tangent) axis.</summary>
  public float Bank;
}

/// <summary>
/// A single baked sample along a rail spline: position, orientation, and arc-length coordinate.
/// </summary>
public struct BakedSample {
  /// <summary>Position in world space.</summary>
  public Vector3 Position;

  /// <summary>Orientation quaternion (encodes heading and pitch).</summary>
  public Quaternion Orientation;

  /// <summary>Bank angle at this sample, in radians.</summary>
  public float Bank;

  /// <summary>Arc-length coordinate along the rail from piece start, in world units.</summary>
  public float ArcLength;
}

/// <summary>
/// One side of a track piece's rail spline (left or right).
/// </summary>
public class RailSpline {
  /// <summary>Ordered sequence of control points defining the spline in local piece space.</summary>
  public List<RailControlPoint> ControlPoints { get; set; } = [];

  /// <summary>Baked samples for fast runtime queries. Regenerated when geometry changes.</summary>
  public List<BakedSample> BakedSamples { get; set; } = [];

  /// <summary>Total arc-length of this rail, in world units (cached from last bake).</summary>
  public float TotalArcLength { get; set; }
}

/// <summary>
/// Left/Right rail selector for dual-rail queries.
/// </summary>
public enum RailSide {
  Left = 0,
  Right = 1,
}

/// <summary>
/// A track piece: two independent rail splines, transform, and bake state.
/// Placement in the world is via affine transform; all geometry is authored in local piece space.
/// </summary>
public class TrackPiece {
  /// <summary>Unique identifier within the track graph.</summary>
  public int PieceId { get; set; }

  /// <summary>Type of piece (straight, curve, slope, loop, corkscrew, etc.).</summary>
  public TrackPieceType PieceType { get; set; }

  /// <summary>Left rail spline (local piece space).</summary>
  public RailSpline LeftRail { get; set; } = new();

  /// <summary>Right rail spline (local piece space).</summary>
  public RailSpline RightRail { get; set; } = new();

  /// <summary>Position of piece origin in world space.</summary>
  public Vector3 Position { get; set; }

  /// <summary>Heading angle, in radians (yaw rotation applied to rails when placed).</summary>
  public float Heading { get; set; }

  /// <summary>Bank angle, in radians (roll rotation applied to rails when placed).</summary>
  public float Bank { get; set; }

  /// <summary>True if this piece's rails have been baked; false if geometry was modified and needs rebake.</summary>
  public bool IsBaked { get; set; }

  /// <summary>
  /// For organic (hand-authored) pieces: true if control points are user-overridden.
  /// For procedural pieces: false (geometry is generated from profile curve).
  /// </summary>
  public bool IsOrganic { get; set; }
}

/// <summary>
/// Standard track piece types (extensible enum-like pattern for future custom types).
/// </summary>
public enum TrackPieceType {
  Straight = 0,
  Curve = 1,
  Slope = 2,
  Loop = 3,
  Corkscrew = 4,
  Twist = 5,
  BankedCurve = 6,
  // Future: more exotic piece types
}

/// <summary>
/// A node in the track graph: chains pieces sequentially, validating tangent continuity.
/// </summary>
public class TrackGraphNode {
  /// <summary>The track piece at this node.</summary>
  public TrackPiece Piece { get; set; } = default!;

  /// <summary>List of outgoing edges (for DAG support; typically 1 for linear tracks, >1 for junctions).</summary>
  public List<TrackGraphEdge> OutgoingEdges { get; set; } = [];

  /// <summary>Incoming edge (parent node in the chain; null for root).</summary>
  public TrackGraphEdge? IncomingEdge { get; set; }
}

/// <summary>
/// An edge in the track graph connecting two pieces, with validation of C1 continuity.
/// </summary>
public class TrackGraphEdge {
  /// <summary>The node this edge points to.</summary>
  public TrackGraphNode TargetNode { get; set; } = default!;

  /// <summary>True if tangent continuity (C1) has been validated between this and previous piece.</summary>
  public bool IsContinuousAtStart { get; set; }
}

/// <summary>
/// A complete track graph: the DAG of track pieces chained together.
/// </summary>
public class TrackGraph {
  /// <summary>Root node (first piece in the sequence).</summary>
  public TrackGraphNode? RootNode { get; set; }

  /// <summary>All nodes in the graph, indexed by piece ID.</summary>
  public Dictionary<int, TrackGraphNode> NodesById { get; set; } = [];

  /// <summary>Next available piece ID (incremented as pieces are added).</summary>
  public int NextPieceId { get; set; } = 1;
}

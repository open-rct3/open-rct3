// Track Graph
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace OpenRCT3.Simulation.Tracks;

/// <summary>A junction or boundary in a tracked ride's directed acyclic piece graph.</summary>
public sealed record TrackNode(string Id);

/// <summary>One graph connection carrying a complete, locally addressable track piece.</summary>
public sealed record TrackEdge(string Id, TrackNode From, TrackNode To, TrackPiece Piece);

/// <summary>An immutable, validated DAG of dual-rail track pieces.</summary>
public sealed class TrackGraph {
  private readonly ReadOnlyCollection<TrackNode> nodes;
  private readonly ReadOnlyCollection<TrackEdge> edges;
  private readonly IReadOnlyDictionary<string, ReadOnlyCollection<TrackEdge>> outgoing;

  public IReadOnlyList<TrackNode> Nodes => nodes;
  public IReadOnlyList<TrackEdge> Edges => edges;

  public TrackGraph(
    IEnumerable<TrackNode> nodes,
    IEnumerable<TrackEdge> edges,
    float joinPositionTolerance = 0.001f,
    float joinTangentTolerance = 0.001f,
    float joinBankToleranceRadians = 0.001f
  ) {
    ArgumentNullException.ThrowIfNull(nodes);
    ArgumentNullException.ThrowIfNull(edges);
    ValidateTolerance(joinPositionTolerance, nameof(joinPositionTolerance));
    ValidateTolerance(joinTangentTolerance, nameof(joinTangentTolerance));
    ValidateTolerance(joinBankToleranceRadians, nameof(joinBankToleranceRadians));

    var nodeArray = nodes.ToArray();
    var edgeArray = edges.ToArray();
    var nodesById = BuildNodeIndex(nodeArray);
    ValidateEdges(edgeArray, nodesById);
    ValidateAcyclic(nodeArray, edgeArray);
    ValidateJoins(
      nodeArray,
      edgeArray,
      joinPositionTolerance,
      joinTangentTolerance,
      joinBankToleranceRadians
    );

    this.nodes = Array.AsReadOnly(nodeArray);
    this.edges = Array.AsReadOnly(edgeArray);
    outgoing = nodeArray.ToDictionary(
      node => node.Id,
      node => Array.AsReadOnly(edgeArray.Where(edge => edge.From.Id == node.Id).ToArray())
    );
  }

  public IReadOnlyList<TrackEdge> GetOutgoing(TrackNode node) {
    ArgumentNullException.ThrowIfNull(node);
    if (!outgoing.TryGetValue(node.Id, out var result))
      throw new ArgumentException("The node is not part of this graph.", nameof(node));
    return result;
  }

  private static Dictionary<string, TrackNode> BuildNodeIndex(IEnumerable<TrackNode> nodes) {
    var result = new Dictionary<string, TrackNode>(StringComparer.Ordinal);
    foreach (var node in nodes) {
      if (node is null || string.IsNullOrWhiteSpace(node.Id))
        throw new ArgumentException("Track nodes need non-empty IDs.", nameof(nodes));
      if (!result.TryAdd(node.Id, node))
        throw new ArgumentException($"Duplicate track node ID '{node.Id}'.", nameof(nodes));
    }
    return result;
  }

  private static void ValidateEdges(
    IEnumerable<TrackEdge> edges,
    IReadOnlyDictionary<string, TrackNode> nodesById
  ) {
    var edgeIds = new HashSet<string>(StringComparer.Ordinal);
    foreach (var edge in edges) {
      if (edge is null || string.IsNullOrWhiteSpace(edge.Id))
        throw new ArgumentException("Track edges need non-empty IDs.", nameof(edges));
      if (edge.From is null || edge.To is null || edge.Piece is null)
        throw new ArgumentException("Track edges need endpoints and a piece.", nameof(edges));
      if (!edgeIds.Add(edge.Id))
        throw new ArgumentException($"Duplicate track edge ID '{edge.Id}'.", nameof(edges));
      if (!nodesById.ContainsKey(edge.From.Id) || !nodesById.ContainsKey(edge.To.Id))
        throw new ArgumentException("Every edge endpoint must belong to the graph.", nameof(edges));
      if (edge.From.Id == edge.To.Id)
        throw new ArgumentException("A DAG edge cannot connect a node to itself.", nameof(edges));
    }
  }

  private static void ValidateAcyclic(
    IReadOnlyList<TrackNode> nodes,
    IReadOnlyList<TrackEdge> edges
  ) {
    var indegree = nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);
    var outgoing = nodes.ToDictionary(
      node => node.Id,
      _ => new List<TrackEdge>(),
      StringComparer.Ordinal
    );
    foreach (var edge in edges) {
      indegree[edge.To.Id]++;
      outgoing[edge.From.Id].Add(edge);
    }

    var ready = new Queue<string>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
    var visited = 0;
    while (ready.TryDequeue(out var nodeId)) {
      visited++;
      foreach (var edge in outgoing[nodeId]) {
        indegree[edge.To.Id]--;
        if (indegree[edge.To.Id] == 0) ready.Enqueue(edge.To.Id);
      }
    }

    if (visited != nodes.Count)
      throw new ArgumentException("Track graphs must be acyclic.", nameof(edges));
  }

  private static void ValidateJoins(
    IEnumerable<TrackNode> nodes,
    IReadOnlyList<TrackEdge> edges,
    float positionTolerance,
    float tangentTolerance,
    float bankTolerance
  ) {
    foreach (var node in nodes) {
      var endpoints = edges
        .Where(edge => edge.From.Id == node.Id || edge.To.Id == node.Id)
        .Select(edge => edge.From.Id == node.Id ? edge.Piece.Entry : edge.Piece.Exit)
        .ToArray();
      if (endpoints.Length < 2) continue;

      var expected = endpoints[0];
      foreach (var actual in endpoints.Skip(1))
        ValidateJoin(expected, actual, positionTolerance, tangentTolerance, bankTolerance, node.Id);
    }
  }

  private static void ValidateJoin(
    TrackPieceEndpoint expected,
    TrackPieceEndpoint actual,
    float positionTolerance,
    float tangentTolerance,
    float bankTolerance,
    string nodeId
  ) {
    ValidateRailJoin(
      expected.Left,
      actual.Left,
      positionTolerance,
      tangentTolerance,
      bankTolerance,
      nodeId,
      RailSide.Left
    );
    ValidateRailJoin(
      expected.Right,
      actual.Right,
      positionTolerance,
      tangentTolerance,
      bankTolerance,
      nodeId,
      RailSide.Right
    );
  }

  private static void ValidateRailJoin(
    RailEndpoint expected,
    RailEndpoint actual,
    float positionTolerance,
    float tangentTolerance,
    float bankTolerance,
    string nodeId,
    RailSide side
  ) {
    var expectedMagnitude = TrackMath.Length(expected.Tangent);
    var actualMagnitude = TrackMath.Length(actual.Tangent);
    var directionDifference = TrackMath.Distance(
      TrackMath.Normalize(expected.Tangent),
      TrackMath.Normalize(actual.Tangent)
    );
    var magnitudeScale = Math.Max(expectedMagnitude, actualMagnitude);
    var magnitudeDifference = Math.Abs(expectedMagnitude - actualMagnitude) / magnitudeScale;

    if (TrackMath.Distance(expected.Position, actual.Position) > positionTolerance
        || directionDifference > tangentTolerance
        || magnitudeDifference > tangentTolerance
        || MathF.Abs(TrackMath.ShortestAngleDelta(
          expected.BankRadians,
          actual.BankRadians
        )) > bankTolerance)
      throw new ArgumentException(
        $"Track pieces do not form a C1-continuous {side} rail join at node '{nodeId}'."
      );
  }

  private static void ValidateTolerance(float tolerance, string parameterName) {
    if (!float.IsFinite(tolerance) || tolerance < 0f)
      throw new ArgumentOutOfRangeException(parameterName);
  }
}

// Tracked Ride Base Class
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Rides.TrackSpline;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace OpenRCT3.Rides;

/// <summary>
/// Base class for rides that operate on a track graph (coasters, railways, etc.).
/// </summary>
public abstract class TrackedRide : Ride {
  /// <summary>Track, traversible by this ride's trains.</summary>
  public required TrackGraph Track { get; set; }

  /// <summary>Total length of this ride, in meters.</summary>
  public float Length {
    get {
      if (Track.RootNode == null) return 0f;

      var totalLength = 0f;
      var current = Track.RootNode;
      var visited = new HashSet<int>();

      while (current != null) {
        if (!visited.Add(current.Piece.PieceId)) break;

        var left = current.Piece.LeftRail;
        var right = current.Piece.RightRail;
        totalLength += (left.TotalArcLength + right.TotalArcLength) / 2f;

        current = current.OutgoingEdges.FirstOrDefault()?.TargetNode;
      }

      return totalLength;
    }
  }

  /// <summary>Maximum height of this ride, in meters.</summary>
  public float MaxHeight {
    get {
      var maxHeight = 0f;
      foreach (var node in Track.NodesById.Values) {
        var left = node.Piece.LeftRail;
        var right = node.Piece.RightRail;

        foreach (var sample in left.BakedSamples) {
          maxHeight = Math.Max(maxHeight, sample.Position.Y);
        }
        foreach (var sample in right.BakedSamples) {
          maxHeight = Math.Max(maxHeight, sample.Position.Y);
        }
      }
      return maxHeight;
    }
  }

  /// <summary>Samples a computed center-line from first node to first endpoint, in a depth-first traversal.</summary>
  /// <remarks>
  /// A track's center is the average of its left and right rail's at each point along the track.
  /// </remarks>
  public IEnumerable<BakedSample> Center {
    get {
      if (Track.RootNode == null) yield break;

      IEnumerable<BakedSample> TraverseCenterline(TrackGraphNode node) {
        var left = node.Piece.LeftRail.BakedSamples;
        var right = node.Piece.RightRail.BakedSamples;

        for (int i = 0; i < left.Count; i++) {
          var leftSample = left[i];
          var rightSample = right[i];

          yield return new BakedSample {
            Position = (leftSample.Position + rightSample.Position) / 2f,
            Orientation = Quaternion.Lerp(leftSample.Orientation, rightSample.Orientation, 0.5f),
            Bank = (leftSample.Bank + rightSample.Bank) / 2f,
            ArcLength = (leftSample.ArcLength + rightSample.ArcLength) / 2f
          };
        }

        if (node.OutgoingEdges.Count > 0) {
          foreach (var sample in TraverseCenterline(node.OutgoingEdges[0].TargetNode)) {
            yield return sample;
          }
        }
      }

      foreach (var sample in TraverseCenterline(Track.RootNode)) {
        yield return sample;
      }
    }
  }
}

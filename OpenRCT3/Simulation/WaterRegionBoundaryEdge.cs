// Water Region Boundary Edge
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// One outward-facing edge of a tile in a traced water region.
/// </summary>
/// <param name="X">The water tile's X index in the OOB-inclusive grid.</param>
/// <param name="Y">The water tile's Y index in the OOB-inclusive grid.</param>
/// <param name="Edge">The side whose neighboring tile is outside the traced region.</param>
public readonly record struct WaterRegionBoundaryEdge(int X, int Y, Edge Edge);

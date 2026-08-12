// Coaster Ride Class
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Rides;

/// <summary>
/// A roller coaster ride: tracks inversions and advanced mechanics.
/// </summary>
public class Coaster : TrackedRide {
  /// <summary>Total number of inversions of this ride's track.</summary>
  public ushort Inversions {
    get {
      // Derive from the rail splines and 3D trigonometry along the whole track's length.
      // Deferred to full track geometry analysis phase after rendering foundation is complete.
      throw new NotImplementedException("Inversion detection deferred to track analysis phase.");
    }
  }
}

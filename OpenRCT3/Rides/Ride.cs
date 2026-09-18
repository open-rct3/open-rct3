// Ride Base Class
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Rides;

/// <summary>
/// Base class for all ride types in the park.
/// </summary>
public abstract class Ride {
  /// <summary>Player-facing name of this ride.</summary>
  public required string Name { get; set; }

  /// <summary>Amount guests pay before entering this ride's queue.</summary>
  public required decimal Price { get; set; }
}

// Size
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Numerics;

/// <summary>
/// Represents a two-dimensional (2D) size.
/// </summary>
public readonly record struct Size(uint Width, uint Height) {
  public Size(int width, int height) : this((uint)width, (uint)height) { }
}
/// <summary>
/// Represents a two-dimensional (2D) size, of unit <typeparamref name="T"/>.
/// </summary>
public readonly record struct Size<T>(T Width, T Height) where T : unmanaged;

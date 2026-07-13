// Axis-Aligned Bounding Box (AABB)
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.OVL;

/// <summary>
/// Represents an axis-aligned bounding box in 3D space.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BoundingBox(Vector3 min, Vector3 max) {
  [Category("Data")]
  public Vector3 Min = min;
  [Category("Data")]
  public Vector3 Max = max;

  public static BoundingBox Empty => new(Vector3.Zero, Vector3.Zero);
  public readonly Vector3 Center => (Min + Max) / 2;
}

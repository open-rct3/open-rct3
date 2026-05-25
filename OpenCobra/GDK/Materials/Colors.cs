// Common Colors
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenCobra.GDK.Materials;

public static class Colors {
  public static readonly Vector4 Transparent = default;
  public static readonly Vector4 Black = Vector4.Zero with { W = 1 };
  public static readonly Vector4 White = Vector4.One;
  public static readonly Vector4 Red = Vector4.Zero with { X = 1, W = 1 };
  public static readonly Vector4 Green = Vector4.Zero with { Y = 1, W = 1 };
  public static readonly Vector4 Blue = Vector4.Zero with { Z = 1, W = 1 };
  public static readonly Vector4 Cyan = Vector4.One with { X = 0 };
  public static readonly Vector4 Magenta = Vector4.One with { Y = 0 };
  public static readonly Vector4 Yellow = Vector4.One with { Z = 0 };
}

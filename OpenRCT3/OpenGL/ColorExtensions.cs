// ColorExtensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Drawing;
using System.Numerics;

namespace OpenRCT3.OpenGL;

public static class ColorExtensions {
  public static Vector4 ToGl(this Color color) =>
    new(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f);
}

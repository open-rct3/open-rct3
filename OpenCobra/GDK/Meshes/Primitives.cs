// Mesh Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Materials;
using System.Numerics;

namespace OpenCobra.GDK.Meshes;

public static class Primitives {
  /// <summary>
  /// Create a flat quad on the XY plane (Z-up).
  /// </summary>
  /// <param name="name">Name assigned to <see cref="Mesh.Name"/></param>
  /// <param name="color">
  /// Vertex color assigned to <see cref="Vertex.Color"/>. Defaults to <see cref="Colors.Transparent"/>.
  /// </param>
  public static Mesh Plane(string? name = null, Vector4 color = default) => new([
    new Vertex { Position = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0), Color = color },
    new Vertex { Position = new Vector3( 1, -1, 0), TexCoord = new Vector2(1, 0), Color = color },
    new Vertex { Position = new Vector3( 1,  1, 0), TexCoord = new Vector2(1, 1), Color = color },
    new Vertex { Position = new Vector3(-1,  1, 0), TexCoord = new Vector2(0, 1), Color = color }
  ], [0, 1, 2, 0, 2, 3]) { Name = name };
}

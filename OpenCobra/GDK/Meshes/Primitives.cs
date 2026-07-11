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

  /// <summary>
  /// Create an axis-aligned unit cube (extents -1..1 on each axis, Z-up), one flat-shaded quad per face.
  /// </summary>
  /// <param name="name">Name assigned to <see cref="Mesh.Name"/></param>
  /// <param name="color">
  /// Vertex color assigned to <see cref="Vertex.Color"/>. Defaults to <see cref="Colors.Transparent"/>.
  /// </param>
  public static Mesh Cube(string? name = null, Vector4 color = default) {
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    void Face(Vector3 normal, Vector3 right, Vector3 up) {
      var baseIndex = (uint)vertices.Count;
      vertices.Add(new Vertex { Position = normal - right - up, Normal = normal, TexCoord = new Vector2(0, 0), Color = color });
      vertices.Add(new Vertex { Position = normal + right - up, Normal = normal, TexCoord = new Vector2(1, 0), Color = color });
      vertices.Add(new Vertex { Position = normal + right + up, Normal = normal, TexCoord = new Vector2(1, 1), Color = color });
      vertices.Add(new Vertex { Position = normal - right + up, Normal = normal, TexCoord = new Vector2(0, 1), Color = color });
      indices.AddRange([baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3]);
    }

    Face(Vector3.UnitX, -Vector3.UnitY, Vector3.UnitZ);
    Face(-Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
    Face(Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ);
    Face(-Vector3.UnitY, -Vector3.UnitX, Vector3.UnitZ);
    Face(Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);
    Face(-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY);

    return new Mesh(vertices, indices) { Name = name };
  }
}

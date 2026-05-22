// Mesh
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using System.Numerics;

namespace OpenCobra.GDK.Meshes;

/// <summary>
/// Stores geometry data for rendering: vertices, indices, and an optional
/// bounding box.
/// </summary>
/// <remarks>
/// Primitives follow the <abbr title="Counter-Clockwise">CCW</abbr> winding convention, matching the industry standard.
/// Counter-clockwise is the default OpenGL and Direct3D front-face rule.
/// </remarks>
public class Mesh {
  [Category("Data")]
  public List<Vertex> Vertices { get; init; } = [];
  [Category("Data")]
  public List<uint> Indices { get; init; } = [];
  [Category("Data")]
  public BoundingBox? BoundingBox { get; set; }

  public void ComputeBoundingBox() {
    if (Vertices.Count == 0) {
      BoundingBox = null;
      return;
    }

    var min = Vertices[0].Position;
    var max = Vertices[0].Position;

    foreach (var v in Vertices) {
      min = Vector3.Min(min, v.Position);
      max = Vector3.Max(max, v.Position);
    }

    BoundingBox = new BoundingBox(min, max);
  }
}

public struct Vertex {
  [Category("Data")]
  public Vector3 Position;
  [Category("Data")]
  public Vector3 Normal;
  [Category("Data")]
  public Vector2 TexCoord;
  [Category("Appearance")]
  public Vector4 Color;
}

public struct BoundingBox(Vector3 min, Vector3 max) {
  [Category("Data")]
  public Vector3 Min = min;
  [Category("Data")]
  public Vector3 Max = max;
}

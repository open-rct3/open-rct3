// Vertex
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.GDK.Meshes;

/// <summary>
/// Represents a 3D mesh vertex.
/// </summary>
[Category("GPU")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
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

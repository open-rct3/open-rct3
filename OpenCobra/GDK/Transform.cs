// Transform
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenCobra.GDK.Shaders;
using Silk.NET.OpenGL;

namespace OpenCobra.GDK;

public class Transform : Uniform {
  public static readonly string UniformName = "u_Model";
  public new readonly string Name = UniformName;

  public Transform() => Type = UniformType.FloatMat4;

  public Matrix4x4 Matrix { get; set; } = Matrix4x4.Identity;

  public override object? Value {
    get => Matrix;
    set {
      if (value is Matrix4x4 m) Matrix = m;
    }
  }
}

// Transform
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenCobra.GDK.Shaders;

namespace OpenCobra.GDK;

public class Transform : Uniform {
  public Transform() {
    Name = "u_Model";
    Type = UniformType.Mat4;
  }

  public Matrix4x4 Matrix { get; set; } = Matrix4x4.Identity;

  public override object? Value {
    get => Matrix;
    set {
      if (value is Matrix4x4 m) Matrix = m;
    }
  }
}

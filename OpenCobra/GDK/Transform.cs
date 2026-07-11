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

  /// <summary>Appends a translation by <paramref name="offset"/> to this transform.</summary>
  public void Translate(Vector3 offset) => Matrix *= Matrix4x4.CreateTranslation(offset);

  /// <summary>Appends a translation by (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) to this transform.</summary>
  public void Translate(float x, float y, float z) => Translate(new Vector3(x, y, z));

  /// <summary>Appends a rotation of <paramref name="degrees"/> around <paramref name="axis"/> to this transform.</summary>
  public void Rotate(Vector3 axis, float degrees) =>
    Matrix *= Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), degrees * MathF.PI / 180f);

  /// <summary>Appends a rotation of <paramref name="degrees"/> around the X axis to this transform.</summary>
  public void RotateX(float degrees) => Matrix *= Matrix4x4.CreateRotationX(degrees * MathF.PI / 180f);

  /// <summary>Appends a rotation of <paramref name="degrees"/> around the Y axis to this transform.</summary>
  public void RotateY(float degrees) => Matrix *= Matrix4x4.CreateRotationY(degrees * MathF.PI / 180f);

  /// <summary>Appends a rotation of <paramref name="degrees"/> around the Z axis to this transform.</summary>
  public void RotateZ(float degrees) => Matrix *= Matrix4x4.CreateRotationZ(degrees * MathF.PI / 180f);
}

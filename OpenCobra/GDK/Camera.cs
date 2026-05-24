// Camera
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Shaders;
using System.Numerics;

namespace OpenCobra.GDK;

public class Camera : Uniform<Matrix4x4> {
  public static readonly string UniformName = "u_ViewProj";
  public new readonly string Name = UniformName;

  public Camera() => Value = Matrix4x4.Identity;

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void Update(float aspectRatio) {
    // Looking at the origin from the South-East, with Z as Up
    var view = Matrix4x4.CreateLookAt(new Vector3(20, -20, 15), new Vector3(0, 0, 0), Vector3.UnitZ);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspectRatio, 0.1f, 1000f);

    Value = projection * view;
  }
}

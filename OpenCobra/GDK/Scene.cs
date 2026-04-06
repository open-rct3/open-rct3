// Scene
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Numerics;
using OpenCobra.GDK.Meshes;
using OpenCobra.GDK.Shaders;

namespace OpenCobra.GDK;

public class Scene {
  public Model Model { get; set; } = new();

  public Scene() {
    // Create a flat quad
    Model.Mesh.Vertices.AddRange([
      new Vertex { Position = new Vector3(-10, 0, -10), Color = new Vector4(0, 0.5f, 0, 1) },
      new Vertex { Position = new Vector3( 10, 0, -10), Color = new Vector4(0, 0.5f, 0, 1) },
      new Vertex { Position = new Vector3( 10, 0,  10), Color = new Vector4(0, 0.5f, 0, 1) },
      new Vertex { Position = new Vector3(-10, 0,  10), Color = new Vector4(0, 0.5f, 0, 1) }
    ]);
    Model.Mesh.Indices.AddRange([0, 1, 2, 0, 2, 3]);
    Model.Mesh.ComputeBoundingBox();

    Model.Shader.VertexSource = @"#version 120
attribute vec3 a_Position;
attribute vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_View;
uniform mat4 u_Projection;

varying vec4 v_Color;

void main() {
    gl_Position = u_Projection * u_View * u_Model * vec4(a_Position, 1.0);
    v_Color = a_Color;
}";
    Model.Shader.FragmentSource = @"#version 120
varying vec4 v_Color;

void main() {
    gl_FragColor = v_Color;
}";

    Model.Shader.Uniforms.Add(Model.Transform);
    Model.Shader.Uniforms.Add(new Uniform { Name = "u_View", Type = UniformType.Mat4, Value = Matrix4x4.Identity });
    Model.Shader.Uniforms.Add(new Uniform { Name = "u_Projection", Type = UniformType.Mat4, Value = Matrix4x4.Identity });

    UpdateCamera(1.0f);
  }

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void UpdateCamera(float aspectRatio) {
    var view = Matrix4x4.CreateLookAt(new Vector3(0, 15, 20), new Vector3(0, 0, 0), Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspectRatio, 0.1f, 1000f);

    foreach (var uniform in Model.Shader.Uniforms) {
      if (uniform.Name == "u_View") uniform.Value = view;
      if (uniform.Name == "u_Projection") uniform.Value = projection;
    }
  }
}

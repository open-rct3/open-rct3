// Scene
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenCobra.GDK.Meshes;
using OpenCobra.GDK.Shaders;
using OpenCobra.GDK.Assets;

namespace OpenCobra.GDK;

public class Scene {
  public Model Model { get; set; } = new();

  public Scene() {
    // Create a flat quad on the XY plane (Z-up)
    Model.Mesh.Vertices.AddRange([
      new Vertex { Position = new Vector3(-10, -10, 0), TexCoord = new Vector2(0, 0), Color = new Vector4(1, 1, 1, 1) },
      new Vertex { Position = new Vector3( 10, -10, 0), TexCoord = new Vector2(1, 0), Color = new Vector4(1, 1, 1, 1) },
      new Vertex { Position = new Vector3( 10,  10, 0), TexCoord = new Vector2(1, 1), Color = new Vector4(1, 1, 1, 1) },
      new Vertex { Position = new Vector3(-10,  10, 0), TexCoord = new Vector2(0, 1), Color = new Vector4(1, 1, 1, 1) }
    ]);
    Model.Mesh.Indices.AddRange([0, 1, 2, 0, 2, 3]);
    Model.Mesh.ComputeBoundingBox();

    Model.Shader.VertexSource = @"#version 120
attribute vec3 a_Position;
attribute vec2 a_TexCoord;
attribute vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_View;
uniform mat4 u_Projection;

varying vec2 v_TexCoord;
varying vec4 v_Color;

void main() {
    gl_Position = u_Projection * u_View * u_Model * vec4(a_Position, 1.0);
    v_TexCoord = v_TexCoord; // FIXME: Flip Y if needed for texture orientation
    v_TexCoord = a_TexCoord;
    v_Color = a_Color;
}";
    Model.Shader.FragmentSource = @"#version 120
uniform sampler2D u_Texture;
varying vec2 v_TexCoord;
varying vec4 v_Color;

void main() {
    vec4 texColor = texture2D(u_Texture, v_TexCoord);
    gl_FragColor = texColor * v_Color;
}";

    Model.Shader.Uniforms.Add(Model.Transform);
    Model.Shader.Uniforms.Add(new Uniform { Name = "u_View", Type = UniformType.Mat4, Value = Matrix4x4.Identity });
    Model.Shader.Uniforms.Add(new Uniform { Name = "u_Projection", Type = UniformType.Mat4, Value = Matrix4x4.Identity });

    UpdateCamera(1.0f);
  }

  public void LoadTexture(string path, string name) {
    if (File.Exists(path)) {
      Model.Material.AlbedoTexture = TextureLoader.LoadFlexiTexture(path, name);
    }
  }

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void UpdateCamera(float aspectRatio) {
    // Looking at the origin from the South-East, with Z as Up
    var view = Matrix4x4.CreateLookAt(new Vector3(20, -20, 15), new Vector3(0, 0, 0), Vector3.UnitZ);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspectRatio, 0.1f, 1000f);

    foreach (var uniform in Model.Shader.Uniforms) {
      if (uniform.Name == "u_View") uniform.Value = view;
      if (uniform.Name == "u_Projection") uniform.Value = projection;
    }
  }
}

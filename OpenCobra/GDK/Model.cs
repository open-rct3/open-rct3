// Model
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Meshes;
using OpenCobra.GDK.Shaders;

namespace OpenCobra.GDK;

public class Model {
  public Mesh Mesh { get; set; } = new();
  public Material Material { get; set; } = new();
  public Transform Transform { get; set; } = new();
  public ShaderProgram Shader { get; set; } = new();
}

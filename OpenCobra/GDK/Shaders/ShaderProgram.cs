// ShaderProgram
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;

namespace OpenCobra.GDK.Shaders;

public class ShaderProgram {
  [Category("Data")]
  public string VertexSource { get; init; } = string.Empty;
  [Category("Data")]
  public string FragmentSource { get; init; } = string.Empty;
  [Category("Data")]
  public List<Uniform> Uniforms { get; init; } = [];
  [Category("Data")]
  public List<Attribute> Attributes { get; init; } = [];
}

public class Uniform {
  [Category("Design")]
  public string Name { get; init; } = string.Empty;
  [Category("Data")]
  public UniformType Type { get; init; }
  [Category("Data")]
  public object? Value { get; set; }
}

public enum UniformType {
  Float,
  Vec2,
  Vec3,
  Vec4,
  Mat3,
  Mat4,
  Int,
  Sampler2D
}

public class Attribute {
  [Category("Design")]
  public string Name { get; init; } = string.Empty;
  [Category("Data")]
  public AttributeType Type { get; init; }
  [Category("Data")]
  public int Location { get; set; }
}

public enum AttributeType {
  Float,
  Vec2,
  Vec3,
  Vec4
}

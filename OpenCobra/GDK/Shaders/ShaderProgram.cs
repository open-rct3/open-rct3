// ShaderProgram
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using Silk.NET.OpenGL;

namespace OpenCobra.GDK.Shaders;

public record struct ShaderSource(string Vertex, string Fragment);

public class ShaderProgram(uint shader) {
  public Shader Shader { get; init; } = new(shader);

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
  public virtual object? Value { get; set; }
}

public class Uniform<T> : Uniform where T : struct {
  public new T? Value {
    get => base.Value as T?;
    set => base.Value = value;
  }
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

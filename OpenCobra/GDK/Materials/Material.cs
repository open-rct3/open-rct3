// Material
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved

using DryIoc;
using OpenCobra.GDK.Services;
using OpenCobra.GDK.Shaders;
using System.ComponentModel;

namespace OpenCobra.GDK.Materials;

public abstract class Material : IResource, IDisposable {
  // FIXME: Inline this into `Material.State`.
  private bool disposed;

  [Category("GPU")]
  public ShaderSource Shaders { get; protected set; }

  [Category("Appearance")]
  public Texture? AlbedoTexture { get; set; }
  [Category("Appearance")]
  public Texture? NormalMap { get; set; }
  [Category("Appearance")]
  public Texture? SpecularMap { get; set; }
  [Category("Appearance")]
  public Texture? EmissiveMap { get; set; }

  public IEnumerable<Texture> Textures {
    get {
      Texture?[] textures = [AlbedoTexture, NormalMap, SpecularMap, EmissiveMap];
      return textures.Where(t => t != null).Cast<Texture>();
    }
  }

  [Category("GPU")]
  public State State {
    get {
      if (disposed) return State.Disposed;

      var textures = Textures.ToArray();
      if (textures.Length == 0) return State.Ready;
      else if (textures.Any(t => t.State != State.Ready)) return State.Uninitialized;
      else return State.Ready;
    }
  }

  public void Dispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);

    var gl = Scene.IoC.Resolve<IGL>().Context;
    // FIXME: Dispose of shader sources
    Texture?[] textures = [AlbedoTexture, NormalMap, SpecularMap, EmissiveMap];
    foreach (var texture in textures.Where(t => t != null)) {
      Debug.Assert(texture != null);
      gl.DeleteTexture(texture.Handle);
      texture.Dispose();
    }

    disposed = true;
  }
}

public class Flat : Material {
  public Flat() {
    var vertexSource = @"#version 120
attribute vec3 a_Position;
attribute vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_ViewProj;

varying vec4 v_Color;

void main() {
    gl_Position = u_ViewProj * u_Model * vec4(a_Position, 1.0);
    v_Color = a_Color;
}";
    var fragmentSource = @"#version 120
varying vec4 v_Color;

void main() {
    gl_FragColor = v_Color;
}";

    Shaders = new(vertexSource, fragmentSource);
  }
}

public class Textured : Material {
  public Textured() {
    var vertexSource = @"#version 120
attribute vec3 a_Position;
attribute vec2 a_TexCoord;
attribute vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_ViewProj;

varying vec2 v_TexCoord;
varying vec4 v_Color;

void main() {
    gl_Position = u_ViewProj * u_Model * vec4(a_Position, 1.0);
    v_TexCoord = v_TexCoord; // FIXME: Flip Y if needed for texture orientation
    v_TexCoord = a_TexCoord;
    v_Color = a_Color;
}";
    var fragmentSource = @"#version 120
uniform sampler2D u_Texture;
varying vec2 v_TexCoord;
varying vec4 v_Color;

void main() {
    vec4 texColor = texture2D(u_Texture, v_TexCoord);
    gl_FragColor = texColor * v_Color;
}";

    Shaders = new(vertexSource, fragmentSource);
  }
}

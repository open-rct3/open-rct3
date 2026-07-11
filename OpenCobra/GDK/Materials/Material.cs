// Material
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Shaders;
using Silk.NET.OpenGL;
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

    var gl = IGame.IoC.Resolve<GL>();
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
    // Core-profile GLSL matching SurfaceSettings' CoreProfileBit | ForwardCompatibleBit context: no
    // `attribute`/`varying`/`gl_FragColor`, all removed from core profile. Mixing #version 120
    // compatibility syntax with a forward-compatible core context is driver-dependent — some drivers
    // compile it without error but silently fail to wire up the deprecated built-ins (notably
    // gl_FragColor), which was rendering every fragment black regardless of vertex color.
    var vertexSource = @"#version 410 core
in vec3 a_Position;
in vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_ViewProj;

out vec4 v_Color;

void main() {
    gl_Position = u_ViewProj * u_Model * vec4(a_Position, 1.0);
    v_Color = a_Color;
}";
    var fragmentSource = @"#version 410 core
in vec4 v_Color;

out vec4 FragColor;

void main() {
    FragColor = v_Color;
}";

    Shaders = new(vertexSource, fragmentSource);
  }
}

public class Textured : Material {
  public Textured() {
    var vertexSource = @"#version 410 core
in vec3 a_Position;
in vec2 a_TexCoord;
in vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_ViewProj;

out vec2 v_TexCoord;
out vec4 v_Color;

void main() {
    gl_Position = u_ViewProj * u_Model * vec4(a_Position, 1.0);
    v_TexCoord = a_TexCoord; // FIXME: Flip Y if needed for texture orientation
    v_Color = a_Color;
}";
    var fragmentSource = @"#version 410 core
uniform sampler2D u_Texture;
in vec2 v_TexCoord;
in vec4 v_Color; // tint multiplier over the sampled texture, not a lighting term

out vec4 FragColor;

void main() {
    vec4 texColor = texture(u_Texture, v_TexCoord);
    FragColor = texColor * v_Color;
}";

    Shaders = new(vertexSource, fragmentSource);
  }
}

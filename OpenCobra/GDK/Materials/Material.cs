// Material
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved

using OpenCobra.GDK.Shaders;
using System.ComponentModel;

namespace OpenCobra.GDK.Materials;

public abstract class Material : IResource, IDisposable {
  // FIXME: Inline this into `Material.State`.
  private bool disposed;
  private Texture? albedoTexture;
  private Texture? normalMap;
  private Texture? specularMap;
  private Texture? emissiveMap;
  private IDisposable? albedoLease;
  private IDisposable? normalLease;
  private IDisposable? specularLease;
  private IDisposable? emissiveLease;

  [Category("GPU")]
  public ShaderSource Shaders { get; protected set; }

  [Browsable(false)]
  public MaterialCacheKey CacheKey => new(Shaders);

  [Category("Appearance")]
  public Texture? AlbedoTexture {
    get => albedoTexture;
    set => SetTexture(ref albedoTexture, ref albedoLease, value);
  }
  [Category("Appearance")]
  public Texture? NormalMap {
    get => normalMap;
    set => SetTexture(ref normalMap, ref normalLease, value);
  }
  [Category("Appearance")]
  public Texture? SpecularMap {
    get => specularMap;
    set => SetTexture(ref specularMap, ref specularLease, value);
  }
  [Category("Appearance")]
  public Texture? EmissiveMap {
    get => emissiveMap;
    set => SetTexture(ref emissiveMap, ref emissiveLease, value);
  }

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
    disposed = true;
    GC.SuppressFinalize(this);

    // FIXME: Dispose of shader sources
    IDisposable?[] leases = [albedoLease, normalLease, specularLease, emissiveLease];
    foreach (var lease in leases.Where(lease => lease != null).Cast<IDisposable>())
      lease.Dispose();
  }

  private void SetTexture(ref Texture? field, ref IDisposable? lease, Texture? value) {
    ObjectDisposedException.ThrowIf(disposed, this);
    if (ReferenceEquals(field, value)) return;

    var replacementLease = value?.AcquireLease();
    lease?.Dispose();
    field = value;
    lease = replacementLease;
  }
}

public readonly record struct MaterialCacheKey(ShaderSource Shaders);

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
in vec3 a_Normal;
in vec2 a_TexCoord;
in vec4 a_Color;

uniform mat4 u_Model;
uniform mat4 u_ViewProj;

out vec2 v_TexCoord;
out vec4 v_Color;
out float v_Light;

void main() {
    gl_Position = u_ViewProj * u_Model * vec4(a_Position, 1.0);
    v_TexCoord = a_TexCoord; // FIXME: Flip Y if needed for texture orientation
    v_Color = a_Color;
    vec3 transformedNormal = mat3(transpose(inverse(u_Model))) * a_Normal;
    float normalLength = length(transformedNormal);
    vec3 worldNormal = normalLength > 0.0001
        ? transformedNormal / normalLength
        : vec3(0.0, 0.0, 1.0);
    vec3 lightDirection = normalize(vec3(-0.35, -0.45, 0.82));
    float diffuse = max(dot(worldNormal, lightDirection), 0.0);
    v_Light = 0.45 + (0.55 * diffuse);
}";
    var fragmentSource = @"#version 410 core
uniform sampler2D u_Texture;
in vec2 v_TexCoord;
in vec4 v_Color; // tint multiplier over the sampled texture, not a lighting term
in float v_Light;

out vec4 FragColor;

void main() {
    vec4 texColor = texture(u_Texture, v_TexCoord);
    FragColor = vec4(texColor.rgb * v_Color.rgb * v_Light, texColor.a * v_Color.a);
}";

    Shaders = new(vertexSource, fragmentSource);
  }
}

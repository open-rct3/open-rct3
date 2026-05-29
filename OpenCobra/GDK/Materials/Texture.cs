// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Platform;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections;
using System.ComponentModel;

namespace OpenCobra.GDK.Materials;

public class Texture(string name, int width, int height, Image<Rgba32> texture, Recolorable recolorable = 0) : IResource, IDisposable {
  public static readonly string UniformName = "u_Texture";
  private bool disposed;

  [Category("Design")]
  public string Name { get; private set; } = name;
  [Category("Appearance")]
  public int Width { get; } = width;
  [Category("Appearance")]
  public int Height { get; } = height;
  [Category("Appearance")]
  public Recolorable Recolorable { get; } = recolorable;
  [Category("Appearance")]
  public Image<Rgba32> Pixels { get; } = texture;

  /// <summary>
  /// Whether this texture is recolorable.
  /// </summary>
  [Category("Appearance")]
  public bool IsRecolorable => Recolorable != Recolorable.None;

  [Category("GPU")]
  public State State => disposed
    ? State.Disposed
    : (Handle == 0 ? State.Uninitialized : State.Ready);

  [Browsable(false)]
  public uint Handle { get; private set; }

  public void Upload() {
    var gl = Scene.IoC.Resolve<GL>();
    Handle = gl.GenTexture();
    gl.BindTexture(TextureTarget.Texture2D, Handle);

    // FIXME: SAFELY upload texture pixels to GPU!
    var success = Pixels.DangerousTryGetSinglePixelMemory(out var pixelMemory);
    Debug.Assert(success, "Failed to get pixel memory from albedo texture");
    ReadOnlySpan<Rgba32> pixels = pixelMemory.Span;
    gl.TexImage2D(
      TextureTarget.Texture2D,
      0,
      InternalFormat.Rgba,
      Convert.ToUInt32(Width),
      Convert.ToUInt32(Height),
      0,
      PixelFormat.Rgba,
      PixelType.UnsignedByte,
      pixels
    );

    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    gl.BindTexture(TextureTarget.Texture2D, 0);
  }

  public void Dispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);
    Pixels.Dispose();
    disposed = true;
  }
}

public class AnimatedTexture(string name, FlexiTextureList textures) : IEnumerable<Texture> {
  private readonly Texture[] _textures = [.. textures.Frames.Select(
    frame => new Texture(name, textures.Width, textures.Height, frame.Texture, frame.Recolorable)
  )];

  [Category("Design")]
  public string Name { get; private set; } = name;
  [Category("Appearance")]
  public uint Fps { get; } = textures.Fps;
  [Browsable(false)]
  public Texture[] Frames => _textures;
  [Browsable(false)]
  public Texture this[int index] => _textures[index];

  public IEnumerator<Texture> GetEnumerator() => _textures.AsEnumerable().GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

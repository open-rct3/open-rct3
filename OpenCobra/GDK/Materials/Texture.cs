// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;
using System.Diagnostics;

namespace OpenCobra.GDK.Materials;

/// <summary>
/// One frame's mip chain. A static texture has a single <see cref="MipChain"/> with every mip
/// level. An animated (flexi) texture has one single-resolution <see cref="MipChain"/> per frame.
/// </summary>
public record struct MipChain(IReadOnlyList<Image<Rgba32>> Mips);

/// <summary>
/// Animation metadata for a multi-frame <see cref="Texture"/>. <c>FrameCount == 1</c> and
/// <c>Fps == 0</c> for static textures; <c>FrameCount == Frames.Count</c> for animated ones.
/// </summary>
public record struct Animation(uint Fps, int FrameWidth, int FrameHeight, int FrameCount);

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
  public TextureFormat Format { get; init; } = TextureFormat.A8R8G8B8;

  /// <summary>
  /// Every frame's mip chain. Static textures hold one frame with every mip; animated (flexi)
  /// textures hold one single-resolution frame per animation frame.
  /// </summary>
  [Browsable(false)]
  public IReadOnlyList<MipChain> Frames { get; init; } = [new MipChain([texture])];

  /// <summary>
  /// Animation metadata, or <c>null</c> for a static texture. The renderer reads this together
  /// with <see cref="Frames"/> - it never iterates separate <see cref="Texture"/> instances.
  /// </summary>
  [Category("Appearance")]
  public Animation? Animation { get; init; }

  /// <summary>
  /// Convenience alias for <c>Frames[0].Mips[0]</c>, the existing public surface.
  /// </summary>
  [Category("Appearance")]
  public Image<Rgba32> Pixels => Frames[0].Mips[0];

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
    var gl = IGame.IoC.Resolve<GL>();
    Handle = gl.GenTexture();
    gl.BindTexture(TextureTarget.Texture2D, Handle);

    // The renderer's job is to map Frames + Animation onto a GL texture layout (sprite-sheet,
    // array texture, etc.); this is the minimal per-mip loop that places pixels.
    for (var level = 0; level < Frames[0].Mips.Count; level++) {
      var mip = Frames[0].Mips[level];
      var success = mip.DangerousTryGetSinglePixelMemory(out var pixelMemory);
      Debug.Assert(success, "Failed to get pixel memory from texture mip");
      ReadOnlySpan<Rgba32> pixels = pixelMemory.Span;
      gl.TexImage2D(
        TextureTarget.Texture2D,
        level,
        InternalFormat.Rgba,
        Convert.ToUInt32(Math.Max(1, Width >> level)),
        Convert.ToUInt32(Math.Max(1, Height >> level)),
        0,
        PixelFormat.Rgba,
        PixelType.UnsignedByte,
        pixels
      );
    }

    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    gl.BindTexture(TextureTarget.Texture2D, 0);
  }

  public void Dispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);
    foreach (var frame in Frames)
      foreach (var mip in frame.Mips)
        mip.Dispose();
    disposed = true;
  }
}

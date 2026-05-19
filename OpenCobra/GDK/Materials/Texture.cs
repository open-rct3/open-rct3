// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections;
using System.ComponentModel;

namespace OpenCobra.GDK.Materials;

public class Texture(string name, Image<Rgba32> texture, Recolorable recolorable = 0) : IDisposable {
  private bool disposed;

  [Category("Design")]
  public string Name { get; private set; } = name;
  [Category("Appearance")]
  public int Width { get; } = texture.Width;
  [Category("Appearance")]
  public int Height { get; } = texture.Height;
  [Category("Appearance")]
  public Recolorable Recolorable { get; } = recolorable;
  [Category("Appearance")]
  public Image<Rgba32> Pixels { get; } = texture;

  /// <summary>
  /// Whether this texture is recolorable.
  /// </summary>
  public bool IsRecolorable => Recolorable != Recolorable.None;

  public void Dispose() {
    if (disposed) return;
    GC.SuppressFinalize(this);
    Pixels.Dispose();
    disposed = true;
  }
}

public class AnimatedTexture(string name, FlexiTextureList textures) : IEnumerable<Texture> {
  private readonly Texture[] _textures = [.. textures.Frames.Select(
    frame => new Texture(name, frame.Texture, frame.Recolorable)
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

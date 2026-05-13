// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.GDK.Materials;

public class Texture<PixelFormat>(string name, int width, int height, bool opaque = true) : IDisposable
  where PixelFormat : struct, IPixel {
  private bool _disposed;

  [Category("Design")]
  public string Name { get; private set; } = name;
  [Category("Appearance")]
  public int Width { get; } = width;
  [Category("Appearance")]
  public int Height { get; } = height;
  [Category("Appearance")]
  public PixelFormat[] Pixels { get; init; } = new PixelFormat[width * height];
  [Category("Appearance")]
  public bool HasAlpha { get; } = !opaque;

  public void Dispose() {
    if (_disposed) return;
    GC.SuppressFinalize(this);
    _disposed = true;
  }
}

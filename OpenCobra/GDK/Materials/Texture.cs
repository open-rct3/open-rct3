// Texture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.ComponentModel;
using System.Drawing.Imaging;

namespace OpenCobra.GDK.Materials;

public class Texture(string name, PixelFormat format) : IDisposable {
  private bool _disposed;

  [Category("Design")]
  public string Name { get; private set; } = name;
  [Category("Appearance")]
  public int Width { get; init; }
  [Category("Appearance")]
  public int Height { get; init; }
  [Category("Appearance")]
  public PixelFormat Format { get; private set; } = format;
  [Category("Appearance")]
  public byte[] Pixels { get; init; } = [];
  [Category("Appearance")]
  public bool HasAlpha { get; init; }

  public void Dispose() {
    if (_disposed) return;
    GC.SuppressFinalize(this);
    _disposed = true;
  }
}

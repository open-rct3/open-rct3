// Material
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved
using SixLabors.ImageSharp.PixelFormats;
using System.ComponentModel;

namespace OpenCobra.GDK.Materials;

using Texture = Texture<Rgba32>;

public class Material {
  [Category("Appearance")]
  public Texture? AlbedoTexture { get; set; }
  [Category("Appearance")]
  public Texture? NormalMap { get; set; }
  [Category("Appearance")]
  public Texture? SpecularMap { get; set; }
  [Category("Appearance")]
  public Texture? EmissiveMap { get; set; }
  [Browsable(false)]
  public bool TransparencyEnabled { get => Opacity < 1; }
  [Category("Appearance")]
  public float Opacity { get; set; } = 1.0f;
}

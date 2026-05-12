// PaletteConverterTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenCobra.GDK.Materials;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class PaletteConverterTests {
  [Test]
  public void ConvertIndexedToRgba_WithoutAlpha_SetsAlphaTo255() {
    var indexedPixels = new byte[] { 0, 1, 2, 0 };
    var width = 2;
    var height = 2;
    var palette = new byte[1024];
    // Index 0: Red
    palette[0] = 255; palette[1] = 0; palette[2] = 0; palette[3] = 255;
    // Index 1: Green
    palette[4] = 0; palette[5] = 255; palette[6] = 0; palette[7] = 255;
    // Index 2: Blue
    palette[8] = 0; palette[9] = 0; palette[10] = 255; palette[11] = 255;
    var alphaPixels = Array.Empty<byte>();
    var outputRgba = new Rgba32[width * height];

    PaletteConverter.ConvertIndexedToRgba(indexedPixels, width, height, palette, alphaPixels, outputRgba);

    Assert.That(outputRgba[0], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
    Assert.That(outputRgba[1], Is.EqualTo(new Rgba32(0, 255, 0, 255)));
    Assert.That(outputRgba[2], Is.EqualTo(new Rgba32(0, 0, 255, 255)));
    Assert.That(outputRgba[3], Is.EqualTo(new Rgba32(255, 0, 0, 255)));
  }

  [Test]
  public void ConvertIndexedToRgba_WithAlpha_AppliesAlphaPixels() {
    var indexedPixels = new byte[] { 1, 0, 2, 1 };
    var width = 2;
    var height = 2;
    var palette = new byte[1024];
    // Index 0
    palette[0] = 10; palette[1] = 20; palette[2] = 30; palette[3] = 255;
    // Index 1
    palette[4] = 40; palette[5] = 50; palette[6] = 60; palette[7] = 255;
    // Index 2
    palette[8] = 70; palette[9] = 80; palette[10] = 90; palette[11] = 255;
    var alphaPixels = new byte[] { 100, 150, 200, 250 };
    var outputRgba = new Rgba32[width * height];

    PaletteConverter.ConvertIndexedToRgba(indexedPixels, width, height, palette, alphaPixels, outputRgba);

    Assert.That(outputRgba[0], Is.EqualTo(new Rgba32(40, 50, 60, 100)));
    Assert.That(outputRgba[1], Is.EqualTo(new Rgba32(10, 20, 30, 150)));
    Assert.That(outputRgba[2], Is.EqualTo(new Rgba32(70, 80, 90, 200)));
    Assert.That(outputRgba[3], Is.EqualTo(new Rgba32(40, 50, 60, 250)));
  }
}

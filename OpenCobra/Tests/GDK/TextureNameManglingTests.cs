// TextureNameManglingTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// GDK-side regression tests for OVL file-type tag stripping. The OVL decoder writes
// `Name.Type` ("Terrain_06.tex", "Foo.ftx", ...) into OVL.Files.Texture.Name; the
// GDK Texture ctor strips that suffix so the resulting GDK Texture.Name is the bare
// human-readable symbol name callers look up by. These tests pin that behavior at the
// GDK boundary (the only place GDK callers see the name) so the fix can't silently
// regress for static, flexi, or animated textures.

using OpenCobra.GDK.Assets;
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using GdkTexture = OpenCobra.GDK.Materials.Texture;
using Texture = OpenCobra.OVL.Files.Texture;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class TextureNameManglingTests {
  private static Texture MakeSyntheticOvlTexture(
    string name, int width = 2, int height = 2, int mipCount = 1
  ) {
    var texture = new Texture(name, TextureFormat.A8R8G8B8, (uint)width, (uint)height, (uint)mipCount);
    for (var level = 0; level < mipCount; level++) {
      var w = Math.Max(1, width >> level);
      var h = Math.Max(1, height >> level);
      var pixels = new Rgba32[w * h];
      for (var i = 0; i < pixels.Length; i++) pixels[i] = new Rgba32((byte)(i + 1), 0, 0, 255);
      texture.MipLevels[level] = Image.LoadPixelData<Rgba32>(pixels, w, h);
    }
    return texture;
  }

  [Test]
  public void GdkTexture_InheritsStrippedName_FromOvlTexSuffix() {
    using var ovl = MakeSyntheticOvlTexture("Terrain_06.tex");
    using var gdk = TextureLoader.ToGl(ovl);

    Assert.That(ovl.Name, Is.EqualTo("Terrain_06"),
      "OVL-side stripping happens in the Ovl.Files.Texture ctor");
    Assert.That(gdk.Name, Is.EqualTo("Terrain_06"),
      "GDK-side: GDK Texture.Name must be the bare symbol name, no .tex suffix");
  }

  [Test]
  public void GdkTexture_InheritsStrippedName_FromFlicSuffix() {
    using var ovl = MakeSyntheticOvlTexture("SomeFlic.flic");
    using var gdk = TextureLoader.ToGl(ovl);

    Assert.That(gdk.Name, Is.EqualTo("SomeFlic"));
  }

  [Test]
  public void GdkTexture_PreservesMultiFrameFtxHashIndex() {
    // FlexiTexture's multi-frame path builds names like "Foo.ftx#0", "Foo.ftx#1".
    // The ctor strips the ".ftx" tag but keeps the "#i" frame index so each
    // animation frame still has a unique name in the GDK Texture.
    using var ovl = MakeSyntheticOvlTexture("Foo.ftx#2");
    using var gdk = TextureLoader.ToGl(ovl);

    Assert.That(ovl.Name, Is.EqualTo("Foo#2"));
    Assert.That(gdk.Name, Is.EqualTo("Foo#2"));
  }

  [Test]
  public void GdkTexture_PassesBareNameThrough_WithoutAddingSuffix() {
    // If a caller hands in an already-bare name, neither side should re-suffix it.
    using var ovl = MakeSyntheticOvlTexture("AlreadyBare");
    using var gdk = TextureLoader.ToGl(ovl);

    Assert.That(gdk.Name, Is.EqualTo("AlreadyBare"));
  }

  [Test]
  public void GdkTexture_AnimationFrameFlatten_PropagatesFrameName() {
    // Mirrors TextureLoader.LoadTexture's FileType.FlexibleTexture branch: each frame
    // is ToGl'd, then flattened into one GDK Texture with Frames.Count == collection.Count.
    // Each per-frame OVL texture carries the suffixed "Name.ftx#i" form that
    // FlexiTextureList.Parse produces; the ctor leaves that intact (the "#i" tail makes
    // ".ftx" not a clean trailing tag) so each frame keeps a unique name. The flattened
    // combined GDK texture inherits the first frame's name verbatim.
    using var frame0 = MakeSyntheticOvlTexture("FlexiTex.ftx#0");
    using var frame1 = MakeSyntheticOvlTexture("FlexiTex.ftx#1");
    var collection = new TextureCollection([frame0, frame1], fps: 12);

    GdkTexture? result = null;
    for (var i = 0; i < collection.Count; i++) {
      var animation = i == 0
        ? new Animation(collection.Fps, 2, 2, collection.Count)
        : (Animation?)null;
      var frameTexture = TextureLoader.ToGl(collection[i], animation);
      result = result == null
        ? frameTexture
        : new GdkTexture(result.Name, result.Width, result.Height, result.Pixels, result.Recolorable) {
            Format = result.Format,
            Frames = [.. result.Frames, .. frameTexture.Frames],
            Animation = result.Animation,
          };
    }
    using (result) {
      Assert.That(result, Is.Not.Null);
      Assert.That(result!.Name, Is.EqualTo("FlexiTex#0"),
        "Flattened GDK Texture.Name is the first frame's per-frame name with .ftx stripped");
      Assert.That(result.Frames.Count, Is.EqualTo(2));
    }
  }
}

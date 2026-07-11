// FileTypeNameManglingTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OpenCobra.Tests.OVL;

/// <summary>
/// Regression tests for the OVL file-type tag suffix stripping that
/// <see cref="Texture"/>'s constructor and <see cref="Texture.WithName"/> apply so callers can look
/// textures up by their bare symbol name (e.g. <c>"Terrain_06"</c>) instead of
/// <c>Name.Type</c> (e.g. <c>"Terrain_06.tex"</c>).
/// </summary>
[TestFixture]
public class FileTypeNameManglingTests {
  [TestCase("Terrain_06.tex", "Terrain_06")]
  [TestCase("TerrainCliff0.tex", "TerrainCliff0")]
  [TestCase("GUIIcon.ftx", "GUIIcon")]
  [TestCase("Foo.flic", "Foo")]
  [TestCase("bar.btbl", "bar")]
  [TestCase("SkinBody_AF01_L1.mms", "SkinBody_AF01_L1")]
  [TestCase("SkinLegs_AF01_L1.prt", "SkinLegs_AF01_L1")]
  [TestCase("Foo.ftx#0", "Foo#0")]
  [TestCase("Foo.ftx#1", "Foo#1")]
  [TestCase("Foo.ftx#12", "Foo#12")]
  [TestCase("Terrain_06.ftx#0", "Terrain_06#0")]
  [TestCase("Foo:tex", "Foo")]
  [TestCase("GUIIcon:ftx", "GUIIcon")]
  public void StripOvlTagSuffix_RemovesRecognisedOvlTag(string input, string expected) {
    Assert.That(input.StripOvlTagSuffix(), Is.EqualTo(expected));
  }

  [TestCase("Foo.bar#1")]        // hash-indexed non-OVL tag
  [TestCase("plain")]            // no separator at all
  [TestCase("a.b.c")]            // trailing "c" is not an OVL tag
  [TestCase("foo.unknown")]      // unknown extension
  [TestCase(".tex")]             // leading dot, nothing before
  [TestCase("foo.")]             // trailing dot, nothing after
  public void StripOvlTagSuffix_LeavesNonTagSuffixesAlone(string input) {
    Assert.That(input.StripOvlTagSuffix(), Is.EqualTo(input));
  }

  [Test]
  public void Texture_Ctor_StripsTexSuffix() {
    using var tex = new Texture("Terrain_06.tex", TextureFormat.Dxt1, 256, 256, mipCount: 4);
    Assert.That(tex.Name, Is.EqualTo("Terrain_06"));
  }

  [Test]
  public void Texture_Ctor_LeavesUntaggedNameAlone() {
    using var tex = new Texture("AlreadyBare", TextureFormat.A8R8G8B8, 2, 2);
    Assert.That(tex.Name, Is.EqualTo("AlreadyBare"));
  }

  [Test]
  public void Texture_Ctor_PreservesMultiFrameFtxHashIndex() {
    // Multi-frame ftx lookups build names like "Foo.ftx#0", "Foo.ftx#1"; the ctor strips
    // just the ".ftx" portion and keeps the "#i" frame index so each frame stays unique.
    using var tex = new Texture("Foo.ftx#0", TextureFormat.A8R8G8B8, 2, 2);
    Assert.That(tex.Name, Is.EqualTo("Foo#0"));
  }

  [Test]
  public void Texture_WithName_AlsoStripsSuffix() {
    using var tex = new Texture("Anything", TextureFormat.A8R8G8B8, 2, 2);
    var renamed = tex.WithName("Terrain_06.tex");
    Assert.That(renamed.Name, Is.EqualTo("Terrain_06"));
  }
}

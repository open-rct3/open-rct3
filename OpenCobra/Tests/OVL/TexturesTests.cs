using NUnit.Framework;
using OpenCobra.GDK.Assets;
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Reflection;
using System.IO;

using GdkTexture = OpenCobra.GDK.Materials.Texture;
using Texture = OpenCobra.OVL.Files.Texture;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TexturesTests {
  // Every embedded "*.common.ovl" fixture under Fixtures/OVL/ (BaseGame or CustomScenery)
  // gets its own test case here automatically - no code changes needed to add a fixture.
  private static IEnumerable<string> CommonOvlResourceNames() =>
    Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.EndsWith(".common.ovl"));

  [TestCaseSource(nameof(CommonOvlResourceNames))]
  public void Extract_FromFixtureOvl_ReturnsCollection(string commonResourceName) {
    var assembly = Assembly.GetExecutingAssembly();
    // Ovl.Load resolves the paired ".unique.ovl" by replacing the suffix on the given
    // path's base name, so both files must land in the same temp directory under
    // matching names - the original embedded resource name (which includes the fixture's
    // subfolder as dots) doesn't matter here, only the local file name does.
    var uniqueResourceName = commonResourceName[..^".common.ovl".Length] + ".unique.ovl";

    var tempDir = Directory.CreateTempSubdirectory().FullName;
    try {
      var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
      CopyResourceTo(assembly, commonResourceName, commonPath);

      if (assembly.GetManifestResourceNames().Contains(uniqueResourceName))
        CopyResourceTo(assembly, uniqueResourceName, Path.Combine(tempDir, "fixture.unique.ovl"));

      using var ovl = Ovl.Load(commonPath);
      var textures = Textures.Extract(ovl);
      Assert.That(textures, Is.Not.Null);
      // We don't necessarily know a given fixture has textures, but we can check it doesn't throw
    } finally {
      Directory.Delete(tempDir, recursive: true);
    }
  }

  private static void CopyResourceTo(Assembly assembly, string resourceName, string destPath) {
    using var stream = assembly.GetManifestResourceStream(resourceName);
    Assert.That(stream, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
    using var fs = File.OpenWrite(destPath);
    stream.CopyTo(fs);
  }

  private static Texture MakeSyntheticTexture(
    string name = "synth", int width = 2, int height = 2, int mipCount = 1,
    Recolorable recolorable = Recolorable.None, bool nullifyMip = false
  ) {
    var texture = new Texture(name, TextureFormat.A8R8G8B8, (uint)width, (uint)height, (uint)mipCount, recolorable);
    for (var level = 0; level < mipCount; level++) {
      if (nullifyMip && level == 0) continue;
      var w = Math.Max(1, width >> level);
      var h = Math.Max(1, height >> level);
      var pixels = new Rgba32[w * h];
      for (var i = 0; i < pixels.Length; i++) pixels[i] = new Rgba32((byte)(i + 1), 0, 0, 255);
      texture.MipLevels[level] = Image.LoadPixelData<Rgba32>(pixels, w, h);
    }
    return texture;
  }

  [Test]
  public void TextureCollection_Ctor_KeysByNameAndSetsFps() {
    using var a = MakeSyntheticTexture("A");
    using var b = MakeSyntheticTexture("B");
    var collection = new TextureCollection([a, b], fps: 24);

    Assert.That(collection.Fps, Is.EqualTo(24u));
    Assert.That(collection.Count, Is.EqualTo(2));
    Assert.That(collection.Names, Is.EquivalentTo(new[] { "A", "B" }));
  }

  [Test]
  public void TextureCollection_AddAndAddRange_ArePublic() {
    var collection = new TextureCollection();
    using var a = MakeSyntheticTexture("A");
    using var b = MakeSyntheticTexture("B");
    using var c = MakeSyntheticTexture("C");

    collection.Add(a);
    collection.AddRange([b, c]);

    Assert.That(collection.Count, Is.EqualTo(3));
  }

  [Test]
  public void Texture_Recolorable_DefaultsToNoneAndCanBeSet() {
    using var defaultTexture = MakeSyntheticTexture("Default");
    Assert.That(defaultTexture.Recolorable, Is.EqualTo(Recolorable.None));

    using var recolorableTexture = MakeSyntheticTexture("Recolorable", recolorable: Recolorable.First | Recolorable.Second);
    Assert.That(recolorableTexture.Recolorable, Is.EqualTo(Recolorable.First | Recolorable.Second));
  }

  [TestFixture]
  public class TakeMipTests {
    [Test]
    public void TakeMip_NullsSourceSlot_AndReturnsTheMip() {
      using var src = MakeSyntheticTexture("Src", width: 2, height: 2, mipCount: 2);
      var original = src.MipLevels[0];

      var taken = src.TakeMip(0);

      Assert.That(taken, Is.SameAs(original));
      Assert.That(src.MipLevels[0], Is.Null);
      taken.Dispose();
    }

    [Test]
    public void TakeMip_ThenDisposeSource_DoesNotDoubleDisposeTheTakenMip() {
      var src = MakeSyntheticTexture("Src", width: 2, height: 2, mipCount: 1);
      var mip0 = src.TakeMip(0);

      // Dispose() skips the nulled slot, so this must not throw or touch `mip0`.
      Assert.DoesNotThrow(new Action(src.Dispose));
      Assert.That(mip0.Width, Is.EqualTo(2));
      mip0.Dispose();
    }

    [Test]
    public void TakeMip_TransferredToGdkTexture_OwnershipIsSafeToDisposeBothSides() {
      var src = MakeSyntheticTexture("Src", width: 2, height: 2, mipCount: 1);
      var mip0 = src.TakeMip(0);

      using var gdkTexture = new GdkTexture(src.Name, (int)src.Width, (int)src.Height, mip0, src.Recolorable);

      // `src` no longer owns mip0 (TakeMip nulled the slot), and `gdkTexture` now owns it -
      // disposing both must not double-dispose the shared image.
      Assert.DoesNotThrow(new Action(src.Dispose));
      Assert.DoesNotThrow(new Action(gdkTexture.Dispose));
    }
  }

  [TestFixture]
  public class ToGlTests {
    [Test]
    public void ToGl_CopiesMipsIndependently() {
      using var src = MakeSyntheticTexture("Src", width: 2, height: 2, mipCount: 3);
      using var gdkTexture = TextureLoader.ToGl(src);

      Assert.That(gdkTexture.Frames.Count, Is.EqualTo(1));
      Assert.That(gdkTexture.Frames[0].Mips.Count, Is.EqualTo(3));
      for (var i = 0; i < 3; i++)
        Assert.That(gdkTexture.Frames[0].Mips[i], Is.Not.SameAs(src.MipLevels[i]));

      Assert.That(gdkTexture.Recolorable, Is.EqualTo(Recolorable.None));
      Assert.That(gdkTexture.Format, Is.EqualTo(TextureFormat.A8R8G8B8));
    }

    [Test]
    public void ToGl_PlumbsRecolorableFlags() {
      using var src = MakeSyntheticTexture("Src", recolorable: Recolorable.First | Recolorable.Second);
      using var gdkTexture = TextureLoader.ToGl(src);

      Assert.That(gdkTexture.Recolorable, Is.EqualTo(Recolorable.First | Recolorable.Second));
    }

    [Test]
    public void ToGl_NullMip_Throws() {
      using var src = MakeSyntheticTexture("Src", mipCount: 1, nullifyMip: true);
      Assert.Throws<InvalidOperationException>(new Action(() => TextureLoader.ToGl(src)));
    }

    [Test]
    public void ToGl_Dispose_IsIdempotent() {
      using var src = MakeSyntheticTexture("Src");
      var gdkTexture = TextureLoader.ToGl(src);

      Assert.DoesNotThrow(new Action(() => {
        gdkTexture.Dispose();
        gdkTexture.Dispose();
      }));
    }

    [Test]
    public void ToGl_DisposingGdkTexture_DoesNotAffectSourceOvlTexture() {
      using var src = MakeSyntheticTexture("Src");
      var gdkTexture = TextureLoader.ToGl(src);

      gdkTexture.Dispose();

      // The source OVL texture's mip is a distinct Image instance and must still be usable.
      Assert.That(src.MipLevels[0]!.Width, Is.EqualTo(2));
    }

    [Test]
    public void ToGl_TwiceFromSameSource_ProducesDifferentImageInstances() {
      using var src = MakeSyntheticTexture("Src");
      var clone = src.WithName("Src2");

      using var first = TextureLoader.ToGl(src);
      using var second = TextureLoader.ToGl(clone);

      Assert.That(first.Frames[0].Mips[0], Is.Not.SameAs(second.Frames[0].Mips[0]));
    }

    [Test]
    public void ToGl_ThreeFrameFlexi_ProducesIndependentFramesAndDisposesOnce() {
      using var frame0 = MakeSyntheticTexture("Flexi#0");
      using var frame1 = MakeSyntheticTexture("Flexi#1");
      using var frame2 = MakeSyntheticTexture("Flexi#2");
      var collection = new TextureCollection([frame0, frame1, frame2], fps: 12);

      var frames = collection.Select((t, i) =>
        TextureLoader.ToGl(t, i == 0 ? new Animation(collection.Fps, 2, 2, collection.Count) : (Animation?)null)
      ).ToList();

      var combined = new GdkTexture(frames[0].Name, frames[0].Width, frames[0].Height, frames[0].Pixels, frames[0].Recolorable) {
        Format = frames[0].Format,
        Frames = [.. frames.SelectMany(f => f.Frames)],
        Animation = frames[0].Animation,
      };
      foreach (var frame in frames) frame.Dispose();

      Assert.That(combined.Frames.Count, Is.EqualTo(3));
      for (var i = 0; i < collection.Count; i++)
        Assert.That(combined.Frames[i].Mips[0], Is.Not.SameAs(collection[i].MipLevels[0]));

      Assert.DoesNotThrow(new Action(combined.Dispose));
    }
  }

  [TestFixture]
  public class FlexiTextureListParseTests {
    // FlexiTextureList.Parse is the decode/naming half of Load, split out specifically so it can
    // be exercised with synthetic FlexiFrameData instead of a real OVL archive - Parse has no
    // dependency on Ovl.TryResolveRelocation, so these tests can't duplicate or drift from Ovl's
    // own relocation logic.
    private static FlexiFrameData MakeFrame(Recolorable recolorable = Recolorable.None) {
      var palette = new byte[256 * 4];
      for (var i = 0; i < 256; i++) {
        palette[i * 4 + 0] = (byte) i; // B
        palette[i * 4 + 1] = (byte) i; // G
        palette[i * 4 + 2] = (byte) i; // R
        palette[i * 4 + 3] = 255;      // A (unused by ConvertIndexedBgraToRgba)
      }
      var texture = new byte[] { 1, 2, 3, 4 }; // 2x2 palette indices
      var alpha = new byte[] { 255, 255, 255, 255 };
      return new FlexiFrameData(recolorable, palette, texture, alpha);
    }

    [Test]
    public void SingleFrame_KeepsSymbolName() {
      var collection = FlexiTextureList.Parse("ftx:Example", fps: 0, width: 2, height: 2, [MakeFrame()]);
      Assert.That(collection.Count, Is.EqualTo(1));
      Assert.That(collection[0].Name, Is.EqualTo("ftx:Example"));
      collection[0].Dispose();
    }

    [Test]
    public void MultiFrame_GetsIndexSuffixAndPlumbsFpsAndRecolorable() {
      const uint fps = 12;
      FlexiFrameData[] frameData = [
        MakeFrame(Recolorable.None),
        MakeFrame(Recolorable.First),
        MakeFrame(Recolorable.First | Recolorable.Second),
      ];
      var collection = FlexiTextureList.Parse("ftx:Example", fps, width: 2, height: 2, frameData);

      Assert.That(collection.Count, Is.EqualTo(3));
      Assert.That(collection.Fps, Is.EqualTo(fps));
      for (var i = 0; i < 3; i++) {
        Assert.That(collection[i].Name, Is.EqualTo($"ftx:Example#{i}"));
        Assert.That(collection[i].Recolorable, Is.EqualTo(frameData[i].Recolorable));
        Assert.That(collection[i].Width, Is.EqualTo(2u));
        Assert.That(collection[i].Height, Is.EqualTo(2u));
      }
      foreach (var texture in collection) texture.Dispose();
    }

    [Test]
    public void LoadTexture_FlexiSource_FlattensFramesWithAnimationMetadata() {
      const uint fps = 24;
      FlexiFrameData[] frameData = [MakeFrame(), MakeFrame(), MakeFrame()];
      var collection = FlexiTextureList.Parse("ftx:Example", fps, width: 2, height: 2, frameData);

      // Mirrors TextureLoader.LoadTexture's FileType.FlexibleTexture flattening: ToGl each frame,
      // combine into a single GDK Texture whose Frames.Count == collection.Count.
      GdkTexture? result = null;
      for (var i = 0; i < collection.Count; i++) {
        var animation = i == 0
          ? new Animation(collection.Fps, Convert.ToInt32(collection[i].Width), Convert.ToInt32(collection[i].Height), collection.Count)
          : (Animation?) null;
        var frameTexture = TextureLoader.ToGl(collection[i], animation);
        result = result == null
          ? frameTexture
          : new GdkTexture(result.Name, result.Width, result.Height, result.Pixels, result.Recolorable) {
            Format = result.Format,
            Frames = [.. result.Frames, .. frameTexture.Frames],
            Animation = result.Animation,
          };
      }

      Assert.That(result, Is.Not.Null);
      Assert.That(result!.Frames.Count, Is.EqualTo(collection.Count));
      Assert.That(result.Animation, Is.Not.Null);
      Assert.That(result.Animation!.Value.Fps, Is.EqualTo(fps));
      Assert.That(result.Animation!.Value.FrameWidth, Is.EqualTo(2));
      Assert.That(result.Animation!.Value.FrameHeight, Is.EqualTo(2));
      Assert.That(result.Animation!.Value.FrameCount, Is.EqualTo(collection.Count));

      result.Dispose();
      foreach (var texture in collection) texture.Dispose();
    }
  }
}

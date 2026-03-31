using System.Drawing;
using System.Linq;
using NUnit.Framework;

namespace OpenRCT3.Tests;

public class IconTests {
  [Test]
  public void LoadIcon_ReturnsNonNullWindowIcon() {
    var icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    Assert.That(icon, Is.Not.Null);
  }

  [Test]
  public void LoadIcon_ContainsMultipleResolutions() {
    var icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    Assert.That(icon.Images, Is.Not.Empty);
    Assert.That(icon.Images.Length, Is.GreaterThan(1));
  }

  [Test]
  public void LoadIcon_HasExpectedSizes() {
    var icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    var sizes = icon.Images.Select(i => i.Width).Distinct().Order().ToArray();
    Assert.That(sizes, Is.SupersetOf(new[] { 16, 32, 48 }));
  }

  [Test]
  public void LoadIcon_PixelsAreNotBlank() {
    var icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    foreach (var image in icon.Images) {
      var hasColor = image.Data.Any(b => b != 0);
      Assert.That(hasColor, Is.True, $"Image {image.Width}x{image.Height} has all-zero pixels");
    }
  }

  [Test]
  public void LoadIcon_PixelsAreNotFullyTransparent() {
    var icon = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    foreach (var image in icon.Images) {
      var hasAlpha = false;
      for (var i = 3; i < image.Data.Length; i += 4) {
        if (image.Data[i] > 0) {
          hasAlpha = true;
          break;
        }
      }
      Assert.That(hasAlpha, Is.True, $"Image {image.Width}x{image.Height} is fully transparent");
    }
  }

  [Test]
  public void LoadIcon_16x16_MatchesSourceResource() {
    var asm = typeof(Icons).Assembly;
    using var stream = asm.GetManifestResourceStream("OpenRCT3.Panda.ico");
    Assert.That(stream, Is.Not.Null, "Embedded resource not found");

    using var multiIcon = new Icon(stream!);
    using var icon16 = new Icon(multiIcon, 16, 16);
    var expected = Icons.ToImage(icon16);

    var loaded = Icons.LoadEmbedded("OpenRCT3.Panda.ico");
    var image16 = loaded.Images.First(i => i.Width == 16);
    Assert.That(image16.Data, Is.EqualTo(expected.Data));
  }
}

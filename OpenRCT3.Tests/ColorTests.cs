// Unit tests for color conversions between System.Drawing.Color, vectors, and CSS hex.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using NUnit.Framework;
using OpenRCT3;
using Drawing = System.Drawing;

namespace OpenRCT3.Tests;

[TestFixture]
public class ColorTests {
  [Test]
  public void FromRgb_WithHexInt_ConstructsExpectedColor() {
    var color = OpenRCT3.Color.FromRgb(0x4CAF50);
    Assert.That(color.R, Is.EqualTo(0x4C));
    Assert.That(color.G, Is.EqualTo(0xAF));
    Assert.That(color.B, Is.EqualTo(0x50));
    Assert.That(color.A, Is.EqualTo(255));
  }

  [Test]
  public void FromRgba_WithHexInt_ConstructsExpectedColor() {
    var color = OpenRCT3.Color.FromRgba(unchecked((int)0x4CAF5080));
    Assert.That(color.R, Is.EqualTo(0x4C));
    Assert.That(color.G, Is.EqualTo(0xAF));
    Assert.That(color.B, Is.EqualTo(0x50));
    Assert.That(color.A, Is.EqualTo(0x80));
  }

  [Test]
  public void FromRgba_WithBytes_ConstructsExpectedColor() {
    var color = OpenRCT3.Color.FromRgba(76, 175, 80, 128);
    Assert.That(color.R, Is.EqualTo(76));
    Assert.That(color.G, Is.EqualTo(175));
    Assert.That(color.B, Is.EqualTo(80));
    Assert.That(color.A, Is.EqualTo(128));
  }

  [Test]
  public void ToGl_AndToVector4_ReturnsNormalizedValues() {
    var color = Drawing.Color.FromArgb(255, 128, 64, 32);
    var vec = color.ToVector4();
    Assert.That(vec.X, Is.EqualTo(128f / 255f).Within(1e-4));
    Assert.That(vec.Y, Is.EqualTo(64f / 255f).Within(1e-4));
    Assert.That(vec.Z, Is.EqualTo(32f / 255f).Within(1e-4));
    Assert.That(vec.W, Is.EqualTo(1.0f).Within(1e-4));

    var glVec = color.ToGl();
    Assert.That(glVec, Is.EqualTo(vec));
  }

  [Test]
  public void ToVector3_ReturnsNormalizedRgb() {
    var color = Drawing.Color.FromArgb(255, 128, 64, 32);
    var vec = color.ToVector3();
    Assert.That(vec.X, Is.EqualTo(128f / 255f).Within(1e-4));
    Assert.That(vec.Y, Is.EqualTo(64f / 255f).Within(1e-4));
    Assert.That(vec.Z, Is.EqualTo(32f / 255f).Within(1e-4));
  }

  [Test]
  public void Vector4_ToColor_RoundtripsSuccessfully() {
    var vec = new Vector4(0.2f, 0.4f, 0.6f, 0.8f);
    var color = vec.ToColor();
    Assert.That(color.R, Is.EqualTo(Convert.ToByte(0.2f * 255f)));
    Assert.That(color.G, Is.EqualTo(Convert.ToByte(0.4f * 255f)));
    Assert.That(color.B, Is.EqualTo(Convert.ToByte(0.6f * 255f)));
    Assert.That(color.A, Is.EqualTo(Convert.ToByte(0.8f * 255f)));
  }

  [Test]
  public void Vector3_ToColor_ProducesOpaqueColor() {
    var vec = new Vector3(0.5f, 0.25f, 0.75f);
    var color = vec.ToColor();
    Assert.That(color.R, Is.EqualTo(Convert.ToByte(0.5f * 255f)));
    Assert.That(color.G, Is.EqualTo(Convert.ToByte(0.25f * 255f)));
    Assert.That(color.B, Is.EqualTo(Convert.ToByte(0.75f * 255f)));
    Assert.That(color.A, Is.EqualTo(255));
  }

  [Test]
  public void ToCss_FormatsHexStringsCorrectly() {
    var color = Drawing.Color.FromArgb(255, 76, 175, 80);
    Assert.That(color.ToCss(), Is.EqualTo("#4CAF50"));
    Assert.That(color.ToCss(includeAlpha: true), Is.EqualTo("#4CAF50FF"));
  }

  [Test]
  public void FromCss_ParsesHexStringsCorrectly() {
    var c1 = OpenRCT3.Color.FromCss("#4CAF50");
    Assert.That(c1.R, Is.EqualTo(76));
    Assert.That(c1.G, Is.EqualTo(175));
    Assert.That(c1.B, Is.EqualTo(80));
    Assert.That(c1.A, Is.EqualTo(255));

    var c2 = OpenRCT3.Color.FromCss("#4CAF5080");
    Assert.That(c2.R, Is.EqualTo(76));
    Assert.That(c2.G, Is.EqualTo(175));
    Assert.That(c2.B, Is.EqualTo(80));
    Assert.That(c2.A, Is.EqualTo(128));
  }

  [Test]
  public void FromCss_InvalidFormat_ThrowsFormatException() {
    Assert.Throws<FormatException>(new Action(() => OpenRCT3.Color.FromCss("invalid")));
  }

  [Test]
  public void ToRgb_AndToRgba_ConvertsCorrectly() {
    var color = Drawing.Color.FromArgb(128, 76, 175, 80);
    Assert.That(color.ToRgb(), Is.EqualTo(0x4CAF50));
    Assert.That(color.ToRgba(), Is.EqualTo(unchecked((int)0x4CAF5080)));
    Assert.That(color.ToRgbaUint(), Is.EqualTo(0x4CAF5080u));
  }
}

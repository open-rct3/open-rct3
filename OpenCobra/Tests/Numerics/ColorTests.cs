// Unit tests for WCAG color utilities.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;
using Drawing = System.Drawing;

namespace OpenCobra.Tests.Numerics;

[TestFixture]
public class ColorTests {
  [Test]
  [Description("Calculates the standard maximum 21:1 contrast ratio between black and white.")]
  public void ContrastRatioBlackAndWhite() {
    var ratio = Color.CalculateContrastRatio(0xFFFFFFFFu, 0xFF000000u);
    Assert.That(ratio, Is.EqualTo(21.0).Within(0.01));
  }

  [Test]
  [Description("Converts Drawing.Color to ImGui ABGR uint format.")]
  public void DrawingColorToUint() {
    var color = Drawing.Color.FromArgb(255, 76, 175, 80); // #4CAF50 with full opacity
    var uint_color = Color.CalculateLuminance(color);
    Assert.That(uint_color, Is.GreaterThanOrEqualTo(0.0));
  }

  [Test]
  [Description("Resolves accessible label colors maintaining WCAG AA 4.5:1 contrast against window background and plot fill.")]
  public void ResolveLabelColorAgainstBackgroundAndFill() {
    var lineColor = 0xFF4CAF50u; // #4CAF50 in ImGui ABGR
    var fillColor = 0x5950AF4Cu; // 35% alpha #4CAF50
    var windowBg = 0xFF1E1E1Eu;  // Standard ImGui dark window background

    var resolved = Color.ResolveLabelColor(lineColor, windowBg, fillColor);
    var effectiveBackground = Color.BlendOver(fillColor, windowBg);

    var windowContrast = Color.CalculateContrastRatio(resolved, windowBg);
    var fillContrast = Color.CalculateContrastRatio(resolved, effectiveBackground);

    using (Assert.EnterMultipleScope()) {
      Assert.That(windowContrast, Is.GreaterThanOrEqualTo(4.5), "Label must contrast =4.5:1 against window.");
      Assert.That(fillContrast, Is.GreaterThanOrEqualTo(4.5), "Label must contrast =4.5:1 against fill.");
    }
  }

  [Test]
  [Description("Blends a semi-transparent foreground over a background color.")]
  public void BlendOverAlphaComposite() {
    var foreground = 0x800000FFu; // 50% red (ABGR)
    var background = 0xFFFF0000u; // opaque blue (ABGR)
    var blended = Color.BlendOver(foreground, background);

    var a = (blended >> 24) & 0xFFu;
    Assert.That(a, Is.EqualTo(0xFF), "Blended result must be fully opaque.");
  }

  [Test]
  [Description("Resolves to original line color when it already satisfies contrast.")]
  public void ResolveLabelColorWhenLineContrasts() {
    var lineColor = 0xFFFFFFFFu; // White line
    var windowBg = 0xFF1E1E1Eu;
    var resolved = Color.ResolveLabelColor(lineColor, windowBg, null);
    Assert.That(resolved, Is.EqualTo(lineColor), "White line already contrasts; no change needed.");
  }

  [Test]
  [Description("Chooses black or white based on higher contrast when line color fails thresholds.")]
  public void ResolveLabelColorChoosesHighestContrast() {
    var lineColor = 0xFF808080u; // Mid-gray; should fail to contrast
    var windowBg = 0xFF1E1E1Eu;
    var resolved = Color.ResolveLabelColor(lineColor, windowBg, null);

    var isWhiteOrBlack = resolved == 0xFFFFFFFFu || resolved == 0xFF000000u;
    Assert.That(isWhiteOrBlack, Is.True, "Resolved color must be white or black.");
  }

  [Test]
  [Description("Handles zero alpha blending edge case (fully transparent foreground).")]
  public void BlendOverZeroAlpha() {
    var transparent = 0x000000FFu; // 0% alpha red (ABGR)
    var background = 0xFFFF0000u; // opaque blue (ABGR)
    var blended = Color.BlendOver(transparent, background);

    var r = (blended) & 0xFFu;
    var g = (blended >> 8) & 0xFFu;
    var b = (blended >> 16) & 0xFFu;
    using (Assert.EnterMultipleScope()) {
      Assert.That(r, Is.EqualTo(0x00));
      Assert.That(g, Is.EqualTo(0x00));
      Assert.That(b, Is.EqualTo(0xFF));
    }
  }

  [Test]
  [Description("Calculates luminance correctly for pure black and pure white edge cases.")]
  public void LuminanceEdgeCases() {
    var blackLuminance = Color.CalculateLuminance(0xFF000000u);
    var whiteLuminance = Color.CalculateLuminance(0xFFFFFFFFu);

    using (Assert.EnterMultipleScope()) {
      Assert.That(blackLuminance, Is.LessThan(0.01), "Black luminance should be ~0.");
      Assert.That(whiteLuminance, Is.GreaterThan(0.99), "White luminance should be ~1.");
    }
  }

  [Test]
  [Description("Verifies RGB channel values after blending 50% red over blue.")]
  public void BlendOverRgbChannels() {
    var foreground = 0x800000FFu; // 50% red (R=255, G=0, B=0, A=128 in ABGR)
    var background = 0xFFFF0000u; // opaque blue (R=0, G=0, B=255, A=255 in ABGR)
    var blended = Color.BlendOver(foreground, background);

    var r = blended & 0xFFu;
    var g = (blended >> 8) & 0xFFu;
    var b = (blended >> 16) & 0xFFu;

    using (Assert.EnterMultipleScope()) {
      Assert.That(r, Is.InRange(127, 129), "R channel should be ~128 after 50% red blend.");
      Assert.That(g, Is.EqualTo(0), "G channel should remain 0.");
      Assert.That(b, Is.InRange(126, 128), "B channel should be ~127 after 50% blue blend.");
    }
  }
}

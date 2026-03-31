using OpenTK.Mathematics;

namespace OpenRCT3;

public static class ColorExtensions {
  public static Color4<Rgba> ToGl(this System.Drawing.Color color) {
    return new Color4<Rgba>(
      color.R / 255f,
      color.G / 255f,
      color.B / 255f,
      color.A / 255f
    );
  }
}

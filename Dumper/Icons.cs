using Rop.Winforms8.DuotoneIcons;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;


namespace Dumper;

internal class Icons {
  public const int DefaultSize = 16;

  internal static Bitmap? Render(IEmbeddedIcons icons, string name, DuoToneColor color, int size = DefaultSize) {
    var icon = icons.GetIcon(name);
    if (icon == null) return null;

    var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    icon.DrawIcon(g, color, 0, 0, size);
    return bmp;
  }
}

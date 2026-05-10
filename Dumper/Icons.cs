using Rop.Winforms8.DuotoneIcons;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;


namespace Dumper;

internal class Icons {
  public const int DefaultSize = 16;
  public static readonly Icon DefaultWindowIcon = SystemIcons.Application;

  public static readonly DuoToneColor DefaultColor = new(
    Color.FromArgb(64, 64, 64),
    Color.FromArgb(185, 185, 185)
  );
  public static readonly DuoToneColor Blue = new(
    Color.FromArgb(25, 118, 210),
    Color.White
  );
  public static readonly DuoToneColor Folder = new(
    Color.FromArgb(200, 162, 23),
    Color.FromArgb(200, 162, 23)
  );
  public static readonly DuoToneColor Danger = new(
    Color.FromArgb(211, 47, 47),
    Color.Transparent
  );

  internal static Bitmap? Render(
    IEmbeddedIcons icons, string name, DuoToneColor? color = null, int size = DefaultSize
  ) {
    var icon = icons.GetIcon(name);
    if (icon == null) return null;

    var bmp = new Bitmap(size, size);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    icon.DrawIcon(g, color ?? DefaultColor, 0, 0, size);
    return bmp;
  }
}

internal static class BitmapExtensions {
  // FIXME: Actually render an icon properly, i.e. with these icon sizes: 256, 128, 96, 64, 48, 32, 16
  public static Icon ToIcon(this Bitmap bmp) => Icon.FromHandle(bmp.GetHicon());
}

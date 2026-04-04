using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenRCT3;

internal static class Icons {
  public static Icon LoadEmbedded(string resourceName) {
    var asm = Assembly.GetExecutingAssembly();
    using var stream = asm.GetManifestResourceStream(resourceName);
    if (stream == null) return null!;

    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    var icoData = ms.ToArray();

    var multiIcon = new Icon(new MemoryStream(icoData));
    foreach (var size in new[] { 16, 32, 48, 64, 128, 256 }) {
      using var icon = new Icon(multiIcon, size, size);
      if (icon.Width != size || icon.Height != size) continue;
    }

    return multiIcon;
  }

  public static int DefaultSize = 16;

  public static Image ToImage(Icon icon, int? desiredSize = null) {
    var size = desiredSize ?? DefaultSize;

    var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bitmap);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.DrawIcon(icon, 0, 0);

    return bitmap;
  }
}

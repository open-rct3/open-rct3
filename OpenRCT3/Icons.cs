using OpenTK.Windowing.Common.Input;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Image = OpenTK.Windowing.Common.Input.Image;

namespace OpenRCT3;

internal static class Icons {
  public static WindowIcon LoadEmbedded(string resourceName) {
    var asm = Assembly.GetExecutingAssembly();
    using var stream = asm.GetManifestResourceStream(resourceName);
    if (stream == null) return null!;

    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    var icoData = ms.ToArray();

    var images = new List<Image>();
    using var multiIcon = new Icon(new MemoryStream(icoData));

    foreach (var size in new[] { 16, 32, 48, 64, 128, 256 }) {
      using var icon = new Icon(multiIcon, size, size);
      if (icon.Width != size || icon.Height != size) continue;
      images.Add(ToImage(icon));
    }

    return new WindowIcon(images.ToArray());
  }

  public static Image ToImage(Icon icon) {
    var size = icon.Width;
    var rect = new Rectangle(0, 0, size, size);
    using var bmp = icon.ToBitmap();
    using var converted = bmp.Clone(rect, PixelFormat.Format32bppArgb);
    var bits = converted.LockBits(rect, ImageLockMode.ReadOnly, converted.PixelFormat);
    var pixels = new byte[bits.Stride * bits.Height];
    Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length);
    converted.UnlockBits(bits);

    // GDI+ Format32bppArgb is BGRA; OpenTK Image expects RGBA
    // Swap only if format is indeed BGRA
    if (converted.PixelFormat == PixelFormat.Format32bppArgb) {
      for (var i = 0; i < pixels.Length; i += 4)
        (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
    }

    return new Image(size, size, pixels);
  }
}

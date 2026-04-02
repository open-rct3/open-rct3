using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace OvlTestBench;

public partial class OvlTestBenchForm {
  private const int IconSize = 16;

  private void InitializeComponentIcons() {
    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();

    var defaultColor = new DuoToneColor(Color.FromArgb(64, 64, 64), Color.FromArgb(185, 185, 185));
    var successColor = new DuoToneColor(Color.FromArgb(0x4C, 0xAF, 0x50), Color.FromArgb(0x81, 0xC7, 0x84));
    var infoColor = new DuoToneColor(Color.FromArgb(0x19, 0x76, 0xD2), Color.White);

    var formIcon = RenderIconResource(icons, "TestTube", infoColor, [16, 24, 32, 64]);
    if (formIcon == null) {
      var fallbackBmp = RenderIcon(icons, "TestTube", infoColor, 32);
      if (fallbackBmp != null) Icon = BitmapToIcon(fallbackBmp);
    } else Icon = formIcon;

    startStopButton.Image = RenderIcon(icons, "Play", successColor);
    diagButton.Image = RenderIcon(icons, "ClipboardCheck", infoColor);
  }

  private static Icon? RenderIconResource(IEmbeddedIcons icons, string name, DuoToneColor color, int[] sizes) {
    var bitmaps = new List<Bitmap>();

    try {
      foreach (var size in sizes) {
        var bmp = RenderIcon(icons, name, color, size);
        if (bmp != null) bitmaps.Add(bmp);
      }

      if (bitmaps.Count == 0) return null;

      // Create ICO format in memory with multiple resolutions
      using var ms = new MemoryStream();
      WriteIconToStream(ms, bitmaps);
      ms.Seek(0, SeekOrigin.Begin);
      return new Icon(ms);
    } finally {
      // Dispose bitmaps after icon creation
      foreach (var bmp in bitmaps) {
        bmp?.Dispose();
      }
    }
  }

  private static void WriteIconToStream(Stream stream, List<Bitmap> bitmaps) {
    // ICO file header
    stream.WriteByte(0);
    stream.WriteByte(0);
    stream.WriteByte(1);
    stream.WriteByte(0);

    // Number of images (little-endian)
    byte[] imageCount = BitConverter.GetBytes((ushort)bitmaps.Count);
    stream.Write(imageCount, 0, 2);

    // Image directory entries
    int dataOffset = 6 + (bitmaps.Count * 16);
    var entries = new List<(byte Width, byte Height, int DataOffset, int DataSize)>();

    // First pass: calculate offsets
    int currentOffset = dataOffset;
    foreach (var bmp in bitmaps) {
      using var bmpStream = new MemoryStream();
      // Save as PNG for better quality
      bmp.Save(bmpStream, System.Drawing.Imaging.ImageFormat.Png);
      int size = (int)bmpStream.Length;
      entries.Add(((byte)bmp.Width, (byte)bmp.Height, currentOffset, size));
      currentOffset += size;
    }

    // Write directory entries
    foreach (var (width, height, offset, size) in entries) {
      stream.WriteByte(width);
      stream.WriteByte(height);
      stream.WriteByte(0); // color count
      stream.WriteByte(0); // reserved
      stream.WriteByte(1); // color planes
      stream.WriteByte(0);
      stream.WriteByte(32); // bits per pixel
      stream.WriteByte(0);
      byte[] sizeBytes = BitConverter.GetBytes(size);
      stream.Write(sizeBytes, 0, 4);
      byte[] offsetBytes = BitConverter.GetBytes(offset);
      stream.Write(offsetBytes, 0, 4);
    }

    // Write image data
    foreach (var bmp in bitmaps) {
      using var bmpStream = new MemoryStream();
      bmp.Save(bmpStream, System.Drawing.Imaging.ImageFormat.Png);
      bmpStream.WriteTo(stream);
    }
  }

  Icon BitmapToIcon(Bitmap bmp) {
    nint hIcon = nint.Zero;
    try {
      hIcon = bmp.GetHicon();
      return Icon.FromHandle(hIcon);
    } finally {
      if (hIcon != nint.Zero) DestroyIcon(hIcon);
    }
  }

  [DllImport("user32.dll", CharSet = CharSet.Auto)]
  static extern bool DestroyIcon(IntPtr handle);

  private static Bitmap? RenderIcon(IEmbeddedIcons icons, string name, DuoToneColor color, int? size = null) {
    var icon = icons.GetIcon(name);
    if (icon == null) return null;
    var iconSize = size ?? IconSize;
    var bmp = new Bitmap(iconSize, iconSize);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    icon.DrawIcon(g, color, 0, 0, iconSize);
    return bmp;
  }
}

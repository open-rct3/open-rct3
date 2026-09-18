// Shared tex/flic/btbl decode plumbing.
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.Runtime.InteropServices;

namespace OpenCobra.OVL.Files;

internal static class BinaryReaderExtensions {
  public static BitmapTable ReadBitmapTable(this BinaryReader reader) =>
    reader.Read<BitmapTable>(out var table) != 0 ? table : default;

  public static FlicHeader ReadFlicHeader(this BinaryReader reader) =>
    reader.Read<FlicHeader>(out var flic) != 0 ? flic : default;

  /// <summary>
  /// Reads a structure of type <typeparamref name="T"/> from the binary reader and returns the number of bytes read.
  /// </summary>
  public static uint Read<T>(this BinaryReader reader, out T data) where T : struct {
    var size = Marshal.SizeOf<T>();
    var bytes = reader.ReadBytes(size);
    if (bytes.Length != size) {
      data = default!;
      return 0;
    }

    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    try {
      var ptr = handle.AddrOfPinnedObject();
      if (ptr == nint.Zero) {
        data = default!;
        return 0;
      }

      data = Marshal.PtrToStructure<T>(ptr)!;
      return Convert.ToUInt32(size);
    } finally {
      handle.Free();
    }
  }
}

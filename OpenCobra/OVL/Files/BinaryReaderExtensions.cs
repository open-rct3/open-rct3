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
  public static uint Read<T>(this BinaryReader reader, out T data) {
    byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    var ptr = handle.AddrOfPinnedObject();
    if (ptr == nint.Zero) {
      data = default!;
      return 0;
    }
    var structure = (T?)Marshal.PtrToStructure(ptr, typeof(T));
    if (structure == null) data = default!;
    handle.Free();

    data = structure!;
    return Convert.ToUInt32(bytes.Length);
  }
}

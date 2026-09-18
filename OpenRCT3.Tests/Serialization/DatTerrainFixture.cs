// DatTerrainFixture
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Text;

namespace OpenRCT3.Tests.Serialization;

/// <summary>Creates the smallest saved terrain accepted by <see cref="DatTerrainReader"/>.</summary>
internal static class DatTerrainFixture {
  /// <summary>Builds a deterministic one-cell terrain fixture for probes that need a saved park.</summary>
  public static byte[] BuildMinimalTerrainBytes() {
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true)) {
      writer.Write(1u);
      WriteAscii16(writer, "Landscape");
      writer.Write(1u);
      WriteAscii16(writer, "EngineTerrain");
      WriteAscii16(writer, "GE_Terrain");
      writer.Write(0u);
      writer.Write(0u);
      writer.Write(1u);
      writer.Write(0u);
      writer.Write(1ul);

      using var payload = new MemoryStream();
      using (var payloadWriter = new BinaryWriter(payload, Encoding.ASCII, leaveOpen: true)) {
        payloadWriter.Write(Convert.ToByte(1));
        payloadWriter.Write(Convert.ToByte(1));
        payloadWriter.Write(-16f);
        payloadWriter.Write(-20f);
        payloadWriter.Write(4f);
        payloadWriter.Write(4f);
        payloadWriter.Write(1f);
        payloadWriter.Write(2f);
        payloadWriter.Write(3f);
        payloadWriter.Write(4f);
        payloadWriter.Write(Convert.ToByte(5));
        payloadWriter.Write(Convert.ToByte(6));
        payloadWriter.Write(Convert.ToByte(18));
        payloadWriter.Write(Convert.ToByte(19));
        payloadWriter.Write(Convert.ToByte(20));
        payloadWriter.Write(Convert.ToByte(21));
        payloadWriter.Write(Convert.ToByte(22));
        payloadWriter.Write(Convert.ToByte(23));
      }
      writer.Write(Convert.ToUInt32(payload.Length));
      writer.Write(payload.ToArray());
    }
    return stream.ToArray();
  }

  private static void WriteAscii16(BinaryWriter writer, string value) {
    var bytes = Encoding.ASCII.GetBytes(value);
    writer.Write(Convert.ToUInt16(bytes.Length));
    writer.Write(bytes);
  }
}

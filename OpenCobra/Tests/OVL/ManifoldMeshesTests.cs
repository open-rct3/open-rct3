using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class ManifoldMeshesTests {
  // ManifoldMesh header is 48 bytes: bbox_min (position 12 + unk04 4 = 16), bbox_max (16),
  // vertex_count(4), mainfoldface_count(4), vertices*(4), mainfoldface_indices*(4).
  private static byte[] MakeSyntheticHeader(uint vertexCount = 4, uint faceCount = 2) {
    var data = new byte[48];
    // bbox_min.position = (-1, -1, -1)
    Buffer.BlockCopy(BitConverter.GetBytes(-1f), 0, data, 0, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(-1f), 0, data, 4, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(-1f), 0, data, 8, 4);
    // bbox_max.position = (1, 1, 1)
    Buffer.BlockCopy(BitConverter.GetBytes(1f), 0, data, 16, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(1f), 0, data, 20, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(1f), 0, data, 24, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(vertexCount), 0, data, 32, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(faceCount), 0, data, 36, 4);
    return data;
  }

  [Test]
  public void ParseHeader_SyntheticStruct_DecodesBoundingBoxAndCounts() {
    var data = MakeSyntheticHeader(vertexCount: 9, faceCount: 12);

    using (Assert.EnterMultipleScope()) {
      Assert.That(BitConverter.ToSingle(data, 0), Is.EqualTo(-1f), "bbox_min.x");
      Assert.That(BitConverter.ToSingle(data, 16), Is.EqualTo(1f), "bbox_max.x");
      Assert.That(BitConverter.ToUInt32(data, 32), Is.EqualTo(9u), "vertex_count");
      Assert.That(BitConverter.ToUInt32(data, 36), Is.EqualTo(12u), "mainfoldface_count");
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void TryExtractOne_WaterFlatProxy_DecodesVerticesAndFaces() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Path", "UnderWater", "WaterFlat.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"WaterFlat.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var meshes = ManifoldMeshes.Extract(ovl);

    using (Assert.EnterMultipleScope()) {
      Assert.That(meshes, Is.Not.Empty);
      var mesh = meshes.FirstOrDefault(m => m.Name == "WaterFlatHi_Proxy");
      Assert.That(mesh, Is.Not.Default);
      Assert.That(mesh.Vertices, Has.Count.EqualTo(9));
      Assert.That(mesh.Faces, Has.Count.EqualTo(12));
    }
  }
}

using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class SceneryItemVisualsTests {
  // SceneryItemVisual_V is 52 bytes: sivflags(4), sway(4), brightness(4), unk4(4), scale(4),
  // lod_count(4), lods*(4), unk6-11(6*4=24).
  private static byte[] MakeSyntheticVisualV(
    uint sivflags = 0, float sway = 0.2f, float brightness = 0.8f, float scale = 0.4f, uint lodCount = 1
  ) {
    var data = new byte[52];
    var offset = 0;
    Buffer.BlockCopy(BitConverter.GetBytes(sivflags), 0, data, offset, 4); offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(sway), 0, data, offset, 4); offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(brightness), 0, data, offset, 4); offset += 4;
    offset += 4; // unk4
    Buffer.BlockCopy(BitConverter.GetBytes(scale), 0, data, offset, 4); offset += 4;
    Buffer.BlockCopy(BitConverter.GetBytes(lodCount), 0, data, offset, 4);
    return data;
  }

  // SceneryItemVisualLOD is 72 bytes: type(4), lod_name*(4), shs_ref*(4), unk2(4), bsh_ref*(4),
  // unk4(4), ftx_ref*(4), txs_ref*(4), unk7-12(6*4=24), distance(4), animation_count(4), unk14(4),
  // animations_ref*(4).
  private static byte[] MakeSyntheticLod(uint meshType = 0, float distance = 40f, uint animationCount = 0) {
    var data = new byte[72];
    Buffer.BlockCopy(BitConverter.GetBytes(meshType), 0, data, 0, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(distance), 0, data, 56, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(animationCount), 0, data, 60, 4);
    return data;
  }

  [Test]
  public void ParseVisualV_SyntheticStruct_DecodesAllFields() {
    var data = MakeSyntheticVisualV(sivflags: 0x00000005, sway: 0.2f, brightness: 0.8f, scale: 0.4f, lodCount: 2);

    using (Assert.EnterMultipleScope()) {
      Assert.That((SvdFlags) BitConverter.ToUInt32(data, 0), Is.EqualTo(SvdFlags.Greenery | SvdFlags.Rotation));
      Assert.That(BitConverter.ToSingle(data, 4), Is.EqualTo(0.2f).Within(0.0001f));
      Assert.That(BitConverter.ToSingle(data, 8), Is.EqualTo(0.8f).Within(0.0001f));
      Assert.That(BitConverter.ToSingle(data, 16), Is.EqualTo(0.4f).Within(0.0001f));
      Assert.That(BitConverter.ToUInt32(data, 20), Is.EqualTo(2u));
    }
  }

  [Test]
  public void ParseLod_SyntheticStructPerMeshType_DecodesDistanceAndType() {
    var staticLod = MakeSyntheticLod(meshType: 0, distance: 40f);
    var boneLod = MakeSyntheticLod(meshType: 3, distance: 100f);
    var billboardLod = MakeSyntheticLod(meshType: 4, distance: 2000f);

    using (Assert.EnterMultipleScope()) {
      Assert.That((SvdLodType) BitConverter.ToUInt32(staticLod, 0), Is.EqualTo(SvdLodType.StaticShape));
      Assert.That(BitConverter.ToSingle(staticLod, 56), Is.EqualTo(40f));

      Assert.That((SvdLodType) BitConverter.ToUInt32(boneLod, 0), Is.EqualTo(SvdLodType.BoneShape));
      Assert.That(BitConverter.ToSingle(boneLod, 56), Is.EqualTo(100f));

      Assert.That((SvdLodType) BitConverter.ToUInt32(billboardLod, 0), Is.EqualTo(SvdLodType.Billboard));
      Assert.That(BitConverter.ToSingle(billboardLod, 56), Is.EqualTo(2000f));
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromBigVase_DecodesStaticShapeLods() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Themed", "Atlantis", "Scenery", "Vases", "BigVase.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"BigVase.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var svds = SceneryItemVisuals.Extract(ovl);

    var bigVase = svds.FirstOrDefault(s => s.Name == "BigVase");
    using (Assert.EnterMultipleScope()) {
      Assert.That(bigVase, Is.Not.Default, "Expected a decoded 'BigVase' SVD");
      Assert.That(bigVase.Lods, Has.Count.EqualTo(4), "BigVase is known to have 4 LODs (Hi/Med/Low/UltraLow)");
      Assert.That(bigVase.Lods, Has.All.Matches<LodEntry>(l => l.MeshType == SvdLodType.StaticShape));
      // Every LOD's StaticShapeRef must resolve within this single-archive pair, since the referenced
      // shs symbols (BigVaseHiLOD etc.) live in the same BigVase.common/unique.ovl pair.
      Assert.That(bigVase.Lods, Has.All.Matches<LodEntry>(l => l.StaticShapeRef != null));
      Assert.That(bigVase.Lods.Select(l => l.StaticShapeRef), Is.EquivalentTo([
        "BigVaseHiLOD", "BigVaseMedLOD", "BigVaseLowLOD", "BigVaseUltraLowLOD"
      ]));
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromBeachTorch01_DecodesTwoSvdsSharingLods() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Themed", "IslandParadise", "PathExtras", "Torches", "BeachTorch01.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"BeachTorch01.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var svds = SceneryItemVisuals.Extract(ovl);

    Assert.That(svds.Select(s => s.Name), Is.EquivalentTo(["BeachTorchScenery01", "BeachTorch01"]));
    foreach (var svd in svds)
      Assert.That(svd.Lods, Has.Count.EqualTo(4), $"{svd.Name}: expected 4 LODs");
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_StaticShapeLods_ResolveFtxRefWhenTextured() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Themed", "IslandParadise", "PathExtras", "Torches", "BeachTorch01.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"BeachTorch01.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var shapes = StaticShapes.Extract(ovl);

    // BeachTorch1Hlod/Mlod/Llod meshes are known to reference the "BeachTorch" flexi-texture -
    // confirms StaticMesh.FtxRef resolves via the symbol-reference table against real data.
    var texturedMeshes = shapes.SelectMany(s => s.Meshes).Where(m => m.FtxRef != null).ToList();
    Assert.That(texturedMeshes, Is.Not.Empty, "Expected at least one FtxRef-textured mesh");
    Assert.That(texturedMeshes, Has.All.Matches<StaticMesh>(m => m.FtxRef == "BeachTorch"));
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_WaterFlat_DecodesProxyManifoldMesh() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Path", "UnderWater", "WaterFlat.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"WaterFlat.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var svd = SceneryItemVisuals.Extract(ovl).FirstOrDefault(s => s.Name == "WaterFlat");

    using (Assert.EnterMultipleScope()) {
      Assert.That(svd, Is.Not.Default, "Expected a decoded 'WaterFlat' SVD");
      Assert.That((svd.Flags & SvdFlags.SoakedOrWild), Is.Not.EqualTo((SvdFlags) 0), "Expected Soaked or Wild flag set");
      Assert.That(svd.ProxyMesh, Is.Not.Null, "Expected a resolved ProxyMesh");
      Assert.That(svd.ProxyMesh!.Value.Vertices, Has.Count.EqualTo(9));
      Assert.That(svd.ProxyMesh.Value.Faces, Has.Count.EqualTo(12));
    }
  }
}

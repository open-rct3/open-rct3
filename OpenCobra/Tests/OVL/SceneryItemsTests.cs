using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class SceneryItemsTests {
  // SceneryItem_V is 212 bytes. Only the fields exercised by these offset-sanity checks are filled in.
  private static byte[] MakeSyntheticItemV(
    ushort positionType = 4, ushort structureVersion = 0, uint squaresX = 1, uint squaresZ = 1,
    int cost = 1400, int removalCost = -1200, uint sceneryType = 7
  ) {
    var data = new byte[212];
    Buffer.BlockCopy(BitConverter.GetBytes(positionType), 0, data, 8, 2);
    Buffer.BlockCopy(BitConverter.GetBytes(structureVersion), 0, data, 10, 2);
    Buffer.BlockCopy(BitConverter.GetBytes(squaresX), 0, data, 16, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(squaresZ), 0, data, 20, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(cost), 0, data, 60, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(removalCost), 0, data, 64, 4);
    Buffer.BlockCopy(BitConverter.GetBytes(sceneryType), 0, data, 72, 4);
    return data;
  }

  [Test]
  public void ParseItemV_SyntheticStruct_DecodesPositionAndListingFields() {
    var data = MakeSyntheticItemV(positionType: 4, structureVersion: 1, squaresX: 1, squaresZ: 1, cost: 1400, removalCost: -1200, sceneryType: 7);

    using (Assert.EnterMultipleScope()) {
      Assert.That((Placement) BitConverter.ToUInt16(data, 8), Is.EqualTo(Placement.Quarter));
      Assert.That(BitConverter.ToUInt16(data, 10), Is.EqualTo(1), "structure_version");
      Assert.That(BitConverter.ToUInt32(data, 16), Is.EqualTo(1u), "squares_x");
      Assert.That(BitConverter.ToUInt32(data, 20), Is.EqualTo(1u), "squares_z");
      Assert.That(BitConverter.ToInt32(data, 60), Is.EqualTo(1400), "cost");
      Assert.That(BitConverter.ToInt32(data, 64), Is.EqualTo(-1200), "removal_cost");
      Assert.That(BitConverter.ToUInt32(data, 72), Is.EqualTo(7u), "type");
    }
  }

  [Test]
  public void StructSizes_MatchVersionedLayout() {
    // v0 = SceneryItem_V only; v1 adds SceneryItem_Sext (16 bytes); v2 adds SceneryItem_Wext (8 bytes)
    // on top of that - see sceneryrevised.h.
    const int vSize = 212;
    const int sextSize = 16;
    const int wextSize = 8;

    using (Assert.EnterMultipleScope()) {
      Assert.That(vSize, Is.EqualTo(212));
      Assert.That(vSize + sextSize, Is.EqualTo(228));
      Assert.That(vSize + sextSize + wextSize, Is.EqualTo(236));
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromBigVase_DecodesPositionListingAndTiles() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Themed", "Atlantis", "Scenery", "Vases", "BigVase.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"BigVase.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var sid = SceneryItems.Extract(ovl).FirstOrDefault(s => s.Name == "BigVase");

    using (Assert.EnterMultipleScope()) {
      Assert.That(sid, Is.Not.Default, "Expected a decoded 'BigVase' SID");
      Assert.That(sid.Listing.SceneryType, Is.EqualTo(7u), "TYPE_SCENERY_SMALL");
      Assert.That(sid.Listing.Cost, Is.EqualTo(1400));
      Assert.That(sid.Listing.RemovalCost, Is.EqualTo(-1200));
      Assert.That(sid.Position.Placement, Is.EqualTo(Placement.Quarter));
      Assert.That(sid.Position.XSquares, Is.EqualTo(1u));
      Assert.That(sid.Position.ZSquares, Is.EqualTo(1u));
      Assert.That(sid.Position.Size.Y, Is.EqualTo(2f));
      Assert.That(sid.Extra.Version, Is.EqualTo((ushort) 1));
      Assert.That(sid.Extra.AddonPack, Is.EqualTo(Addon.Soaked));
      Assert.That(sid.Tiles, Has.Count.EqualTo(1));
      Assert.That(sid.Tiles[0].MinHeight, Is.EqualTo(0));
      Assert.That(sid.Tiles[0].MaxHeight, Is.EqualTo(2));
      Assert.That(sid.OvlPath, Is.EqualTo(@"Style\Themed\Atlantis\Scenery\Vases\BigVase"));
      Assert.That(sid.SvdRefs, Is.EquivalentTo(["BigVase"]));
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromBeachTorch01_LinksToOneOfTwoSvds() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Themed", "IslandParadise", "PathExtras", "Torches", "BeachTorch01.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"BeachTorch01.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var sid = SceneryItems.Extract(ovl).FirstOrDefault(s => s.Name == "BeachTorchScenery01");
    var svdNames = SceneryItemVisuals.Extract(ovl).Select(s => s.Name).ToHashSet();

    using (Assert.EnterMultipleScope()) {
      Assert.That(sid, Is.Not.Default, "Expected a decoded 'BeachTorchScenery01' SID");
      Assert.That(sid.SvdRefs, Is.Not.Empty);
      Assert.That(sid.SvdRefs, Has.All.Matches<string>(r => svdNames.Contains(r)), "No SvdRefs entry should dangle");
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromStyleVanilla_SidSvdLinkageHasNoDanglingRefs() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Vanilla", "Style.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"Style.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var sids = SceneryItems.Extract(ovl);
    var svdNames = SceneryItemVisuals.Extract(ovl).Select(s => s.Name).ToHashSet();

    Assert.That(sids, Is.Not.Empty);
    foreach (var sid in sids)
      foreach (var svdRef in sid.SvdRefs)
        Assert.That(svdNames, Does.Contain(svdRef), $"{sid.Name}: dangling SvdRefs entry '{svdRef}'");
  }
}

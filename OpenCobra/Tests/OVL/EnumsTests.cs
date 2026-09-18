using NUnit.Framework;
using OpenCobra.OVL;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class EnumsTests {
  [Test]
  public void SvdLodType_MatchesOriginalCppLodTypeValues() {
    Assert.That((int) SvdLodType.StaticShape, Is.EqualTo(0));
    Assert.That((int) SvdLodType.BoneShape, Is.EqualTo(3));
    Assert.That((int) SvdLodType.Billboard, Is.EqualTo(4));
  }

  [Test]
  public void SvdFlags_NoShadowAndFlower_ShareTheSameBitValue() {
    Assert.That((uint) SvdFlags.NoShadow, Is.EqualTo((uint) SvdFlags.Flower));
    Assert.That((uint) SvdFlags.NoShadow, Is.EqualTo(0x00000002u));
  }
}

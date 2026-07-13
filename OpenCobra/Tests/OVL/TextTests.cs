using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class TextTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void Extract_FromStyleVanilla_DecodesUtf16Strings() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Vanilla", "Style.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"Style.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var texts = Text.Extract(ovl);

    using (Assert.EnterMultipleScope()) {
      Assert.That(texts, Is.Not.Empty);
      Assert.That(texts, Contains.Key("StyleName"));
      Assert.That(texts["StyleName"], Is.Not.Empty);
    }
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void TryExtractOne_MatchesBulkExtract() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "Style", "Vanilla", "Style.common.ovl");
    if (!File.Exists(path)) Assert.Inconclusive($"Style.common.ovl not found at {path}");

    using var ovl = Ovl.Load(path);
    var file = ovl.Find("StyleName", FileType.Text);
    Assert.That(file, Is.Not.Null);

    var value = Text.TryExtractOne(ovl, file!);
    var bulk = Text.Extract(ovl);

    Assert.That(value, Is.EqualTo(bulk["StyleName"]));
  }
}

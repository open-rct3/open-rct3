using OpenRCT3.Platforms;

namespace OpenRCT3.Tests.Platforms;

[TestFixture]
public class AppConfigTests {
  [Test]
  public void ResolveApplicationDataFolder_UsesAbsoluteConfiguredPath() {
    var configured = Path.Combine(Path.GetTempPath(), "openrct3-native-smoke", "..", "isolated");

    var resolved = AppConfig.ResolveApplicationDataFolder(configured, "ignored");

    Assert.That(resolved, Is.EqualTo(Path.GetFullPath(configured)));
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  public void ResolveApplicationDataFolder_UsesFallbackWhenUnset(string? configured) {
    const string fallback = "known-folder";

    Assert.That(
      AppConfig.ResolveApplicationDataFolder(configured, fallback),
      Is.EqualTo(fallback));
  }

  [Test]
  public void ResolveApplicationDataFolder_RejectsRelativeConfiguredPath() {
    var error = Assert.Throws<InvalidOperationException>(new Action(() =>
      AppConfig.ResolveApplicationDataFolder("relative\\profile", "ignored")));

    Assert.That(error!.Message, Does.Contain(AppConfig.AppDataPathEnvironmentVariable));
  }
}

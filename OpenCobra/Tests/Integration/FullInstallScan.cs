// FullInstallScan - scratch verification tool, not part of the fix plan; delete after use.
using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class FullInstallScan {
  private static string? Rct3Path() => Environment.GetEnvironmentVariable("RCT3_PATH");

  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [Explicit("Full-install scan, ~5 min. Run manually via --filter.")]
  public void ScanAllCommonOvls() {
    var rct3 = Rct3Path()!;
    var files = Directory.GetFiles(rct3, "*.common.ovl", SearchOption.AllDirectories);
    var solutionDir = AppContext.BaseDirectory;
    while (!Directory.Exists(Path.Combine(solutionDir, ".agents")))
      solutionDir = Path.GetDirectoryName(solutionDir) ?? throw new DirectoryNotFoundException("Could not find repo root (.agents dir)");
    var outPath = Path.Combine(solutionDir, ".agents", "summaries", "ovl-texture-scan.csv");
    using var writer = new StreamWriter(outPath, append: false);
    // Textures.Extract now includes ftx (FlexiTexture) decoding internally, so its Count is
    // already the grand total per file - do not add a separate ftx pass on top of it (that would
    // double-count every ftx entry).
    writer.WriteLine("file,decoded,ftx_entries,crash");

    var totalDecoded = 0;
    var totalFtxEntries = 0;
    var fileCount = 0;
    var crashes = 0;
    foreach (var file in files) {
      fileCount++;
      try {
        using var ovl = Ovl.Load(file);
        var decoded = Textures.Extract(ovl).Count;
        totalDecoded += decoded;

        var ftxEntries = ovl.Keys.Count(k => k.Type == FileType.FlexibleTexture);
        totalFtxEntries += ftxEntries;

        var rel = Path.GetRelativePath(rct3, file);
        writer.WriteLine($"{rel},{decoded},{ftxEntries},");
      } catch (Exception ex) {
        crashes++;
        var rel = Path.GetRelativePath(rct3, file);
        writer.WriteLine($"{rel},,,{ex.GetType().Name}");
      }
    }
    writer.Flush();

    TestContext.Out.WriteLine(
      $"Scanned {fileCount} files, {crashes} crashes. " +
      $"Total decoded (tex/flic/btbl/ftx, unified)={totalDecoded}. ftx entries seen={totalFtxEntries}.");
    TestContext.Out.WriteLine($"Per-file CSV: {outPath}");
  }
}

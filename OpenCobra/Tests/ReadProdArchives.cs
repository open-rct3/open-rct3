// ReadProdArchives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace OVL.Tests;

internal class OvlTestConfig {
  [System.Text.Json.Serialization.JsonPropertyName("extraOvls")]
  public Dictionary<string, string> ExtraOvls { get; set; } = new();
}

[TestFixture]
[CancelAfter(300000)]
public class ReadProdArchives {
  private Assembly assembly;
  private OvlTestConfig? config;

  [SetUp]
  public void Setup() {
    assembly = Assembly.GetExecutingAssembly();
    var configPath = Path.Combine(
      Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
      "ovl-tests.local.json"
    );
    if (System.IO.File.Exists(configPath)) {
      var json = System.IO.File.ReadAllText(configPath);
      config = JsonSerializer.Deserialize<OvlTestConfig>(json);
    }
  }

  private Stream OpenResource(string resourcePath) {
    if (resourcePath.StartsWith("OVL.Tests.")) {
      var stream = assembly.GetManifestResourceStream(resourcePath);
      Assert.That(stream, Is.Not.Null, $"Embedded resource not found: {resourcePath}");
      return stream!;
    }

    Assert.That(System.IO.File.Exists(resourcePath), Is.True, $"OVL file not found: {resourcePath}");
    return System.IO.File.OpenRead(resourcePath);
  }

  public static IEnumerable LocalOvls {
    get {
      var configPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ovl-tests.local.json"
      );
      if (!System.IO.File.Exists(configPath)) yield break;

      var json = System.IO.File.ReadAllText(configPath);
      var config = JsonSerializer.Deserialize<OvlTestConfig>(json);
      if (config?.ExtraOvls == null) yield break;

      foreach (var (name, glob) in config.ExtraOvls) {
        var dir = Path.GetDirectoryName(glob)!;
        var pattern = "*" + Path.GetExtension(glob);

        if (!System.IO.Directory.Exists(dir)) continue;

        var matches = System.IO.Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
        foreach (var file in matches) {
          var fileName = Path.GetFileName(file);
          var type = fileName.Contains(".common.") ? OvlType.Common : OvlType.Unique;
          yield return new TestCaseData(file, type);
        }
      }
    }
  }

  [Test]
  [TestCaseSource(typeof(ReadProdArchives), nameof(LocalOvls))]
  public void ReadLocalOvl(string filePath, OvlType type) {
    var stream = OpenResource(filePath);
    Assert.DoesNotThrow(() => {
      var ovl = Ovl.Read(stream, Path.GetFileName(filePath));
      Assert.That(ovl, Is.InstanceOf<Ovl>());
      Assert.That(ovl.Type, Is.EqualTo(type));
    });
  }

  [Test]
  [TestCaseSource(typeof(ReadProdArchives), nameof(LocalOvls))]
  public void LocalOvlHasLoaderHeaders(string filePath, OvlType type) {
    var ovl = Ovl.Read(OpenResource(filePath), Path.GetFileName(filePath));
    Assert.That(ovl.LoaderHeaders, Is.Not.Null);
    // Some production OVLs (e.g. SharedTextures, Objects) have fileTypeCount == 0
    // and legitimately contain no loader headers.
    if (ovl.CommonData?.LoaderHeaders.Length > 0) {
      Assert.That(ovl.LoaderHeaders, Is.Not.Empty);
    }
  }

  [Test]
  [TestCaseSource(typeof(ReadProdArchives), nameof(PairedOvls))]
  public void PairedArchiveHasLoaderEntries(string commonPath, string uniquePath) {
    var ovl = Ovl.Load(commonPath);
    Assert.That(ovl.LoaderEntries, Is.Not.Null);
    // Some production paired archives (e.g. SharedTextures, Objects) have fileTypeCount == 0
    // and legitimately contain no loader entries.
    if (ovl.CommonData?.LoaderHeaders.Length > 0) {
      Assert.That(ovl.LoaderEntries.Count, Is.GreaterThan(0),
        "Paired archive should have loader entries");
    }
  }

  private static IEnumerable<string> ReadRawStrings(byte[] data) {
    var result = new List<string>();
    var pos = 0;
    while (pos < data.Length) {
      var end = Array.IndexOf(data, (byte)0, pos);
      if (end < 0) break;
      if (end > pos) {
        result.Add(System.Text.Encoding.ASCII.GetString(data, pos, end - pos));
      }
      pos = end + 1;
    }
    return result;
  }

  public static IEnumerable PairedOvls {
    get {
      var configPath = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
        "ovl-tests.local.json"
      );
      if (!System.IO.File.Exists(configPath)) yield break;

      var json = System.IO.File.ReadAllText(configPath);
      var config = JsonSerializer.Deserialize<OvlTestConfig>(json);
      if (config?.ExtraOvls == null) yield break;

      var commonFiles = new List<(string path, string name)>();
      var uniqueFiles = new List<(string path, string name)>();

      foreach (var (name, glob) in config.ExtraOvls) {
        var dir = Path.GetDirectoryName(glob)!;
        var pattern = "*" + Path.GetExtension(glob);

        if (!System.IO.Directory.Exists(dir)) continue;

        var matches = System.IO.Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
        foreach (var file in matches) {
          var fileName = Path.GetFileName(file);
          if (fileName.Contains(".common.")) {
            commonFiles.Add((file, fileName));
          } else if (fileName.Contains(".unique.")) {
            uniqueFiles.Add((file, fileName));
          }
        }
      }

      var commonPrefixes = commonFiles.Select(c => c.name.Split('.')[0]).ToHashSet();
      foreach (var common in commonFiles) {
        var prefix = common.name.Split('.')[0];
        var matchingUnique = uniqueFiles.FirstOrDefault(u => u.name.StartsWith(prefix));
        if (!string.IsNullOrEmpty(matchingUnique.path)) {
          yield return new TestCaseData(common.path, matchingUnique.path);
        }
      }
    }
  }
}
using System;
using OVL;

namespace OvlTestBench.Tests;

public record OvlTest(string Name, Action<OvlPair> Test);

public static class LoadOvls {
  public static readonly OvlTest[] All = [
    new("ReadLocalOvl", pair => {
      foreach (var file in pair.Files) {
        using var stream = System.IO.File.OpenRead(file.Path);
        var ovl = Ovl.Read(stream, file.Path);
        Assert.That(ovl.Type == file.Type, $"{System.IO.Path.GetFileName(file.Path)}: expected {file.Type}, got {ovl.Type}");
      }
    }),
    new("LocalOvlHasLoaderHeaders", pair => {
      foreach (var file in pair.Files) {
        using var stream = System.IO.File.OpenRead(file.Path);
        var ovl = Ovl.Read(stream, file.Path);
        if (ovl.CommonData?.LoaderHeaders.Length > 0)
          Assert.That(ovl.LoaderHeaders.Length > 0, $"{System.IO.Path.GetFileName(file.Path)}: expected headers but got none");
      }
    }),
    new("PairedArchiveHasLoaderEntries", pair => {
      if (!string.IsNullOrEmpty(pair.CommonPath)) {
        var ovl = Ovl.Load(pair.CommonPath);
        if (ovl.CommonData?.LoaderHeaders.Length > 0)
          Assert.That(ovl.LoaderEntries.Count > 0, "Expected loader entries but got none");
      }
    }),
  ];
}

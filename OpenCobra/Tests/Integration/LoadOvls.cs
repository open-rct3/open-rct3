using System;
using System.IO;
using OpenCobra.OVL;

namespace OvlTestBench.Tests;

public record OvlTest(string Name, Action<OvlPair> Test);

public static class LoadOvls {
  public readonly static OvlTest[] All = [
    new("ReadLocalOvl", pair => {
      foreach (var file in pair.Files) {
        var ovl = Ovl.Load(file.Path);
        Assert.That(ovl.Count > 0, $"{Path.GetFileName(file.Path)}: expected non-empty archive");
      }
    }),
    new("LocalOvlHasLoaders", pair => {
      foreach (var file in pair.Files) {
        var ovl = Ovl.Load(file.Path);
        Assert.That(ovl.Keys.Count > 0, $"{System.IO.Path.GetFileName(file.Path)}: expected headers but got none");
      }
    }),
    new("PairedArchiveHasResources", pair => {
      if (string.IsNullOrEmpty(pair.CommonPath)) return;
      var ovl = Ovl.Load(pair.CommonPath);
      Assert.That(ovl.Values.Count > 0, "Expected loader entries but got none");
    }),
  ];
}

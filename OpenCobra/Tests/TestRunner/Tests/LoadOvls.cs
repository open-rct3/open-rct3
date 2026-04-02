using System;
using System.Collections.Generic;
using OVL;

namespace OvlTestBench.Tests;

public record OvlTest(string Name, Action<OvlPair> Test);

public static class Assert {
  private static readonly List<string> _errors = new();

  public static void That(bool condition, string message = "") {
    if (!condition) _errors.Add(message);
  }

  public static void AddError(string message) => _errors.Add(message);

  public static TestResult Result(string name) {
    var result = new TestResult(name, _errors.Count == 0, string.Join("; ", _errors));
    _errors.Clear();
    return result;
  }
}

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

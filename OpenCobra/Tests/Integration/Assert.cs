using System;
using System.Collections.Generic;

namespace OvlTestBench.Tests;

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

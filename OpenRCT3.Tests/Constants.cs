// Build-time constants and fixture paths shared across OpenRCT3.Tests.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Tests;

public static class Constants {
  /// <summary>The repository root directory, injected at build time via <c>SolutionDir</c>.</summary>
  public static string SolutionDir => ThisAssembly.Constants.SolutionDir;

  /// <summary>The vendored saved-park fixtures, shared with <c>OpenCobra.Tests</c> through an MSBuild link.</summary>
  public static string ParkFixturesDir => Path.Combine(SolutionDir, "OpenRCT3.Tests", "Fixtures", "Parks");
}

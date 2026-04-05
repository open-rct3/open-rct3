// InstallFinder
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Platforms;

public class InstallNotFoundException(string message) : Exception(message);

public static class InstallFinder {
  private static class SteamIds {
    public const string Complete = "RollerCoaster Tycoon 3 Complete Edition";
    public const string Platinum = "RollerCoaster Tycoon 3";
  }

  public static bool Validate(string path) {
    return Directory.Exists(path) && File.Exists(Path.Combine(path, "terrain", "RCT3", "Terrain_RCT3.common.ovl"));
  }

  public static string Find(string[]? extraPaths = null) {
    var potentialPaths = new List<string>();
    potentialPaths.AddRange([
#if WINDOWS
      @"C:\Program Files\RollerCoaster Tycoon 3",
      @"C:\Program Files\RollerCoaster Tycoon 3 Complete Edition",
      @"C:\Program Files\RollerCoaster Tycoon 3 Platinum",
      @"C:\Program Files (x86)\RollerCoaster Tycoon 3 Complete Edition",
      @"C:\Program Files (x86)\RollerCoaster Tycoon 3 Platinum",
      // Good Old Games (GOG)
      @"C:\GOG Games\RollerCoaster Tycoon 3 Complete Edition",
      @"C:\GOG Games\RollerCoaster Tycoon 3 Platinum",
      // Steam
      $"C:\\Steam\\steamapps\\common\\{SteamIds.Complete}",
      $"C:\\Steam\\steamapps\\common\\{SteamIds.Platinum}",
#elif OSX
      "/Applications/RollerCoaster Tycoon 3 Complete Edition.app/Contents/Assets",
      "/Applications/RollerCoaster Tycoon 3 Platinum.app/Contents/Assets",
      // TODO: Add GOG and Steam to macOS search paths
#endif
    ]);

    // Extra configurable paths
    if (extraPaths != null) potentialPaths.AddRange(extraPaths);

    foreach (var path in potentialPaths) {
      if (Validate(path))
        return path;
    }

    throw new InstallNotFoundException("RCT3 installation not found.");
  }
}

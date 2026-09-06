// Terrain
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenRCT3.Platforms;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the terrain of the park, a grid of tiles, each 4x4 meters.
/// </summary>
/// <remarks>
/// <para>
/// The coordinate system is Z-up:
/// <list type="bullet">
/// <item><description>X: West to East (East is +X)</description></item>
/// <item><description>Y: South to North (North is +Y)</description></item>
/// <item><description>Z: Down to Up (Up is +Z)</description></item>
/// </list>
/// </para>
/// <para>
/// The world origin (0, 0, 0) is located at the middle of the South edge of the park.
/// </para>
/// </remarks>
public class Terrain {
  public int Width { get; }
  public int Height { get; }
  public Texture? GrassTexture { get; private set; }

  /// <summary>
  /// Initializes a new instance of the <see cref="Terrain"/> class.
  /// </summary>
  /// <param name="width">The buildable width in tiles.</param>
  /// <param name="height">The buildable height in tiles.</param>
  public Terrain(int width = Park.DefaultMapSize, int height = Park.DefaultMapSize) {
    Width = width + (Park.OutOfBoundsBorder * 2);
    Height = height + (Park.OutOfBoundsBorder * 2);
  }

  /// <summary>
  /// Loads the terrain data and textures.
  /// </summary>
  /// <returns>A loaded <see cref="Terrain"/> instance.</returns>
  public static Terrain Load() {
    var config = AppConfig.Instance;
    Debug.Assert(config.InstallPath != null);

    var terrain = new Terrain();
    // Load textures from terrain/RCT3/Terrain_RCT3.common.ovl
    var terrainOvl = Path.Combine(config.InstallPath, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    var ovl = Ovl.Load(terrainOvl);

    return terrain;
  }
}

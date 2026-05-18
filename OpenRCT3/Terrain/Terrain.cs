// Terrain
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Materials;

namespace OpenRCT3.Terrain;

/// <summary>
/// Represents the terrain of the park.
/// </summary>
/// <remarks>
/// <para>
/// The terrain is a grid of tiles, each 4x4 meters.
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
  /// <summary>
  /// The size of a single grid square in meters.
  /// </summary>
  public const float TileSize = 4.0f;
  /// <summary>
  /// The default width and height of the buildable area in tiles.
  /// </summary>
  public const int DefaultMapSize = 128;
  /// <summary>
  /// The width of the out-of-bounds border in tiles.
  /// </summary>
  public const int OutOfBoundsBorder = 5;

  public int Width { get; }
  public int Height { get; }
  public Texture? GrassTexture { get; private set; }

  /// <summary>
  /// Initializes a new instance of the <see cref="Terrain"/> class.
  /// </summary>
  /// <param name="width">The buildable width in tiles.</param>
  /// <param name="height">The buildable height in tiles.</param>
  public Terrain(int width = DefaultMapSize, int height = DefaultMapSize) {
    Width = width + (OutOfBoundsBorder * 2);
    Height = height + (OutOfBoundsBorder * 2);
  }

  /// <summary>
  /// Loads the terrain data and textures.
  /// </summary>
  /// <returns>A loaded <see cref="Terrain"/> instance.</returns>
  public static Terrain Load() {
    var terrain = new Terrain();
    // TODO: Load textures from terrain/RCT3/Terrain_RCT3.common.ovl
    return terrain;
  }
}

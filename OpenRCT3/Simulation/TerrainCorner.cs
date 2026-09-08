// Terrain Corner
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Runtime.InteropServices;

namespace OpenRCT3.Simulation;

/// <summary>
/// Per-corner terrain data, owned by a single tile (corners are not shared between tiles).
/// </summary>
/// <remarks>
/// <para>
/// Packed to 4 bytes so that a 138x138 terrain stores its 76,176 corners in roughly 305 KB without
/// padding. Layout: <c>ushort Height</c> (corner-height count, see
/// <see cref="Terrain.HeightStep"/>) + <c>byte SurfaceIndex</c> (ground paint-type index) +
/// <c>byte CliffIndex</c> (cliff paint-type index).
/// </para>
/// <para>
/// <see cref="SurfaceIndex"/> / <see cref="CliffIndex"/> are storage only in this plan; the paint
/// tool/brush that writes them is future work. <see cref="CliffIndex"/> is stored on every corner
/// unconditionally; it is only meaningful to the renderer where a tile's edge is detached from its
/// neighbor and is otherwise don't-care.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public struct TerrainCorner {
  /// <summary>
  /// The corner's height in <see cref="Terrain.HeightStep"/> units (1 cm per unit). Stored as
  /// <c>ushort</c> for a 0 to 65,535 cm (0 to 655.35 m) range, which covers any plausible
  /// theme-park terrain height above the world Z=0 floor.
  /// </summary>
  public ushort Height;
  /// <summary>
  /// The index of the ground paint type, into the <c>ter</c> entries decoded from
  /// <c>Terrain_RCT3.*.ovl</c> (e.g. grass, sand, etc.).
  /// </summary>
  public byte SurfaceIndex;
  /// <summary>
  /// The index of the cliff paint type, into the <c>ter</c> entries decoded from
  /// <c>Terrain_RCT3.*.ovl</c> (e.g. rock cliff, dirt cliff, etc.). Only meaningful where a tile's
  /// edge is detached from its neighbor; otherwise don't-care.
  /// </summary>
  public byte CliffIndex;

  /// <summary>
  /// Initializes a new instance of the <see cref="TerrainCorner"/> struct.
  /// </summary>
  /// <param name="height">The corner height in <see cref="Terrain.HeightStep"/> units.</param>
  /// <param name="surfaceIndex">The ground paint-type index.</param>
  /// <param name="cliffIndex">The cliff paint-type index.</param>
  public TerrainCorner(ushort height, byte surfaceIndex = 0, byte cliffIndex = 0) {
    Height = height;
    SurfaceIndex = surfaceIndex;
    CliffIndex = cliffIndex;
  }
}

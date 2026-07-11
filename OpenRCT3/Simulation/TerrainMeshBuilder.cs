// Terrain Mesh Builder
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Meshes;
using System.Collections.Generic;
using System.Numerics;

namespace OpenRCT3.Simulation;

/// <summary>
/// Builds a renderable <see cref="Mesh"/> from a <see cref="Terrain"/>'s corner-height grid.
/// </summary>
/// <remarks>
/// A solid-colored prototype: each tile emits a top face (two triangles) from its four corners, plus
/// a vertical cliff face on its South/West edges when <see cref="Terrain.IsEdgeDetached"/> reports a
/// detached edge. Checking only South/West per tile (rather than all four) emits each interior edge's
/// cliff face exactly once, since a tile's South edge is the same world edge as its southern
/// neighbor's North edge. Surface painting (<see cref="TerrainCorner.SurfaceIndex"/>) isn't wired up
/// yet — every vertex gets the same flat <paramref name="color"/>.
/// </remarks>
public static class TerrainMeshBuilder {
  public static Mesh Build(Terrain terrain, Vector4 color, string? name = "Terrain") {
    var vertices = new List<Vertex>();
    var indices = new List<uint>();

    for (var tileY = 0; tileY < terrain.Height; tileY++) {
      for (var tileX = 0; tileX < terrain.Width; tileX++) {
        AddTopFace(terrain, tileX, tileY, color, vertices, indices);

        if (terrain.IsEdgeDetached(tileX, tileY, Edge.South))
          AddCliffFace(terrain, tileX, tileY, Edge.South, color, vertices, indices);
        if (terrain.IsEdgeDetached(tileX, tileY, Edge.West))
          AddCliffFace(terrain, tileX, tileY, Edge.West, color, vertices, indices);
      }
    }

    return new Mesh(vertices, indices) { Name = name };
  }

  private static Vector3 CornerPosition(Terrain terrain, int tileX, int tileY, TerrainCornerSlot slot) {
    var (dx, dy) = slot switch {
      TerrainCornerSlot.SouthWest => (0, 0),
      TerrainCornerSlot.SouthEast => (1, 0),
      TerrainCornerSlot.NorthWest => (0, 1),
      TerrainCornerSlot.NorthEast => (1, 1),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    var worldX = (tileX + dx - (terrain.Width / 2f)) * Park.TileSize;
    var worldY = (tileY + dy) * Park.TileSize;
    var worldZ = Terrain.CornerHeightToWorldZ(terrain.GetCorner(tileX, tileY, slot).Height);
    return new Vector3(worldX, worldY, worldZ);
  }

  private static void AddTopFace(
    Terrain terrain,
    int tileX,
    int tileY,
    Vector4 color,
    List<Vertex> vertices,
    List<uint> indices) {
    var sw = CornerPosition(terrain, tileX, tileY, TerrainCornerSlot.SouthWest);
    var se = CornerPosition(terrain, tileX, tileY, TerrainCornerSlot.SouthEast);
    var nw = CornerPosition(terrain, tileX, tileY, TerrainCornerSlot.NorthWest);
    var ne = CornerPosition(terrain, tileX, tileY, TerrainCornerSlot.NorthEast);

    // Two triangles, CCW when viewed from +Z: (SW, SE, NE) and (SW, NE, NW).
    var normal = Vector3.Normalize(Vector3.Cross(se - sw, ne - sw));
    var baseIndex = (uint)vertices.Count;
    // Each tile's texture maps 1:1 to the unit square, not world-space - simplest correct testing
    // mapping (no repeat/tiling math to get wrong). Revisit once continuous tiling is wanted.
    vertices.Add(new Vertex { Position = sw, Normal = normal, Color = color, TexCoord = new Vector2(0, 0) });
    vertices.Add(new Vertex { Position = se, Normal = normal, Color = color, TexCoord = new Vector2(1, 0) });
    vertices.Add(new Vertex { Position = ne, Normal = normal, Color = color, TexCoord = new Vector2(1, 1) });
    vertices.Add(new Vertex { Position = nw, Normal = normal, Color = color, TexCoord = new Vector2(0, 1) });
    indices.AddRange([baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3]);
  }

  private static void AddCliffFace(
    Terrain terrain,
    int tileX,
    int tileY,
    Edge edge,
    Vector4 color,
    List<Vertex> vertices,
    List<uint> indices) {
    var (nearSlot, farSlot) = edge switch {
      Edge.South => (TerrainCornerSlot.SouthWest, TerrainCornerSlot.SouthEast),
      Edge.West => (TerrainCornerSlot.SouthWest, TerrainCornerSlot.NorthWest),
      _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null),
    };
    var (dx, dy) = edge == Edge.South ? (0, -1) : (-1, 0);
    var neighborX = tileX + dx;
    var neighborY = tileY + dy;

    var nearTop = CornerPosition(terrain, tileX, tileY, nearSlot);
    var farTop = CornerPosition(terrain, tileX, tileY, farSlot);
    var nearNeighborSlot = edge == Edge.South ? TerrainCornerSlot.NorthWest : TerrainCornerSlot.SouthEast;
    var farNeighborSlot = edge == Edge.South ? TerrainCornerSlot.NorthEast : TerrainCornerSlot.NorthEast;
    var nearBottom = CornerPosition(terrain, neighborX, neighborY, nearNeighborSlot);
    var farBottom = CornerPosition(terrain, neighborX, neighborY, farNeighborSlot);

    // Wind so the face's outward normal points away from this tile, into the neighbor.
    var normal = Vector3.Normalize(Vector3.Cross(farTop - nearTop, nearBottom - nearTop));
    var baseIndex = (uint)vertices.Count;
    // Same unit-square-per-face mapping as AddTopFace - see comment there.
    vertices.Add(new Vertex { Position = nearTop, Normal = normal, Color = color, TexCoord = new Vector2(0, 1) });
    vertices.Add(new Vertex { Position = farTop, Normal = normal, Color = color, TexCoord = new Vector2(1, 1) });
    vertices.Add(new Vertex { Position = farBottom, Normal = normal, Color = color, TexCoord = new Vector2(1, 0) });
    vertices.Add(new Vertex { Position = nearBottom, Normal = normal, Color = color, TexCoord = new Vector2(0, 0) });
    indices.AddRange([baseIndex, baseIndex + 1, baseIndex + 2, baseIndex, baseIndex + 2, baseIndex + 3]);
  }
}

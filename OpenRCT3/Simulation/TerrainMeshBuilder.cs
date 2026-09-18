// Terrain Mesh Builder
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Meshes;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenRCT3.Simulation;

/// <summary>
/// Builds renderable <see cref="Mesh"/> geometry from a <see cref="Terrain"/>'s corner-height grid.
/// </summary>
/// <remarks>
/// Each tile emits a top face (two triangles) from its four corners, plus
/// a vertical cliff face on its South/West edges when <see cref="Terrain.IsEdgeDetached"/> reports a
/// detached edge. Checking only South/West per tile (rather than all four) emits each interior edge's
/// cliff face exactly once, since a tile's South edge is the same world edge as its southern
/// neighbor's North edge. <see cref="BuildBatches"/> separates top and cliff geometry by the decoded
/// <see cref="TerrainCorner.SurfaceIndex"/>/<see cref="TerrainCorner.CliffIndex"/> material keys while
/// retaining the same <paramref name="color"/> tint for every vertex.
/// </remarks>
public static class TerrainMeshBuilder {
  /// <summary>Builds one aggregate mesh for geometry-only callers.</summary>
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

  /// <summary>
  /// Builds deterministic texture batches for the terrain's decoded per-tile surface and cliff
  /// indices.
  /// </summary>
  /// <remarks>
  /// RCT3 DAT cells store one surface and one cliff index per tile. The simulation duplicates those
  /// values onto the tile's four corners so future paint tools can support blended terrain. Until
  /// that blending is implemented, mixed corner indices fail explicitly instead of silently choosing
  /// the wrong texture.
  /// </remarks>
  public static IReadOnlyList<TerrainMeshBatch> BuildBatches(
    Terrain terrain,
    Vector4 color,
    string name = "Terrain"
  ) {
    var geometry = new Dictionary<(TerrainMaterialKind Kind, byte Index), MeshGeometry>();

    for (var tileY = 0; tileY < terrain.Height; tileY++) {
      for (var tileX = 0; tileX < terrain.Width; tileX++) {
        var surfaceIndex = GetUniformMaterialIndex(
          terrain, tileX, tileY, TerrainMaterialKind.Surface);
        var surface = GetGeometry(geometry, TerrainMaterialKind.Surface, surfaceIndex);
        AddTopFace(terrain, tileX, tileY, color, surface.Vertices, surface.Indices);

        if (terrain.IsEdgeDetached(tileX, tileY, Edge.South)) {
          var cliffIndex = GetUniformMaterialIndex(
            terrain, tileX, tileY, TerrainMaterialKind.Cliff);
          var cliff = GetGeometry(geometry, TerrainMaterialKind.Cliff, cliffIndex);
          AddCliffFace(
            terrain, tileX, tileY, Edge.South, color, cliff.Vertices, cliff.Indices);
        }
        if (terrain.IsEdgeDetached(tileX, tileY, Edge.West)) {
          var cliffIndex = GetUniformMaterialIndex(
            terrain, tileX, tileY, TerrainMaterialKind.Cliff);
          var cliff = GetGeometry(geometry, TerrainMaterialKind.Cliff, cliffIndex);
          AddCliffFace(
            terrain, tileX, tileY, Edge.West, color, cliff.Vertices, cliff.Indices);
        }
      }
    }

    return geometry
      .OrderBy(batch => batch.Key.Kind)
      .ThenBy(batch => batch.Key.Index)
      .Select(batch => new TerrainMeshBatch(
        batch.Key.Kind,
        batch.Key.Index,
        new Mesh(batch.Value.Vertices, batch.Value.Indices) {
          Name = $"{name} {batch.Key.Kind} {batch.Key.Index}"
        }))
      .ToArray();
  }

  private static MeshGeometry GetGeometry(
    Dictionary<(TerrainMaterialKind Kind, byte Index), MeshGeometry> geometry,
    TerrainMaterialKind kind,
    byte index
  ) {
    var key = (kind, index);
    if (!geometry.TryGetValue(key, out var batch)) {
      batch = new MeshGeometry();
      geometry.Add(key, batch);
    }
    return batch;
  }

  private static byte GetUniformMaterialIndex(
    Terrain terrain,
    int tileX,
    int tileY,
    TerrainMaterialKind kind
  ) {
    var corners = terrain.GetCorners(tileX, tileY);
    var index = GetMaterialIndex(corners[0], kind);
    foreach (var corner in corners[1..]) {
      if (GetMaterialIndex(corner, kind) == index) continue;
      throw new InvalidOperationException(
        $"Terrain tile ({tileX}, {tileY}) has mixed {kind.ToString().ToLowerInvariant()} " +
        "indices; blended terrain rendering is not implemented.");
    }
    return index;
  }

  private static byte GetMaterialIndex(TerrainCorner corner, TerrainMaterialKind kind) => kind switch {
    TerrainMaterialKind.Surface => corner.SurfaceIndex,
    TerrainMaterialKind.Cliff => corner.CliffIndex,
    _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
  };

  internal static Vector3 CornerPosition(Terrain terrain, int tileX, int tileY, TerrainCornerSlot slot) {
    var (dx, dy) = slot switch {
      TerrainCornerSlot.SouthWest => (0, 0),
      TerrainCornerSlot.SouthEast => (1, 0),
      TerrainCornerSlot.NorthWest => (0, 1),
      TerrainCornerSlot.NorthEast => (1, 1),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };

    var worldX = terrain.Origin.X + ((tileX + dx) * terrain.TileSize.X);
    var worldZ = terrain.Origin.Y + ((tileY + dy) * terrain.TileSize.Y);
    var worldY = Terrain.CornerHeightToWorldY(terrain.GetCorner(tileX, tileY, slot).Height);
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

    // RCT3 splits each terrain cell along its SouthEast-to-NorthWest diagonal. These are the same
    // two triangle orders serialized by WaterManager: (SW, SE, NW) and (NE, NW, SE).
    var southWestNormal = Vector3.Normalize(Vector3.Cross(nw - sw, se - sw));
    var northEastNormal = Vector3.Normalize(Vector3.Cross(se - ne, nw - ne));
    var sharedNormal = Vector3.Normalize(southWestNormal + northEastNormal);
    var baseIndex = (uint)vertices.Count;
    vertices.Add(new Vertex {
      Position = sw, Normal = southWestNormal, TexCoord = new Vector2(0, 0), Color = color
    });
    vertices.Add(new Vertex {
      Position = se, Normal = sharedNormal, TexCoord = new Vector2(1, 0), Color = color
    });
    vertices.Add(new Vertex {
      Position = ne, Normal = northEastNormal, TexCoord = new Vector2(1, 1), Color = color
    });
    vertices.Add(new Vertex {
      Position = nw, Normal = sharedNormal, TexCoord = new Vector2(0, 1), Color = color
    });
    indices.AddRange([
      baseIndex,
      baseIndex + 3,
      baseIndex + 1,
      baseIndex + 1,
      baseIndex + 3,
      baseIndex + 2
    ]);
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
    var normal = Vector3.Normalize(Vector3.Cross(nearBottom - nearTop, farTop - nearTop));
    var edgeLength = edge == Edge.South ? terrain.TileSize.X : terrain.TileSize.Y;
    var nearHeight = Math.Abs(nearTop.Y - nearBottom.Y) / edgeLength;
    var farHeight = Math.Abs(farTop.Y - farBottom.Y) / edgeLength;
    var baseIndex = (uint)vertices.Count;
    vertices.Add(new Vertex {
      Position = nearTop, Normal = normal, TexCoord = new Vector2(0, nearHeight), Color = color
    });
    vertices.Add(new Vertex {
      Position = farTop, Normal = normal, TexCoord = new Vector2(1, farHeight), Color = color
    });
    vertices.Add(new Vertex {
      Position = farBottom, Normal = normal, TexCoord = new Vector2(1, 0), Color = color
    });
    vertices.Add(new Vertex {
      Position = nearBottom, Normal = normal, TexCoord = new Vector2(0, 0), Color = color
    });
    indices.AddRange([baseIndex, baseIndex + 2, baseIndex + 1, baseIndex, baseIndex + 3, baseIndex + 2]);
  }

  private sealed class MeshGeometry {
    public List<Vertex> Vertices { get; } = [];
    public List<uint> Indices { get; } = [];
  }
}

/// <summary>Identifies which terrain texture catalog partition a mesh batch uses.</summary>
public enum TerrainMaterialKind {
  Surface,
  Cliff,
}

/// <summary>A terrain mesh whose faces all use one decoded texture catalog entry.</summary>
public sealed record TerrainMeshBatch(TerrainMaterialKind Kind, byte Index, Mesh Mesh);

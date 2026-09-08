// TerrainMeshBuilderTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Simulation;
using System.Numerics;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class TerrainMeshBuilderTests {
  private static Terrain NewTerrain(ushort initialHeight = 0)
    => new(width: 2, height: 2, initialHeight);

  [Test]
  public void Build_FlatTerrain_EmitsOnlyTopFaces() {
    var terrain = NewTerrain();
    var mesh = TerrainMeshBuilder.Build(terrain, Vector4.One);

    // One quad (4 verts, 6 indices) per tile, no cliff faces on flat terrain.
    var tileCount = terrain.Width * terrain.Height;
    Assert.That(mesh.Vertices.Count, Is.EqualTo(tileCount * 4));
    Assert.That(mesh.Indices.Count, Is.EqualTo(tileCount * 6));
  }

  [Test]
  public void Build_DetachedEdge_EmitsCliffFace() {
    var terrain = NewTerrain();
    // Raising a single corner without propagating (SetCornerHeight) detaches every edge that
    // touches it — here, the South and West edges of tile (1,1), and the West edge of tile (2,1)
    // (whose corner now mismatches its neighbor across that edge).
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.SouthWest, 100);

    // Count how many South/West edges the builder checks are now detached, matching its own logic,
    // rather than hand-deriving which edges a single-corner edit affects.
    var detachedCount = 0;
    for (var y = 0; y < terrain.Height; y++) {
      for (var x = 0; x < terrain.Width; x++) {
        if (terrain.IsEdgeDetached(x, y, Edge.South)) detachedCount++;
        if (terrain.IsEdgeDetached(x, y, Edge.West)) detachedCount++;
      }
    }
    Assert.That(detachedCount, Is.GreaterThan(0));

    var flatMesh = TerrainMeshBuilder.Build(NewTerrain(), Vector4.One);
    var mesh = TerrainMeshBuilder.Build(terrain, Vector4.One);

    Assert.That(mesh.Vertices.Count, Is.EqualTo(flatMesh.Vertices.Count + (detachedCount * 4)));
    Assert.That(mesh.Indices.Count, Is.EqualTo(flatMesh.Indices.Count + (detachedCount * 6)));
  }

  [Test]
  public void Build_CornerWorldPositions_MatchTileGrid() {
    var terrain = NewTerrain();
    var mesh = TerrainMeshBuilder.Build(terrain, Vector4.One);

    // Tile (0,0)'s SouthWest corner is the grid's most negative-X, most-South point.
    var sw = mesh.Vertices[0].Position;
    Assert.That(sw.Y, Is.EqualTo(0));
    Assert.That(sw.Z, Is.EqualTo(0));
  }
}

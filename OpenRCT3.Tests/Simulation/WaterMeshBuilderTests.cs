// WaterMeshBuilderTests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Meshes;
using OpenRCT3.Simulation;
using System.Numerics;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class WaterMeshBuilderTests {
  private const int DryHeight = 300;
  private const int SubmergedHeight = 0;
  private const int WaterHeight = 100;
  private const int TileX = 1;
  private const int TileY = 1;

  private static IEnumerable<TestCaseData> TriangleMasks {
    get {
      foreach (var triangle in Enum.GetValues<WaterTerrainTriangle>()) {
        for (var mask = 1; mask <= 7; mask++)
          yield return new TestCaseData(triangle, Convert.ToByte(mask))
            .SetName($"Build_{triangle}_Mask{mask}_ClipsCounterClockwise");
      }
    }
  }

  [TestCaseSource(nameof(TriangleMasks))]
  public void Build_EveryMask_EmitsExpectedCounterClockwisePolygon(
    WaterTerrainTriangle triangle,
    byte mask
  ) {
    var terrain = new Terrain(width: 1, height: 1);
    SetTriangleHeights(terrain, triangle, mask, SubmergedHeight, DryHeight);
    var color = new Vector4(0.12f, 0.45f, 0.72f, 1f);
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, triangle, mask)
    ]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, color);

    var submergedCount = BitOperations.PopCount(Convert.ToUInt32(mask));
    var expectedVertexCount = submergedCount == 2 ? 4 : 3;
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices, Has.Count.EqualTo(expectedVertexCount));
      Assert.That(mesh.Indices, Has.Count.EqualTo(expectedVertexCount == 4 ? 6 : 3));
      Assert.That(mesh.Vertices.Select(vertex => vertex.Position.Y),
        Is.All.EqualTo(ExpectedSurfaceY));
      Assert.That(mesh.Vertices.Select(vertex => vertex.Normal), Is.All.EqualTo(Vector3.UnitY));
      Assert.That(mesh.Vertices.Select(vertex => vertex.Color), Is.All.EqualTo(color));
    }
    AssertCounterClockwise(mesh);
  }

  [TestCase(WaterTerrainTriangle.SouthWest)]
  [TestCase(WaterTerrainTriangle.NorthEast)]
  public void Build_OneSubmergedVertex_InterpolatesBothBoundaryEdges(
    WaterTerrainTriangle triangle
  ) {
    var terrain = new Terrain(width: 1, height: 1);
    SetTriangleHeights(terrain, triangle, vertexMask: 1, SubmergedHeight, DryHeight);
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, triangle, vertexMask: 1)
    ]);
    var corners = GetTrianglePositions(terrain, triangle);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    AssertPosition(mesh.Vertices[0], corners[0]);
    AssertPosition(mesh.Vertices[1], Vector2.Lerp(corners[0], corners[1], 1f / 3f));
    AssertPosition(mesh.Vertices[2], Vector2.Lerp(corners[2], corners[0], 2f / 3f));
  }

  [TestCase(WaterTerrainTriangle.SouthWest)]
  [TestCase(WaterTerrainTriangle.NorthEast)]
  public void Build_TwoSubmergedVertices_EmitsInterpolatedQuad(
    WaterTerrainTriangle triangle
  ) {
    var terrain = new Terrain(width: 1, height: 1);
    SetTriangleHeights(terrain, triangle, vertexMask: 3, SubmergedHeight, DryHeight);
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, triangle, vertexMask: 3)
    ]);
    var corners = GetTrianglePositions(terrain, triangle);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Indices, Is.EqualTo(new uint[] { 0, 2, 1, 0, 3, 2 }));
      AssertPosition(mesh.Vertices[0], corners[0]);
      AssertPosition(mesh.Vertices[1], corners[1]);
      AssertPosition(mesh.Vertices[2], Vector2.Lerp(corners[1], corners[2], 1f / 3f));
      AssertPosition(mesh.Vertices[3], Vector2.Lerp(corners[2], corners[0], 2f / 3f));
    }
  }

  [Test]
  public void Build_WaterTouchesOneCorner_OmitsBoundaryOnlyTriangle() {
    var terrain = new Terrain(width: 1, height: 1);
    SetTriangleHeights(
      terrain,
      WaterTerrainTriangle.SouthWest,
      vertexMask: 1,
      submergedHeight: WaterHeight,
      dryHeight: DryHeight);
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, WaterTerrainTriangle.SouthWest, 1)
    ]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices, Is.Empty);
      Assert.That(mesh.Indices, Is.Empty);
    }
  }

  [Test]
  public void Build_WaterTouchesOneEdge_OmitsBoundaryOnlyTriangle() {
    var terrain = new Terrain(width: 1, height: 1);
    SetTriangleHeights(
      terrain,
      WaterTerrainTriangle.SouthWest,
      vertexMask: 3,
      submergedHeight: WaterHeight,
      dryHeight: DryHeight);
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, WaterTerrainTriangle.SouthWest, 3)
    ]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices, Is.Empty);
      Assert.That(mesh.Indices, Is.Empty);
    }
  }

  [Test]
  public void Build_OnPlaneEdgeHasMixedMask_OmitsBoundaryOnlyTriangle() {
    var terrain = new Terrain(width: 1, height: 1);
    terrain.SetCorner(
      TileX,
      TileY,
      TerrainCornerSlot.SouthWest,
      new TerrainCorner(WaterHeight));
    terrain.SetCorner(
      TileX,
      TileY,
      TerrainCornerSlot.SouthEast,
      new TerrainCorner(WaterHeight));
    terrain.SetCorner(
      TileX,
      TileY,
      TerrainCornerSlot.NorthWest,
      new TerrainCorner(DryHeight));
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, WaterTerrainTriangle.SouthWest, 1)
    ]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices, Is.Empty);
      Assert.That(mesh.Indices, Is.Empty);
    }
  }

  [Test]
  public void Build_FullTileApi_UsesTheOriginalTerrainDiagonal() {
    var terrain = new Terrain(width: 1, height: 1);
    var pool = new WaterPool(WaterHeight, [(TileX, TileY)]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    var expected = new[] {
      CornerPosition(terrain, TerrainCornerSlot.SouthWest),
      CornerPosition(terrain, TerrainCornerSlot.SouthEast),
      CornerPosition(terrain, TerrainCornerSlot.NorthWest),
      CornerPosition(terrain, TerrainCornerSlot.NorthEast),
      CornerPosition(terrain, TerrainCornerSlot.NorthWest),
      CornerPosition(terrain, TerrainCornerSlot.SouthEast),
    };
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices.Select(vertex => new Vector2(
        vertex.Position.X, vertex.Position.Z)), Is.EqualTo(expected));
      Assert.That(mesh.Indices, Is.EqualTo(new uint[] { 0, 2, 1, 3, 5, 4 }));
      Assert.That(IndexedArea(mesh),
        Is.EqualTo(terrain.TileSize.X * terrain.TileSize.Y).Within(0.0001f));
    }
  }

  [Test]
  public void Build_UnorderedTriangles_UsesStableTileAndTriangleOrder() {
    var terrain = new Terrain(width: 2, height: 2);
    var pool = new WaterPool(0, [
      new WaterSurfaceTriangle(1, 2, WaterTerrainTriangle.SouthWest, 7),
      new WaterSurfaceTriangle(2, 1, WaterTerrainTriangle.NorthEast, 7),
      new WaterSurfaceTriangle(2, 1, WaterTerrainTriangle.SouthWest, 7),
    ]);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices, Has.Count.EqualTo(9));
      Assert.That(mesh.Vertices[0].Position.X,
        Is.EqualTo(terrain.Origin.X + (2 * terrain.TileSize.X)));
      Assert.That(mesh.Vertices[0].Position.Z,
        Is.EqualTo(terrain.Origin.Y + terrain.TileSize.Y));
      Assert.That(mesh.Vertices[3].Position.X,
        Is.EqualTo(terrain.Origin.X + (3 * terrain.TileSize.X)));
      Assert.That(mesh.Vertices[6].Position.X,
        Is.EqualTo(terrain.Origin.X + terrain.TileSize.X));
      Assert.That(mesh.Vertices[6].Position.Z,
        Is.EqualTo(terrain.Origin.Y + (2 * terrain.TileSize.Y)));
    }
  }

  [Test]
  public void Build_Ocean_EmitsOnlyItsBoundedInMapTriangles() {
    var terrain = new Terrain(width: 1, height: 1);
    var pool = new WaterPool(WaterHeight, [(0, 0)], isOcean: true);

    var mesh = WaterMeshBuilder.Build(terrain, pool, Vector4.One);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Indices, Has.Count.EqualTo(6));
      Assert.That(mesh.BoundingBox.Min.X, Is.EqualTo(terrain.Origin.X));
      Assert.That(mesh.BoundingBox.Max.X, Is.EqualTo(terrain.Origin.X + terrain.TileSize.X));
      Assert.That(mesh.BoundingBox.Min.Z, Is.EqualTo(terrain.Origin.Y));
      Assert.That(mesh.BoundingBox.Max.Z, Is.EqualTo(terrain.Origin.Y + terrain.TileSize.Y));
    }
  }

  [Test]
  public void Build_EmptyPool_Throws() {
    var terrain = new Terrain(width: 1, height: 1);
    var pool = new WaterPool(0, Array.Empty<(int X, int Y)>());

    Assert.Throws<ArgumentException>(
      new Action(() => WaterMeshBuilder.Build(terrain, pool, Vector4.One)));
  }

  [Test]
  public void Build_OffGridTriangle_Throws() {
    var terrain = new Terrain(width: 1, height: 1);
    var pool = new WaterPool(0, [
      new WaterSurfaceTriangle(-1, 0, WaterTerrainTriangle.SouthWest, 7)
    ]);

    Assert.Throws<ArgumentOutOfRangeException>(
      new Action(() => WaterMeshBuilder.Build(terrain, pool, Vector4.One)));
  }

  [Test]
  public void Build_MaskContradictsTerrainHeights_Throws() {
    var terrain = new Terrain(width: 1, height: 1);
    terrain.SetCorner(
      TileX,
      TileY,
      TerrainCornerSlot.SouthWest,
      new TerrainCorner(DryHeight));
    var pool = new WaterPool(WaterHeight, [
      new WaterSurfaceTriangle(TileX, TileY, WaterTerrainTriangle.SouthWest, 7)
    ]);

    Assert.Throws<InvalidDataException>(
      new Action(() => WaterMeshBuilder.Build(terrain, pool, Vector4.One)));
  }

  private static void SetTriangleHeights(
    Terrain terrain,
    WaterTerrainTriangle triangle,
    byte vertexMask,
    int submergedHeight,
    int dryHeight
  ) {
    var slots = GetTriangleSlots(triangle);
    for (var index = 0; index < slots.Length; index++) {
      var height = (vertexMask & (1 << index)) != 0 ? submergedHeight : dryHeight;
      terrain.SetCorner(TileX, TileY, slots[index], new TerrainCorner(height));
    }
  }

  private static Vector2[] GetTrianglePositions(
    Terrain terrain,
    WaterTerrainTriangle triangle
  ) => GetTriangleSlots(triangle)
    .Select(slot => CornerPosition(terrain, slot))
    .ToArray();

  private static TerrainCornerSlot[] GetTriangleSlots(WaterTerrainTriangle triangle) => triangle switch {
    WaterTerrainTriangle.SouthWest => [
      TerrainCornerSlot.SouthWest,
      TerrainCornerSlot.SouthEast,
      TerrainCornerSlot.NorthWest,
    ],
    WaterTerrainTriangle.NorthEast => [
      TerrainCornerSlot.NorthEast,
      TerrainCornerSlot.NorthWest,
      TerrainCornerSlot.SouthEast,
    ],
    _ => throw new ArgumentOutOfRangeException(nameof(triangle), triangle, null),
  };

  private static Vector2 CornerPosition(Terrain terrain, TerrainCornerSlot slot) {
    var (dx, dy) = slot switch {
      TerrainCornerSlot.SouthWest => (0, 0),
      TerrainCornerSlot.SouthEast => (1, 0),
      TerrainCornerSlot.NorthWest => (0, 1),
      TerrainCornerSlot.NorthEast => (1, 1),
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
    return new Vector2(
      terrain.Origin.X + ((TileX + dx) * terrain.TileSize.X),
      terrain.Origin.Y + ((TileY + dy) * terrain.TileSize.Y));
  }

  private static void AssertPosition(Vertex vertex, Vector2 expected) {
    Assert.That(vertex.Position.X, Is.EqualTo(expected.X).Within(0.0001f));
    Assert.That(vertex.Position.Z, Is.EqualTo(expected.Y).Within(0.0001f));
    Assert.That(vertex.Position.Y, Is.EqualTo(ExpectedSurfaceY).Within(0.0001f));
  }

  private static float ExpectedSurfaceY =>
    Terrain.CornerHeightToWorldY(WaterHeight) + WaterMeshBuilder.SurfaceRenderOffset;

  private static void AssertCounterClockwise(Mesh mesh) {
    for (var index = 0; index < mesh.Indices.Count; index += 3) {
      var a = mesh.Vertices[Convert.ToInt32(mesh.Indices[index])].Position;
      var b = mesh.Vertices[Convert.ToInt32(mesh.Indices[index + 1])].Position;
      var c = mesh.Vertices[Convert.ToInt32(mesh.Indices[index + 2])].Position;
      Assert.That(Vector3.Cross(b - a, c - a).Y, Is.GreaterThan(0f));
    }
  }

  private static float IndexedArea(Mesh mesh) {
    var area = 0f;
    for (var index = 0; index < mesh.Indices.Count; index += 3) {
      var a = mesh.Vertices[Convert.ToInt32(mesh.Indices[index])].Position;
      var b = mesh.Vertices[Convert.ToInt32(mesh.Indices[index + 1])].Position;
      var c = mesh.Vertices[Convert.ToInt32(mesh.Indices[index + 2])].Position;
      area += Vector3.Cross(b - a, c - a).Length() / 2f;
    }
    return area;
  }
}

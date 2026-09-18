using System.Numerics;
using System.Reflection;
using System.Text;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OpenCobra.Tests.OVL;

[TestFixture]
public class StaticShapesTests {
  [Test]
  public void Decode_ReadsRelocatedMeshGeometryEffectsAndResourceKeys() {
    var fixture = new StaticShapeFixture();

    var shape = fixture.Decode();

    using (Assert.EnterMultipleScope()) {
      Assert.That(shape.Name, Is.EqualTo("synthetic"));
      Assert.That(shape.BoundingBoxMin, Is.EqualTo(new Vector3(-1, -2, -3)));
      Assert.That(shape.BoundingBoxMax, Is.EqualTo(new Vector3(4, 5, 6)));
      Assert.That(shape.Meshes, Has.Count.EqualTo(1));
      Assert.That(shape.Effects, Has.Count.EqualTo(1));
    }

    var mesh = shape.Meshes[0];
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Name, Is.EqualTo("synthetic/mesh/0"));
      Assert.That(mesh.SupportType, Is.EqualTo(-1));
      Assert.That(mesh.FtxRef, Is.EqualTo("water:ftx"));
      Assert.That(mesh.TxsRef, Is.EqualTo("opaque:txs"));
      Assert.That(mesh.Transparency, Is.EqualTo(0));
      Assert.That(mesh.TextureFlags, Is.EqualTo(12));
      Assert.That(mesh.Sides, Is.EqualTo(3));
      Assert.That(mesh.Vertices, Has.Count.EqualTo(3));
      Assert.That(mesh.Indices, Is.EqualTo(new uint[] { 0, 1, 2 }));
      Assert.That(mesh.IndexLayout, Is.EqualTo(StaticShapeIndexLayout.TriangleList));
      Assert.That(mesh.StoredIndexCount, Is.EqualTo(3));
      Assert.That(mesh.TriangleCount, Is.EqualTo(1));
      Assert.That(mesh.PossibleTriangleCounts, Is.EqualTo(new[] { 1 }));
    }

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.Vertices[0].Position, Is.EqualTo(new Vector3(1, 2, 3)));
      Assert.That(mesh.Vertices[0].Normal, Is.EqualTo(Vector3.UnitY));
      Assert.That(mesh.Vertices[0].TexCoord, Is.EqualTo(new Vector2(0.25f, 0.75f)));
      Assert.That(mesh.Vertices[0].Color.X, Is.EqualTo(17 / 255.0f));
      Assert.That(mesh.Vertices[0].Color.Y, Is.EqualTo(34 / 255.0f));
      Assert.That(mesh.Vertices[0].Color.Z, Is.EqualTo(51 / 255.0f));
      Assert.That(mesh.Vertices[0].Color.W, Is.EqualTo(1));
      Assert.That(shape.Effects[0].Name, Is.EqualTo("spark"));
      Assert.That(shape.Effects[0].Position, Is.EqualTo(Matrix4x4.Identity));
    }
  }

  [Test]
  public void Decode_PlacementNoSort_UsesStoredTriangleCountAndThreeIndicesPerTriangle() {
    var fixture = new StaticShapeFixture();
    fixture.UsePlacementTriangleList([0, 1, 2, 2, 1, 0]);

    var mesh = fixture.Decode().Meshes[0];

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.StoredIndexCount, Is.EqualTo(2));
      Assert.That(mesh.IndexLayout, Is.EqualTo(StaticShapeIndexLayout.PlacementTriangleList));
      Assert.That(mesh.Indices, Is.EqualTo(new uint[] { 0, 1, 2, 2, 1, 0 }));
      Assert.That(mesh.TriangleCount, Is.EqualTo(2));
      Assert.That(mesh.PossibleTriangleCounts, Is.EqualTo(new[] { 2 }));
    }
  }

  [Test]
  public void Decode_PlacementLayoutAmbiguity_PreservesTheCompleteSerializedPayload() {
    var fixture = new StaticShapeFixture();
    fixture.UsePlacementTriangleList([0, 1, 2, 0, 1, 2, 0, 1, 2]);

    var mesh = fixture.Decode().Meshes[0];

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.StoredIndexCount, Is.EqualTo(3));
      Assert.That(mesh.IndexLayout, Is.EqualTo(StaticShapeIndexLayout.PlacementAmbiguous));
      Assert.That(mesh.Indices, Is.EqualTo(new uint[] { 0, 1, 2, 0, 1, 2, 0, 1, 2 }));
      Assert.That(mesh.TriangleCount, Is.Null);
      Assert.That(mesh.PossibleTriangleCounts, Is.EqualTo(new[] { 1, 3 }));
    }
  }

  [Test]
  public void Decode_DistinctMeshHeadersReuseAliasedGeometryWithinAggregateBudget() {
    var fixture = new StaticShapeFixture();
    fixture.UseDistinctMeshFanSharingGeometry(100);

    var shape = fixture.Decode(new StaticShapeDecodeLimits(5_000, 500));

    using (Assert.EnterMultipleScope()) {
      Assert.That(shape.Meshes, Has.Count.EqualTo(100));
      Assert.That(shape.Meshes.All(mesh =>
        ReferenceEquals(mesh.Vertices, shape.Meshes[0].Vertices)), Is.True);
      Assert.That(shape.Meshes.All(mesh =>
        ReferenceEquals(mesh.Indices, shape.Meshes[0].Indices)), Is.True);
    }
  }

  [Test]
  public void Decode_EnforcesWholeDecodeByteAndObjectBudgets() {
    var byteFixture = new StaticShapeFixture();
    var objectFixture = new StaticShapeFixture();

    using (Assert.EnterMultipleScope()) {
      Assert.Throws<InvalidDataException>(new Action(() =>
        byteFixture.Decode(new StaticShapeDecodeLimits(130, 100))));
      Assert.Throws<InvalidDataException>(new Action(() =>
        objectFixture.Decode(new StaticShapeDecodeLimits(1024, 5))));
    }
  }

  [Test]
  public void Decode_ZeroEffects_IgnoresOneSidedResolvableNamesRelocation() {
    var fixture = new StaticShapeFixture();
    fixture.UseZeroEffectsWithOneSidedResolvableNamesRelocation();

    var shape = fixture.Decode();

    Assert.That(shape.Effects, Is.Empty);
  }

  [Test]
  public void Decode_ZeroEffects_IgnoresOneSidedNullNamesRelocation() {
    var fixture = new StaticShapeFixture();
    fixture.UseZeroEffectsWithOneSidedNullNamesRelocation();

    var shape = fixture.Decode();

    Assert.That(shape.Effects, Is.Empty);
  }

  [Test]
  public void Decode_PositiveEffects_RejectsOneSidedPointerRelocation() {
    var fixture = new StaticShapeFixture();
    fixture.RemoveEffectNamesRelocation();

    var error = Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));

    Assert.That(error!.Message,
      Does.Contain("effect names is not a non-null relocated pointer"));
  }

  [Test]
  public void Decode_PositiveEffects_RejectsDanglingPointerRelocation() {
    var fixture = new StaticShapeFixture();
    fixture.UseDanglingEffectPositionsRelocation();

    Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));
  }

  [Test]
  public void Decode_RejectsLocalSymbolReferenceWithoutValidatedLoaderMetadata() {
    var fixture = new StaticShapeFixture();
    fixture.RemoveLocalFtxLoaderMetadata();

    var error = Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));

    Assert.That(error!.Message,
      Does.Contain("water:ftx' is local but has no validated loader metadata"));
  }

  [Test]
  public void Extract_ChargesResourceMetadataBeforeBuildingIndexes() {
    var assembly = Assembly.GetExecutingAssembly();
    var resources = assembly.GetManifestResourceNames();
    var commonResource = resources.First(name => name.EndsWith(".common.ovl"));
    var uniqueResource = commonResource[..^".common.ovl".Length] + ".unique.ovl";
    var tempDir = Directory.CreateTempSubdirectory().FullName;
    try {
      var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
      CopyResource(assembly, commonResource, commonPath);
      if (resources.Contains(uniqueResource))
        CopyResource(assembly, uniqueResource, Path.Combine(tempDir, "fixture.unique.ovl"));
      using var ovl = Ovl.Load(commonPath);

      var error = Assert.Throws<InvalidDataException>(new Action(() =>
        StaticShapes.Extract(ovl, new StaticShapeDecodeLimits(1, 1_000_000))));

      Assert.That(error!.Message, Does.Contain("resource index key"));
    } finally {
      Directory.Delete(tempDir, recursive: true);
    }
  }

  [TestCase(LocalLoaderMutation.Missing)]
  [TestCase(LocalLoaderMutation.WrongType)]
  public void Extract_RejectsLocalSymbolReferenceWithoutMatchingLoaderMetadata(
    LocalLoaderMutation mutation
  ) {
    WithTownHallOvl(ovl => {
      var localFtx = ovl.Keys.Single(file =>
        file.Name == "RS-Base" && file.Type == FileType.FlexibleTexture);
      Assert.That(ovl.TryGetDataPointer(localFtx, out var localFtxAddress), Is.True);
      var loaderEntries = (List<OvlLoaderEntry>)ovl.LoaderEntriesInOrder;
      var loaderIndex = loaderEntries.FindIndex(entry =>
        entry.DataAddress == localFtxAddress &&
        string.Equals(entry.Tag, "ftx", StringComparison.OrdinalIgnoreCase));
      Assert.That(loaderIndex, Is.GreaterThanOrEqualTo(0));

      if (mutation == LocalLoaderMutation.Missing)
        loaderEntries.RemoveAt(loaderIndex);
      else
        loaderEntries[loaderIndex] = loaderEntries[loaderIndex] with { Tag = "txs" };

      var error = Assert.Throws<InvalidDataException>(new Action(() => StaticShapes.Extract(ovl)));

      Assert.That(error!.Message, Does.Contain("RS-Base:ftx"));
      Assert.That(error.Message, mutation == LocalLoaderMutation.Missing
        ? Does.Contain("is local but has no validated loader metadata")
        : Does.Contain("conflicting archive resource metadata"));
    });
  }

  [Test]
  public void Extract_RejectsCopiedLoaderMetadataAtForgedAlignedAddress() {
    WithTownHallOvl(ovl => {
      var loaderEntries = (List<OvlLoaderEntry>)ovl.LoaderEntriesInOrder;
      var shapeLoaderIndexes = loaderEntries
        .Select((entry, index) => (entry, index))
        .Where(item => item.entry.Tag.ToFileType() == FileType.StaticShape)
        .ToList();
      Assert.That(shapeLoaderIndexes, Is.Not.Empty);

      foreach (var (entry, index) in shapeLoaderIndexes)
        loaderEntries[index] = entry with { StructAddress = entry.StructAddress + 20_000 };

      var error = Assert.Throws<InvalidDataException>(new Action(() => StaticShapes.Extract(ovl)));

      Assert.That(error!.Message, Does.Contain("is not an exact loader-table entry"));
    });
  }

  [TestCase(SymbolReferenceMetadataMutation.Count)]
  [TestCase(SymbolReferenceMetadataMutation.Size)]
  public void Extract_RejectsSymbolReferenceCountOrSizeMismatch(
    SymbolReferenceMetadataMutation mutation
  ) {
    WithTownHallOvl(ovl => {
      var blocks = (List<OvlBlockEntry>)ovl.SymbolReferenceBlocksInOrder;
      Assert.That(blocks, Is.Not.Empty);
      blocks[0] = mutation == SymbolReferenceMetadataMutation.Count
        ? blocks[0] with { RecordCount = blocks[0].RecordCount + 1 }
        : blocks[0] with { Data = blocks[0].Data[..^1] };

      var error = Assert.Throws<InvalidDataException>(new Action(() => StaticShapes.Extract(ovl)));

      Assert.That(error!.Message, Does.Contain("block size"));
      Assert.That(error.Message, Does.Contain("does not match its parsed count"));
    });
  }

  [Test]
  public void Extract_AllowsExternalSIOpaqueTextureStyle() {
    WithTownHallOvl(ovl => {
      var shapes = StaticShapes.Extract(ovl);

      Assert.That(shapes.SelectMany(shape => shape.Meshes).Select(mesh => mesh.TxsRef),
        Does.Contain("SIOpaque:txs"));
    });
  }

  [TestCase(MalformedShape.TruncatedHeader)]
  [TestCase(MalformedShape.OversizedMeshCount)]
  [TestCase(MalformedShape.MissingMeshRelocation)]
  [TestCase(MalformedShape.TruncatedVertices)]
  [TestCase(MalformedShape.OutOfRangeIndex)]
  [TestCase(MalformedShape.NonTriangleIndexCount)]
  [TestCase(MalformedShape.AggregateCountMismatch)]
  [TestCase(MalformedShape.UnknownResourceReference)]
  [TestCase(MalformedShape.UnterminatedEffectName)]
  [TestCase(MalformedShape.NonFiniteVertex)]
  [TestCase(MalformedShape.TruncatedPlacementIndices)]
  [TestCase(MalformedShape.PlacementIndexCountOverflow)]
  [TestCase(MalformedShape.OversizedEffectCount)]
  [TestCase(MalformedShape.WrongSymbolReferenceType)]
  [TestCase(MalformedShape.WrongSymbolReferenceArchiveType)]
  [TestCase(MalformedShape.WrongSymbolReferenceOwner)]
  [TestCase(MalformedShape.ConflictingRawAndSymbolReference)]
  [TestCase(MalformedShape.WrongDirectReferenceType)]
  [TestCase(MalformedShape.MissingStaticShapeLoaderOwnership)]
  public void Decode_RejectsMalformedOrUnboundedData(MalformedShape malformed) {
    var fixture = new StaticShapeFixture();
    fixture.MakeMalformed(malformed);

    Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));
  }

  [Test]
  public void Decode_RejectsEffectNameWhoseTerminatorIsAfterTheLimit() {
    var fixture = new StaticShapeFixture();
    fixture.MakeMalformed(MalformedShape.EffectNameTooLong);

    var error = Assert.Throws<InvalidDataException>(new Action(() => fixture.Decode()));

    Assert.That(error!.Message, Does.Contain("exceeds 4096 bytes"));
  }

  [Test]
  public void Extract_FromCustomOvlFixtures_DecodesStaticShapes() {
    var assembly = Assembly.GetExecutingAssembly();
    var resources = assembly.GetManifestResourceNames();
    var commonResources = resources.Where(name => name.EndsWith(".common.ovl")).ToList();
    var shapeCount = 0;
    var meshCount = 0;
    var vertexCount = 0;
    var indexCount = 0;
    var referenceCount = 0;
    string? townHallMesh4Ftx = null;

    foreach (var commonResource in commonResources) {
      var uniqueResource = commonResource[..^".common.ovl".Length] + ".unique.ovl";
      var tempDir = Directory.CreateTempSubdirectory().FullName;
      try {
        var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
        CopyResource(assembly, commonResource, commonPath);
        if (resources.Contains(uniqueResource))
          CopyResource(assembly, uniqueResource, Path.Combine(tempDir, "fixture.unique.ovl"));

        using var ovl = Ovl.Load(commonPath);
        var shapes = StaticShapes.Extract(ovl);
        shapeCount += shapes.Count;
        meshCount += shapes.Sum(shape => shape.Meshes.Count);
        vertexCount += shapes.Sum(shape => shape.Meshes.Sum(mesh => mesh.Vertices.Count));
        indexCount += shapes.Sum(shape => shape.Meshes.Sum(mesh => mesh.Indices.Count));
        referenceCount += shapes.Sum(shape => shape.Meshes.Sum(mesh =>
          Convert.ToInt32(mesh.FtxRef != null) + Convert.ToInt32(mesh.TxsRef != null)));
        var townHall = shapes.SingleOrDefault(shape => shape.Name == "RS-TownHall");
        if (townHall != null) {
          Assert.That(townHall.Meshes, Has.Count.GreaterThan(4));
          townHallMesh4Ftx = townHall.Meshes[4].FtxRef;
        }
      } finally {
        Directory.Delete(tempDir, recursive: true);
      }
    }

    TestContext.Progress.WriteLine(
      $"Custom SHS evidence: shapes={shapeCount}, meshes={meshCount}, vertices={vertexCount}, " +
      $"indices={indexCount}, resolvedReferences={referenceCount}");
    using (Assert.EnterMultipleScope()) {
      Assert.That(shapeCount, Is.GreaterThan(0), "No static shapes were found in the custom OVL fixtures.");
      Assert.That(referenceCount, Is.GreaterThan(0), "No SHS resource references were resolved.");
      Assert.That(townHallMesh4Ftx, Is.EqualTo("RS-Base:ftx"));
    }
  }

  private static void CopyResource(Assembly assembly, string resourceName, string path) {
    using var input = assembly.GetManifestResourceStream(resourceName);
    Assert.That(input, Is.Not.Null, $"Embedded resource '{resourceName}' not found.");
    using var output = File.Create(path);
    input.CopyTo(output);
  }

  private static void WithTownHallOvl(Action<Ovl> action) {
    var assembly = Assembly.GetExecutingAssembly();
    var resources = assembly.GetManifestResourceNames();
    var commonResource = resources.Single(name => name.EndsWith(
      ".RS-TownHall.common.ovl", StringComparison.OrdinalIgnoreCase));
    var uniqueResource = commonResource[..^".common.ovl".Length] + ".unique.ovl";
    var tempDir = Directory.CreateTempSubdirectory().FullName;
    try {
      var commonPath = Path.Combine(tempDir, "fixture.common.ovl");
      CopyResource(assembly, commonResource, commonPath);
      CopyResource(assembly, uniqueResource, Path.Combine(tempDir, "fixture.unique.ovl"));
      using var ovl = Ovl.Load(commonPath);
      action(ovl);
    } finally {
      Directory.Delete(tempDir, recursive: true);
    }
  }

  public enum LocalLoaderMutation {
    Missing,
    WrongType
  }

  public enum SymbolReferenceMetadataMutation {
    Count,
    Size
  }

  public enum MalformedShape {
    TruncatedHeader,
    OversizedMeshCount,
    MissingMeshRelocation,
    TruncatedVertices,
    OutOfRangeIndex,
    NonTriangleIndexCount,
    AggregateCountMismatch,
    UnknownResourceReference,
    UnterminatedEffectName,
    NonFiniteVertex,
    TruncatedPlacementIndices,
    PlacementIndexCountOverflow,
    OversizedEffectCount,
    EffectNameTooLong,
    WrongSymbolReferenceType,
    WrongSymbolReferenceArchiveType,
    WrongSymbolReferenceOwner,
    ConflictingRawAndSymbolReference,
    WrongDirectReferenceType,
    MissingStaticShapeLoaderOwnership
  }

  private sealed class StaticShapeFixture {
    private const uint ShapeAddress = 100;
    private const uint MeshPointersAddress = 200;
    private const uint MeshAddress = 300;
    private const uint VerticesAddress = 400;
    private const uint IndicesAddress = 600;
    private const uint PositionsAddress = 700;
    private const uint NamesAddress = 800;
    private const uint NameAddress = 900;
    private const uint FtxAddress = 1000;
    private const uint TxsAddress = 1100;

    private readonly FakeStaticShapeDataSource source = new();

    public StaticShapeFixture() {
      var shape = source.AddBlock(ShapeAddress, 56);
      WriteVector3(shape, 0, new Vector3(-1, -2, -3));
      WriteVector3(shape, 12, new Vector3(4, 5, 6));
      WriteUInt32(shape, 24, 3);
      WriteUInt32(shape, 28, 3);
      WriteUInt32(shape, 32, 1);
      WriteUInt32(shape, 36, 1);
      WritePointer(shape, ShapeAddress, 40, MeshPointersAddress);
      WriteUInt32(shape, 44, 1);
      WritePointer(shape, ShapeAddress, 48, PositionsAddress);
      WritePointer(shape, ShapeAddress, 52, NamesAddress);

      var meshPointers = source.AddBlock(MeshPointersAddress, 4);
      WritePointer(meshPointers, MeshPointersAddress, 0, MeshAddress);

      var mesh = source.AddBlock(MeshAddress, 40);
      WriteInt32(mesh, 0, -1);
      WriteUInt32(mesh, 4, 0);
      WriteUInt32(mesh, 8, 0);
      WriteUInt32(mesh, 12, 0);
      WriteUInt32(mesh, 16, 12);
      WriteUInt32(mesh, 20, 3);
      WriteUInt32(mesh, 24, 3);
      WriteUInt32(mesh, 28, 3);
      WritePointer(mesh, MeshAddress, 32, VerticesAddress);
      WritePointer(mesh, MeshAddress, 36, IndicesAddress);

      var vertices = source.AddBlock(VerticesAddress, 36 * 3);
      WriteVertex(vertices, 0, new Vector3(1, 2, 3), Vector3.UnitY, new Vector2(0.25f, 0.75f));
      WriteVertex(vertices, 36, new Vector3(4, 5, 6), Vector3.UnitY, Vector2.Zero);
      WriteVertex(vertices, 72, new Vector3(7, 8, 9), Vector3.UnitY, Vector2.One);

      var indices = source.AddBlock(IndicesAddress, 12);
      WriteUInt32(indices, 0, 0);
      WriteUInt32(indices, 4, 1);
      WriteUInt32(indices, 8, 2);

      var positions = source.AddBlock(PositionsAddress, 64);
      WriteMatrix(positions, 0, Matrix4x4.Identity);
      var names = source.AddBlock(NamesAddress, 4);
      WritePointer(names, NamesAddress, 0, NameAddress);
      source.AddBlock(NameAddress, Encoding.ASCII.GetBytes("spark\0"));
      source.AddBlock(FtxAddress, 4);
      source.AddBlock(TxsAddress, 4);
      source.MutableResourcesByAddress[FtxAddress] =
        new StaticShapeResourceMetadata("water:ftx", "ftx");
      source.MutableResourcesByAddress[TxsAddress] =
        new StaticShapeResourceMetadata("opaque:txs", "txs");
      source.MutableResourcesByKey["water:ftx"] = source.MutableResourcesByAddress[FtxAddress];
      source.MutableResourcesByKey["opaque:txs"] = source.MutableResourcesByAddress[TxsAddress];
      source.MutableLocalResourceKeys.Add("water:ftx");
      source.MutableLocalResourceKeys.Add("opaque:txs");
      source.MutableStaticShapeLoaderDataAddresses.Add(ShapeAddress);
      source.MutableResourceReferences[MeshAddress + 4] =
        new StaticShapeResourceReference("water:ftx", ShapeAddress);
      source.MutableResourceReferences[MeshAddress + 8] =
        new StaticShapeResourceReference("opaque:txs", ShapeAddress);
    }

    public StaticShape Decode() => StaticShapes.Decode("synthetic", ShapeAddress, source);
    public StaticShape Decode(StaticShapeDecodeLimits limits) =>
      StaticShapes.Decode("synthetic", ShapeAddress, source, limits);

    public void UsePlacementTriangleList(uint[] indices) {
      Assert.That(indices.Length, Is.Positive);
      Assert.That(indices.Length % 3, Is.Zero);
      WriteUInt32(source.Blocks[MeshAddress], 12, 1);
      WriteUInt32(source.Blocks[MeshAddress], 28, Convert.ToUInt32(indices.Length / 3));
      WriteUInt32(source.Blocks[ShapeAddress], 28, Convert.ToUInt32(indices.Length / 3));
      source.ReplaceBlock(IndicesAddress, EncodeIndices(indices));
    }

    public void UseZeroEffectsWithOneSidedResolvableNamesRelocation() {
      var shape = source.Blocks[ShapeAddress];
      WriteUInt32(shape, 44, 0);
      WriteUInt32(shape, 48, 0);
      source.Relocations.Remove(ShapeAddress + 48);
      WritePointer(shape, ShapeAddress, 52, NamesAddress);
    }

    public void UseZeroEffectsWithOneSidedNullNamesRelocation() {
      var shape = source.Blocks[ShapeAddress];
      WriteUInt32(shape, 44, 0);
      WriteUInt32(shape, 48, 0);
      source.Relocations.Remove(ShapeAddress + 48);
      WriteUInt32(shape, 52, 0);
      source.Relocations[ShapeAddress + 52] = 0;
    }

    public void RemoveEffectNamesRelocation() => source.Relocations.Remove(ShapeAddress + 52);

    public void UseDanglingEffectPositionsRelocation() =>
      WritePointer(source.Blocks[ShapeAddress], ShapeAddress, 48, 1_000_000);

    public void RemoveLocalFtxLoaderMetadata() => source.MutableResourcesByKey.Remove("water:ftx");

    public void UseDistinctMeshFanSharingGeometry(int count) {
      const uint fanPointersAddress = 12_000;
      var pointers = new byte[count * 4];
      for (var i = 0; i < count; i++) {
        var meshAddress = i == 0 ? MeshAddress : Convert.ToUInt32(20_000 + (i - 1) * 40);
        if (i != 0) {
          source.AddBlock(meshAddress, source.Blocks[MeshAddress].ToArray());
          source.Relocations[meshAddress + 32] = VerticesAddress;
          source.Relocations[meshAddress + 36] = IndicesAddress;
          source.MutableResourceReferences[meshAddress + 4] =
            new StaticShapeResourceReference("water:ftx", ShapeAddress);
          source.MutableResourceReferences[meshAddress + 8] =
            new StaticShapeResourceReference("opaque:txs", ShapeAddress);
        }
        WritePointer(pointers, fanPointersAddress, i * 4, meshAddress);
      }
      source.AddBlock(fanPointersAddress, pointers);
      WritePointer(source.Blocks[ShapeAddress], ShapeAddress, 40, fanPointersAddress);
      WriteUInt32(source.Blocks[ShapeAddress], 24, Convert.ToUInt32(count * 3));
      WriteUInt32(source.Blocks[ShapeAddress], 28, Convert.ToUInt32(count * 3));
      WriteUInt32(source.Blocks[ShapeAddress], 32, Convert.ToUInt32(count));
      WriteUInt32(source.Blocks[ShapeAddress], 36, Convert.ToUInt32(count));
    }

    public void MakeMalformed(MalformedShape malformed) {
      switch (malformed) {
        case MalformedShape.TruncatedHeader:
          source.ReplaceBlock(ShapeAddress, source.Blocks[ShapeAddress][..55]);
          break;
        case MalformedShape.OversizedMeshCount:
          WriteUInt32(source.Blocks[ShapeAddress], 36, uint.MaxValue);
          break;
        case MalformedShape.MissingMeshRelocation:
          source.Relocations.Remove(MeshPointersAddress);
          break;
        case MalformedShape.TruncatedVertices:
          source.ReplaceBlock(VerticesAddress, source.Blocks[VerticesAddress][..^1]);
          break;
        case MalformedShape.OutOfRangeIndex:
          WriteUInt32(source.Blocks[IndicesAddress], 8, 3);
          break;
        case MalformedShape.NonTriangleIndexCount:
          WriteUInt32(source.Blocks[ShapeAddress], 28, 2);
          WriteUInt32(source.Blocks[MeshAddress], 28, 2);
          break;
        case MalformedShape.AggregateCountMismatch:
          WriteUInt32(source.Blocks[ShapeAddress], 24, 4);
          break;
        case MalformedShape.UnknownResourceReference:
          source.MutableResourceReferences.Remove(MeshAddress + 4);
          WriteUInt32(source.Blocks[MeshAddress], 4, FtxAddress);
          source.Relocations[MeshAddress + 4] = FtxAddress;
          source.MutableResourcesByAddress.Remove(FtxAddress);
          break;
        case MalformedShape.UnterminatedEffectName:
          source.ReplaceBlock(NameAddress, Encoding.ASCII.GetBytes("spark"));
          break;
        case MalformedShape.NonFiniteVertex:
          WriteSingle(source.Blocks[VerticesAddress], 0, float.NaN);
          break;
        case MalformedShape.TruncatedPlacementIndices:
          UsePlacementTriangleList([0, 1, 2, 2, 1, 0]);
          source.ReplaceBlock(IndicesAddress, source.Blocks[IndicesAddress][..^1]);
          break;
        case MalformedShape.PlacementIndexCountOverflow:
          WriteUInt32(source.Blocks[MeshAddress], 12, 1);
          WriteUInt32(source.Blocks[MeshAddress], 28, uint.MaxValue);
          WriteUInt32(source.Blocks[ShapeAddress], 28, uint.MaxValue);
          break;
        case MalformedShape.OversizedEffectCount:
          WriteUInt32(source.Blocks[ShapeAddress], 44, 65_537);
          break;
        case MalformedShape.EffectNameTooLong:
          source.ReplaceBlock(
            NameAddress,
            [.. Enumerable.Repeat(Convert.ToByte('x'), 4 * 1024), Convert.ToByte(0)]);
          break;
        case MalformedShape.WrongSymbolReferenceType:
          source.MutableResourceReferences[MeshAddress + 4] =
            new StaticShapeResourceReference("opaque:txs", ShapeAddress);
          break;
        case MalformedShape.WrongSymbolReferenceArchiveType:
          source.MutableResourcesByKey["water:ftx"] =
            new StaticShapeResourceMetadata("water:ftx", "txs");
          break;
        case MalformedShape.WrongSymbolReferenceOwner:
          source.MutableResourceReferences[MeshAddress + 4] =
            new StaticShapeResourceReference("water:ftx", ShapeAddress + 1);
          break;
        case MalformedShape.ConflictingRawAndSymbolReference:
          WriteUInt32(source.Blocks[MeshAddress], 4, FtxAddress);
          break;
        case MalformedShape.WrongDirectReferenceType:
          source.MutableResourceReferences.Remove(MeshAddress + 4);
          WritePointer(source.Blocks[MeshAddress], MeshAddress, 4, FtxAddress);
          source.MutableResourcesByAddress[FtxAddress] =
            new StaticShapeResourceMetadata("water:ftx", "txs");
          break;
        case MalformedShape.MissingStaticShapeLoaderOwnership:
          source.MutableStaticShapeLoaderDataAddresses.Clear();
          break;
      }
    }

    private static byte[] EncodeIndices(uint[] indices) {
      var bytes = new byte[indices.Length * sizeof(uint)];
      for (var i = 0; i < indices.Length; i++) WriteUInt32(bytes, i * sizeof(uint), indices[i]);
      return bytes;
    }

    private void WritePointer(byte[] bytes, uint blockAddress, int offset, uint value) {
      WriteUInt32(bytes, offset, value);
      source.Relocations[blockAddress + Convert.ToUInt32(offset)] = value;
    }

    private static void WriteVertex(
      byte[] bytes,
      int offset,
      Vector3 position,
      Vector3 normal,
      Vector2 texCoord
    ) {
      WriteVector3(bytes, offset, position);
      WriteVector3(bytes, offset + 12, normal);
      WriteUInt32(bytes, offset + 24, 4_279_312_947);
      WriteSingle(bytes, offset + 28, texCoord.X);
      WriteSingle(bytes, offset + 32, texCoord.Y);
    }

    private static void WriteMatrix(byte[] bytes, int offset, Matrix4x4 value) {
      var values = new[] {
        value.M11, value.M12, value.M13, value.M14,
        value.M21, value.M22, value.M23, value.M24,
        value.M31, value.M32, value.M33, value.M34,
        value.M41, value.M42, value.M43, value.M44
      };
      for (var i = 0; i < values.Length; i++) WriteSingle(bytes, offset + i * 4, values[i]);
    }

    private static void WriteVector3(byte[] bytes, int offset, Vector3 value) {
      WriteSingle(bytes, offset, value.X);
      WriteSingle(bytes, offset + 4, value.Y);
      WriteSingle(bytes, offset + 8, value.Z);
    }

    private static void WriteSingle(byte[] bytes, int offset, float value) =>
      BitConverter.GetBytes(value).CopyTo(bytes, offset);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
      BitConverter.GetBytes(value).CopyTo(bytes, offset);

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
      BitConverter.GetBytes(value).CopyTo(bytes, offset);
  }

  private sealed class FakeStaticShapeDataSource : IStaticShapeDataSource {
    public Dictionary<uint, byte[]> Blocks { get; } = [];
    public Dictionary<uint, uint> Relocations { get; } = [];
    public Dictionary<uint, StaticShapeResourceMetadata> MutableResourcesByAddress { get; } = [];
    public Dictionary<string, StaticShapeResourceMetadata> MutableResourcesByKey { get; } =
      new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> MutableLocalResourceKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<uint, StaticShapeResourceReference> MutableResourceReferences { get; } = [];
    public HashSet<uint> MutableStaticShapeLoaderDataAddresses { get; } = [];
    public IReadOnlyDictionary<uint, StaticShapeResourceMetadata> ResourcesByAddress =>
      MutableResourcesByAddress;
    public IReadOnlyDictionary<string, StaticShapeResourceMetadata> ResourcesByKey =>
      MutableResourcesByKey;
    public IReadOnlySet<string> LocalResourceKeys => MutableLocalResourceKeys;
    public IReadOnlyDictionary<uint, StaticShapeResourceReference> ResourceReferences =>
      MutableResourceReferences;
    public IReadOnlySet<uint> StaticShapeLoaderDataAddresses => MutableStaticShapeLoaderDataAddresses;

    public byte[] AddBlock(uint address, int length) {
      var bytes = new byte[length];
      Blocks.Add(address, bytes);
      return bytes;
    }

    public void AddBlock(uint address, byte[] bytes) => Blocks.Add(address, bytes);
    public void ReplaceBlock(uint address, byte[] bytes) => Blocks[address] = bytes;

    public bool TryReadBytes(uint address, int length, out byte[] bytes) {
      foreach (var block in Blocks) {
        if (address < block.Key) continue;
        var offset = Convert.ToUInt64(address - block.Key);
        if (offset + Convert.ToUInt64(length) > Convert.ToUInt64(block.Value.Length)) continue;
        bytes = block.Value.AsSpan(Convert.ToInt32(offset), length).ToArray();
        return true;
      }
      bytes = [];
      return false;
    }

    public bool TryGetRelocationSource(uint address, out uint value) =>
      Relocations.TryGetValue(address, out value);

    public bool TryGetNullTerminatedStringByteLength(
      uint address,
      int maximumLength,
      out int length
    ) {
      foreach (var block in Blocks) {
        if (address < block.Key || address >= block.Key + block.Value.Length) continue;
        var offset = Convert.ToInt32(address - block.Key);
        var available = Math.Min(maximumLength, block.Value.Length - offset);
        var end = Array.IndexOf(block.Value, Convert.ToByte(0), offset, available);
        length = end < 0 ? 0 : end - offset;
        return end >= 0;
      }
      length = 0;
      return false;
    }

    public bool TryReadNullTerminatedString(uint address, int maximumLength, out string value) {
      foreach (var block in Blocks) {
        if (address < block.Key || address >= block.Key + block.Value.Length) continue;
        var offset = Convert.ToInt32(address - block.Key);
        var available = Math.Min(maximumLength, block.Value.Length - offset);
        var end = Array.IndexOf(block.Value, Convert.ToByte(0), offset, available);
        if (end < 0) break;
        value = Encoding.ASCII.GetString(block.Value, offset, end - offset);
        return true;
      }
      value = string.Empty;
      return false;
    }
  }
}

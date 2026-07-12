// StaticShapes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes "shs" (StaticShape) entries per rct3-importer's staticshape.h/ManagerSHS.cpp - see
// .agents/plans/features/ovl/ovl-static-shapes.md for the struct layout, allocation-order, and
// index_count semantics this mirrors.
using System.Collections.Concurrent;
using System.Numerics;
using NLog;

namespace OpenCobra.OVL.Files;

public readonly record struct Vertex(Vector3 Position, Vector3 Normal, uint Color, float Tu, float Tv);

public readonly record struct Triangle(uint A, uint B, uint C);

public readonly record struct StaticMesh(
  string Name,
  uint SupportType,
  string? FtxRef,
  string? TxsRef,
  uint Transparency,
  uint TextureFlags,
  uint Sides,
  IReadOnlyList<Vertex> Vertices,
  IReadOnlyList<Triangle> Triangles,
  IReadOnlyList<Triangle> SortedByY,
  IReadOnlyList<Triangle> SortedByZ
);

public readonly record struct EffectPoint(string Name, Matrix4x4 Position);

public readonly record struct StaticShape(
  string Name,
  Vector3 BoundingBoxMin,
  Vector3 BoundingBoxMax,
  uint TotalVertexCount,
  uint TotalIndexCount,
  uint MeshCount2,
  IReadOnlyList<StaticMesh> Meshes,
  IReadOnlyList<EffectPoint> Effects
);

public static class StaticShapes {
  // r3::Constants::Mesh::SupportType::None (rct3constants.h:233) - NOT 0.
  private const uint SupportTypeNone = 0xFFFFFFFF;

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static IReadOnlyList<StaticShape> Extract(Ovl ovl) {
    var shsFiles = ovl.Keys.Where(file => file.Type == FileType.StaticShape).ToList();
    var bag = new ConcurrentBag<StaticShape>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(shsFiles, file => {
      try {
        bag.Add(ReadStaticShape(ovl, file));
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} static shapes from {x} OVL{suffix}.",
        failures.Count,
        shsFiles.Count,
        shsFiles.Count != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  /// <summary>
  /// Decodes a single <c>shs</c>-tagged symbol without scanning the rest of the archive - for
  /// callers (e.g. Dumper's <c>shs-viewer</c> host integration) that need one shape on demand
  /// rather than every shape up front. Returns <c>null</c> instead of throwing on decode failure,
  /// mirroring <see cref="Extract"/>'s per-symbol failure handling.
  /// </summary>
  public static StaticShape? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      return ReadStaticShape(ovl, file);
    } catch (Exception ex) {
      logger.Error(ex, "Failed to decode {FileName}", file.ToString());
      return null;
    }
  }

  private static StaticShape ReadStaticShape(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var shapeAddress))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(shapeAddress, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve StaticShape block for {file.Name}");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, 56);
    var boundingBoxMin = ReadVector3(span[0..12]);
    var boundingBoxMax = ReadVector3(span[12..24]);
    var totalVertexCount = BitConverter.ToUInt32(span[24..28]);
    var totalIndexCount = BitConverter.ToUInt32(span[28..32]);
    // mesh_count2 (span[32..36]) is on-disk but not trusted - see Gaps and Risks #2.
    var meshCount = BitConverter.ToUInt32(span[36..40]);
    var effectCount = BitConverter.ToUInt32(span[44..48]);

    var meshes = new List<StaticMesh>(Convert.ToInt32(meshCount));
    if (meshCount > 0
        && ovl.TryGetRelocationSource(shapeAddress + 40, out var shArrayAddress)) {
      for (var i = 0; i < meshCount; i++) {
        var slotAddress = shArrayAddress + Convert.ToUInt32(i * 4);
        if (!ovl.TryGetRelocationSource(slotAddress, out var meshAddress)) continue;
        meshes.Add(ReadMesh(ovl, meshAddress, $"{file.Name}.mesh{i}"));
      }
    }

    var effects = new List<EffectPoint>(Convert.ToInt32(effectCount));
    if (effectCount > 0
        && ovl.TryGetRelocationSource(shapeAddress + 48, out var positionsAddress)
        && ovl.TryGetRelocationSource(shapeAddress + 52, out var namesAddress)
        && ovl.TryResolveRelocation(positionsAddress, out var positionsBlock, out var positionsOffset)) {
      for (var i = 0; i < effectCount; i++) {
        var nameSlotAddress = namesAddress + Convert.ToUInt32(i * 4);
        var name = ovl.TryGetRelocationSource(nameSlotAddress, out var nameAddress)
          && ovl.TryResolveString(nameAddress, out var resolvedName)
            ? resolvedName
            : string.Empty;

        var matrixOffset = Convert.ToInt32(positionsOffset) + i * 64;
        var matrix = ReadMatrix4x4(positionsBlock.AsSpan(matrixOffset, 64));
        effects.Add(new EffectPoint(name, matrix));
      }
    }

    var meshCount2 = Convert.ToUInt32(meshes.Count(m => m.SupportType == SupportTypeNone));

    return new StaticShape(
      file.Name, boundingBoxMin, boundingBoxMax, totalVertexCount, totalIndexCount, meshCount2, meshes, effects
    );
  }

  private static StaticMesh ReadMesh(Ovl ovl, uint meshAddress, string name) {
    if (!ovl.TryResolveRelocation(meshAddress, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve StaticShapeMesh block for '{name}'.");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, 40);
    var supportType = BitConverter.ToUInt32(span[0..4]);
    var transparency = BitConverter.ToUInt32(span[12..16]);
    var textureFlags = BitConverter.ToUInt32(span[16..20]);
    var sides = BitConverter.ToUInt32(span[20..24]);
    var vertexCount = BitConverter.ToUInt32(span[24..28]);
    var indexCount = BitConverter.ToUInt32(span[28..32]);

    var ftxRef = ovl.TryGetRelocationSource(meshAddress + 4, out var ftxAddress)
      && ovl.TryFindSymbol(ftxAddress, out var ftxFile) ? ftxFile.Name : null;
    var txsRef = ovl.TryGetRelocationSource(meshAddress + 8, out var txsAddress)
      && ovl.TryFindSymbol(txsAddress, out var txsFile) ? txsFile.Name : null;

    var vertices = new List<Vertex>(Convert.ToInt32(vertexCount));
    if (vertexCount > 0 && ovl.TryGetRelocationSource(meshAddress + 32, out var vertexesAddress)
        && ovl.TryResolveRelocation(vertexesAddress, out var vertexBlock, out var vertexOffset)) {
      var vo = Convert.ToInt32(vertexOffset);
      for (var i = 0; i < vertexCount; i++) {
        var v = vertexBlock.AsSpan(vo + i * 36, 36);
        vertices.Add(new Vertex(
          ReadVector3(v[0..12]), ReadVector3(v[12..24]), BitConverter.ToUInt32(v[24..28]),
          BitConverter.ToSingle(v[28..32]), BitConverter.ToSingle(v[32..36])
        ));
      }
    }

    var triangles = new List<Triangle>();
    var sortedByY = new List<Triangle>();
    var sortedByZ = new List<Triangle>();
    // A sort tail (2 * raw index count of extra uint32s) is present only when transparency != 0
    // and a sort algorithm is set - the encoder has no on-disk flag for "algo set" beyond the sort
    // tail's own presence, so this plan's decoder infers it from indexCount/vertexCount plausibility
    // the same way the C++ side's index_count semantics are structured: when a sort tail is
    // present, index_count is the RAW index count (divisible by 3 for well-formed data); when it
    // isn't, index_count is already a triangle count. Both branches read the same underlying bytes
    // - transparency alone gates whether we *attempt* to read a sort tail at all.
    if (indexCount > 0 && ovl.TryGetRelocationSource(meshAddress + 36, out var indicesAddress)
        && ovl.TryResolveRelocation(indicesAddress, out var indexBlock, out var indexOffset)) {
      var io = Convert.ToInt32(indexOffset);
      var count = Convert.ToInt32(indexCount);

      // UNRESOLVED (see ovl-static-shapes.md Gaps #1): the on-disk StaticShapeMesh has no field
      // recording whether a sort algorithm was set - algo_x/y/z (ManagerSHS.h:64-66) live only on
      // the writer's in-memory cStaticShape2, never serialized. `transparency != 0` alone is
      // necessary but NOT sufficient for a sort tail to exist (confirmed against real data:
      // SwingShipHLod/MLod, Straight4mTP01, OldACAMWheel all have transparency != 0 with no sort
      // tail actually present, and blindly assuming one crashed/would-have-corrupted decode). This
      // is a best-effort heuristic, not a proven decode rule: only commit to the "raw index count +
      // sort tail" interpretation if transparency != 0 AND the 3x tail actually fits in the
      // resolved block; otherwise fall back to "index_count is already a triangle count".
      var tailFits = transparency != 0 && io + count * 12 <= indexBlock.Length;
      if (tailFits) {
        // index_count is the RAW index count in this branch (ManagerSHS.cpp:129-141).
        ReadTriangles(indexBlock, io, count, triangles);
        ReadTriangles(indexBlock, io + count * 4, count, sortedByY);
        ReadTriangles(indexBlock, io + count * 8, count, sortedByZ);
      } else {
        // index_count is already a triangle count in this branch - read index_count triangles (3
        // uint32s each), not index_count raw uint32s. Also the fallback when transparency != 0 but
        // the sort tail heuristic above didn't fit (see UNRESOLVED note).
        var rawCount = Math.Min(count * 3, Math.Max(0, (indexBlock.Length - io) / 4));
        ReadTriangles(indexBlock, io, rawCount, triangles);
      }
    }

    return new StaticMesh(
      name, supportType, ftxRef, txsRef, transparency, textureFlags, sides,
      vertices, triangles, sortedByY, sortedByZ
    );
  }

  // Reads `rawCount` raw uint32 indices starting at `byteOffset` and groups them into triangles.
  private static void ReadTriangles(byte[] block, int byteOffset, int rawCount, List<Triangle> destination) {
    for (var i = 0; i + 2 < rawCount; i += 3) {
      var a = BitConverter.ToUInt32(block.AsSpan(byteOffset + i * 4, 4));
      var b = BitConverter.ToUInt32(block.AsSpan(byteOffset + (i + 1) * 4, 4));
      var c = BitConverter.ToUInt32(block.AsSpan(byteOffset + (i + 2) * 4, 4));
      destination.Add(new Triangle(a, b, c));
    }
  }

  private static Vector3 ReadVector3(ReadOnlySpan<byte> span) =>
    new(BitConverter.ToSingle(span[0..4]), BitConverter.ToSingle(span[4..8]), BitConverter.ToSingle(span[8..12]));

  // MATRIX is row-major float[4][4] (_11.._44) - same layout as System.Numerics.Matrix4x4's
  // M11..M44, so no transpose/reorder is needed.
  private static Matrix4x4 ReadMatrix4x4(ReadOnlySpan<byte> span) => new(
    BitConverter.ToSingle(span[0..4]), BitConverter.ToSingle(span[4..8]), BitConverter.ToSingle(span[8..12]), BitConverter.ToSingle(span[12..16]),
    BitConverter.ToSingle(span[16..20]), BitConverter.ToSingle(span[20..24]), BitConverter.ToSingle(span[24..28]), BitConverter.ToSingle(span[28..32]),
    BitConverter.ToSingle(span[32..36]), BitConverter.ToSingle(span[36..40]), BitConverter.ToSingle(span[40..44]), BitConverter.ToSingle(span[44..48]),
    BitConverter.ToSingle(span[48..52]), BitConverter.ToSingle(span[52..56]), BitConverter.ToSingle(span[56..60]), BitConverter.ToSingle(span[60..64])
  );
}

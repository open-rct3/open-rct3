// ManifoldMeshes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Concurrent;
using System.Numerics;
using NLog;

namespace OpenCobra.OVL.Files;

/// <summary>
/// Represents a small, static, non-animated mesh.
/// </summary>
/// <remarks>
/// Generally used as a physics mesh for objects in the game. Also useful for object picking.
/// </remarks>
public readonly record struct ManifoldMesh(
  string Name,
  BoundingBox BoundingBox,
  IReadOnlyList<Vector3> Vertices,
  IReadOnlyList<Triangle> Faces
);

public static class ManifoldMeshes {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static IReadOnlyList<ManifoldMesh> Extract(Ovl ovl) {
    var mamFiles = ovl.Keys.Where(file => file.Type == FileType.ManifoldMesh).ToList();
    var bag = new ConcurrentBag<ManifoldMesh>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(mamFiles, file => {
      try {
        bag.Add(ReadManifoldMesh(ovl, file));
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} manifold meshes from {x} OVL{suffix}.",
        failures.Count,
        mamFiles.Count,
        mamFiles.Count != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  /// <summary>
  /// Decodes a single <c>mam</c>-tagged symbol without scanning the rest of the archive - used by
  /// <see cref="SceneryItemVisuals"/> to resolve <c>SceneryItemVisual.ProxyMesh</c> on demand. Returns
  /// <c>null</c> instead of throwing on decode failure, mirroring
  /// <see cref="StaticShapes.TryExtractOne"/>'s per-symbol failure handling.
  /// </summary>
  public static ManifoldMesh? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      return ReadManifoldMesh(ovl, file);
    } catch (Exception ex) {
      logger.Error(ex, "Failed to decode {FileName}", file.ToString());
      return null;
    }
  }

  private static ManifoldMesh ReadManifoldMesh(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve ManifoldMesh block for {file.Name}");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, 48);
    var bboxMin = ReadVector3(span[0..12]);
    var bboxMax = ReadVector3(span[16..28]);
    var vertexCount = BitConverter.ToUInt32(span[32..36]);
    var faceCount = BitConverter.ToUInt32(span[36..40]);

    var vertices = new List<Vector3>(Convert.ToInt32(vertexCount));
    if (vertexCount > 0 && ovl.TryGetRelocationSource(address + 40, out var verticesAddress)
        && ovl.TryResolveRelocation(verticesAddress, out var vertexBlock, out var vertexOffset)) {
      var vo = Convert.ToInt32(vertexOffset);
      for (var i = 0; i < vertexCount; i++)
        vertices.Add(ReadVector3(vertexBlock.AsSpan(vo + i * 16, 12)));
    }

    var faces = new List<Triangle>(Convert.ToInt32(faceCount));
    if (faceCount > 0 && ovl.TryGetRelocationSource(address + 44, out var indicesAddress)
        && ovl.TryResolveRelocation(indicesAddress, out var indexBlock, out var indexOffset)) {
      var io = Convert.ToInt32(indexOffset);
      for (var i = 0; i < faceCount; i++) {
        var a = BitConverter.ToUInt16(indexBlock.AsSpan(io + i * 6, 2));
        var b = BitConverter.ToUInt16(indexBlock.AsSpan(io + i * 6 + 2, 2));
        var c = BitConverter.ToUInt16(indexBlock.AsSpan(io + i * 6 + 4, 2));
        faces.Add(new Triangle(a, b, c));
      }
    }

    return new ManifoldMesh(file.Name, new BoundingBox(bboxMin, bboxMax), vertices, faces);
  }

  private static Vector3 ReadVector3(ReadOnlySpan<byte> span) =>
    new(BitConverter.ToSingle(span[0..4]), BitConverter.ToSingle(span[4..8]), BitConverter.ToSingle(span[8..12]));
}

// StaticShapes
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using System.Text;

namespace OpenCobra.OVL.Files;

/// <summary>A decoded 36-byte RCT3 static-shape vertex.</summary>
public sealed record StaticShapeVertex(
  Vector3 Position,
  Vector3 Normal,
  Vector2 TexCoord,
  Vector4 Color
);

/// <summary>A triangle expressed as three zero-based vertex indices.</summary>
public readonly record struct Triangle(uint A, uint B, uint C);

/// <summary>A decoded mesh within an RCT3 static-shape resource.</summary>
public sealed record StaticShapeMesh(
  string Name,
  int SupportType,
  string? FtxRef,
  string? TxsRef,
  uint Transparency,
  uint TextureFlags,
  uint Sides,
  IReadOnlyList<StaticShapeVertex> Vertices,
  IReadOnlyList<uint> Indices
) {
  /// <summary>How the on-disk index payload is arranged.</summary>
  public StaticShapeIndexLayout IndexLayout { get; init; }
  /// <summary>The raw count stored in <c>StaticShapeMesh.index_count</c>.</summary>
  public uint StoredIndexCount { get; init; }
  /// <summary>The triangle count, or null when the serialized placement layout is ambiguous.</summary>
  public int? TriangleCount => IndexLayout == StaticShapeIndexLayout.PlacementAmbiguous
    ? null
    : Indices.Count / 3;
  /// <summary>Every triangle count consistent with the serialized payload.</summary>
  public IReadOnlyList<int> PossibleTriangleCounts =>
    IndexLayout == StaticShapeIndexLayout.PlacementAmbiguous
      ? [Convert.ToInt32(StoredIndexCount / 3), Convert.ToInt32(StoredIndexCount)]
      : [Indices.Count / 3];
}

/// <summary>The index layouts that can be proven from <c>ManagerSHS.cpp</c>'s serialized data.</summary>
public enum StaticShapeIndexLayout {
  TriangleList,
  PlacementTriangleList,
  PlacementAmbiguous
}

/// <summary>An effect attachment point stored on a static shape.</summary>
public sealed record ShapeEffect(string Name, Matrix4x4 Position);

/// <summary>A decoded RCT3 static-shape resource.</summary>
public sealed record StaticShape(
  string Name,
  Vector3 BoundingBoxMin,
  Vector3 BoundingBoxMax,
  IReadOnlyList<StaticShapeMesh> Meshes,
  IReadOnlyList<ShapeEffect> Effects
);

/// <summary>Decodes relocated <c>shs</c> resources without resolving them to renderer types.</summary>
public static class StaticShapes {
  // See staticshape.h, vertex.h, and ManagerSHS.cpp in rct3-importer's libOVLng.
  private const int ShapeSize = 56;
  private const int MeshSize = 40;
  private const int VertexSize = 36;
  private const int MatrixSize = 64;
  private const int PointerSize = 4;
  private const int MaximumArrayBytes = 256 * 1024 * 1024;
  private const int MaximumMeshCount = 16 * 1024;
  private const int MaximumEffectCount = 64 * 1024;
  private const int MaximumEffectNameBytes = 4 * 1024;
  private const int MaximumResourceNameBytes = 4 * 1024;
  private const int MaximumShapeCount = 64 * 1024;
  private const int MaximumResourceCount = 1_000_000;
  private const int MaximumSymbolReferenceCount = 1_000_000;

  /// <summary>Decodes every static-shape resource from the unique half of an OVL pair.</summary>
  public static IReadOnlyList<StaticShape> Extract(Ovl ovl) {
    return Extract(ovl, StaticShapeDecodeLimits.Default);
  }

  /// <summary>
  /// Decodes one <c>shs</c> resource for callers that need an optional presentation asset without
  /// failing an archive-wide import when that one symbol is malformed or unsupported.
  /// </summary>
  public static StaticShape? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      if (file.Type != FileType.StaticShape ||
          !file.Path.EndsWith(".unique.ovl", StringComparison.OrdinalIgnoreCase) ||
          !ovl.TryGetDataPointer(file, out var address)) return null;
      var context = new DecodeContext(StaticShapeDecodeLimits.Default);
      return Decode(file.Name, address, new OvlStaticShapeDataSource(ovl, context), context);
    } catch (Exception) {
      return null;
    }
  }

  internal static IReadOnlyList<StaticShape> Extract(Ovl ovl, StaticShapeDecodeLimits limits) {
    ArgumentNullException.ThrowIfNull(ovl);

    if (ovl.Count > MaximumResourceCount)
      throw new InvalidDataException(
        $"OVL resource count {ovl.Count} exceeds the SHS decoder limit {MaximumResourceCount}.");
    var context = new DecodeContext(limits);
    var shapeFiles = new List<OvlFile>();
    foreach (var file in ovl.Keys.Where(file =>
      file.Type == FileType.StaticShape &&
      file.Path.EndsWith(".unique.ovl", StringComparison.OrdinalIgnoreCase))) {
      if (shapeFiles.Count >= MaximumShapeCount)
        throw Invalid(file.Name, $"shape count exceeds the decoder limit {MaximumShapeCount}");
      context.ReserveObjects(1, file.Name, "shape resource index");
      shapeFiles.Add(file);
    }

    var source = new OvlStaticShapeDataSource(ovl, context);
    var shapes = new List<StaticShape>(shapeFiles.Count);
    foreach (var file in shapeFiles) {
      if (!ovl.TryGetDataPointer(file, out var address))
        throw Invalid(file.Name, "resource data pointer is missing");

      shapes.Add(Decode(file.Name, address, source, context));
    }
    return shapes;
  }

  internal static StaticShape Decode(string name, uint address, IStaticShapeDataSource source) =>
    Decode(name, address, source, new DecodeContext(StaticShapeDecodeLimits.Default));

  internal static StaticShape Decode(
    string name,
    uint address,
    IStaticShapeDataSource source,
    StaticShapeDecodeLimits limits
  ) => Decode(name, address, source, new DecodeContext(limits));

  private static StaticShape Decode(
    string name,
    uint address,
    IStaticShapeDataSource source,
    DecodeContext context
  ) {
    if (!source.StaticShapeLoaderDataAddresses.Contains(address))
      throw Invalid(name, $"address {address} is not owned by an shs loader-table entry");
    var header = ReadExact(source, address, ShapeSize, name, "shape header", context);
    var boundsMin = ReadVector3(header, 0);
    var boundsMax = ReadVector3(header, 12);
    ValidateBounds(name, boundsMin, boundsMax);

    var totalVertexCount = ReadUInt32(header, 24);
    var totalIndexCount = ReadUInt32(header, 28);
    var unsupportedMeshCount = ReadUInt32(header, 32);
    var meshCount = ReadUInt32(header, 36);
    if (meshCount == 0)
      throw Invalid(name, "mesh count is zero");
    if (meshCount > MaximumMeshCount)
      throw Invalid(name, $"mesh count {meshCount} exceeds the decoder limit {MaximumMeshCount}");
    if (unsupportedMeshCount > meshCount)
      throw Invalid(name, "unsupported mesh count exceeds mesh count");

    var effectCount = ReadUInt32(header, 44);
    if (effectCount > MaximumEffectCount)
      throw Invalid(name, $"effect count {effectCount} exceeds the decoder limit {MaximumEffectCount}");
    context.ReserveObjects(1 + Convert.ToUInt64(meshCount) + effectCount, name, "shape, mesh, and effect objects");

    var meshPointersAddress = ReadRequiredPointer(
      source, CheckedAdd(address, 40, name), name, "mesh pointer array");
    var meshPointerBytes = ReadArray(
      source, meshPointersAddress, meshCount, PointerSize, name, "mesh pointer array", context);
    var decodedMeshCount = ToCount(meshCount, name, "mesh count");
    var meshes = new List<StaticShapeMesh>(decodedMeshCount);
    var meshAddresses = new HashSet<uint>();
    ulong decodedVertexCount = 0;
    ulong decodedIndexCount = 0;
    uint decodedUnsupportedMeshCount = 0;

    for (var i = 0; i < decodedMeshCount; i++) {
      var meshPointerAddress = CheckedAdd(meshPointersAddress, Convert.ToUInt32(i * PointerSize), name);
      var meshAddress = ReadRequiredPointer(source, meshPointerAddress, name, $"mesh {i} pointer");
      var storedMeshAddress = ReadUInt32(meshPointerBytes, i * PointerSize);
      if (storedMeshAddress != meshAddress)
        throw Invalid(name, $"mesh {i} pointer does not match its relocation target");
      if (!meshAddresses.Add(meshAddress))
        throw Invalid(name, $"mesh {i} aliases an earlier mesh header at {meshAddress}");

      var mesh = ReadMesh(name, address, i, meshAddress, source, context);
      meshes.Add(mesh);
      decodedVertexCount += Convert.ToUInt64(mesh.Vertices.Count);
      decodedIndexCount += mesh.StoredIndexCount;
      if (mesh.SupportType == -1) decodedUnsupportedMeshCount++;
    }

    if (decodedVertexCount != totalVertexCount)
      throw Invalid(name, $"stored vertex total {totalVertexCount} does not match decoded total {decodedVertexCount}");
    if (decodedIndexCount != totalIndexCount)
      throw Invalid(name, $"stored index total {totalIndexCount} does not match decoded total {decodedIndexCount}");
    if (decodedUnsupportedMeshCount != unsupportedMeshCount)
      throw Invalid(name,
        $"stored unsupported mesh count {unsupportedMeshCount} does not match decoded count " +
        decodedUnsupportedMeshCount);

    var effects = ReadEffects(name, address, effectCount, source, context);
    return new StaticShape(name, boundsMin, boundsMax, meshes, effects);
  }

  private static StaticShapeMesh ReadMesh(
    string shapeName,
    uint shapeAddress,
    int meshIndex,
    uint meshAddress,
    IStaticShapeDataSource source,
    DecodeContext context
  ) {
    var name = $"{shapeName}/mesh/{meshIndex}";
    var bytes = ReadExact(
      source, meshAddress, MeshSize, shapeName, $"mesh {meshIndex} header", context);
    var supportType = BitConverter.ToInt32(bytes, 0);
    if (supportType != -1 && (supportType < 0 || supportType > 15))
      throw Invalid(shapeName, $"mesh {meshIndex} has invalid support type {supportType}");

    var ftxRef = ReadResourceReference(
      shapeName, shapeAddress, meshIndex, "ftx", CheckedAdd(meshAddress, 4, shapeName),
      ReadUInt32(bytes, 4), source);
    var txsRef = ReadResourceReference(
      shapeName, shapeAddress, meshIndex, "txs", CheckedAdd(meshAddress, 8, shapeName),
      ReadUInt32(bytes, 8), source);
    var transparency = ReadUInt32(bytes, 12);
    if (transparency > 2)
      throw Invalid(shapeName, $"mesh {meshIndex} has invalid transparency value {transparency}");

    var textureFlags = ReadUInt32(bytes, 16);
    var sides = ReadUInt32(bytes, 20);
    if (sides is not 1 and not 3)
      throw Invalid(shapeName, $"mesh {meshIndex} has invalid side mode {sides}");

    var vertexCount = ReadUInt32(bytes, 24);
    var storedIndexCount = ReadUInt32(bytes, 28);
    if (vertexCount == 0)
      throw Invalid(shapeName, $"mesh {meshIndex} has no vertices");
    if (storedIndexCount == 0)
      throw Invalid(shapeName, $"mesh {meshIndex} has no indices");
    if (transparency == 0 && storedIndexCount % 3 != 0)
      throw Invalid(shapeName,
        $"mesh {meshIndex} index count {storedIndexCount} is not a triangle list");

    var verticesAddress = ReadRequiredPointer(
      source, CheckedAdd(meshAddress, 32, shapeName), shapeName, $"mesh {meshIndex} vertices");
    var indicesAddress = ReadRequiredPointer(
      source, CheckedAdd(meshAddress, 36, shapeName), shapeName, $"mesh {meshIndex} indices");
    var vertices = ReadVertices(
      shapeName, meshIndex, source, verticesAddress, vertexCount, context);
    var indexData = ReadIndices(
      shapeName,
      meshIndex,
      source,
      indicesAddress,
      storedIndexCount,
      vertexCount,
      transparency != 0,
      context);
    return new StaticShapeMesh(
      name, supportType, ftxRef, txsRef, transparency, textureFlags, sides, vertices, indexData.Indices) {
      IndexLayout = indexData.Layout,
      StoredIndexCount = storedIndexCount
    };
  }

  private static IReadOnlyList<StaticShapeVertex> ReadVertices(
    string shapeName,
    int meshIndex,
    IStaticShapeDataSource source,
    uint address,
    uint count,
    DecodeContext context
  ) {
    if (context.TryGetVertices(address, count, out var cached)) return cached;
    context.ReserveObjects(count, shapeName, $"mesh {meshIndex} vertices");
    var bytes = ReadArray(
      source, address, count, VertexSize, shapeName, $"mesh {meshIndex} vertices", context);
    var vertices = new StaticShapeVertex[ToCount(count, shapeName, $"mesh {meshIndex} vertex count")];
    for (var i = 0; i < vertices.Length; i++) {
      var offset = i * VertexSize;
      var position = ReadVector3(bytes, offset);
      var normal = ReadVector3(bytes, offset + 12);
      var texCoord = new Vector2(BitConverter.ToSingle(bytes, offset + 28), BitConverter.ToSingle(bytes, offset + 32));
      if (!IsFinite(position) || !IsFinite(normal) || !IsFinite(texCoord))
        throw Invalid(shapeName, $"mesh {meshIndex} vertex {i} contains a non-finite value");

      var color = ReadUInt32(bytes, offset + 24);
      vertices[i] = new StaticShapeVertex(
        position,
        normal,
        texCoord,
        // RCT3 stores this packed as BGRA bytes in a little-endian uint.
        new Vector4(
          Convert.ToByte(color >> 16 & 255) / 255.0f,
          Convert.ToByte(color >> 8 & 255) / 255.0f,
          Convert.ToByte(color & 255) / 255.0f,
          Convert.ToByte(color >> 24 & 255) / 255.0f));
    }
    context.AddVertices(address, count, vertices, shapeName, meshIndex);
    return vertices;
  }

  private static StaticShapeIndexData ReadIndices(
    string shapeName,
    int meshIndex,
    IStaticShapeDataSource source,
    uint address,
    uint storedCount,
    uint vertexCount,
    bool placementTextured,
    DecodeContext context
  ) {
    if (context.TryGetIndices(
          address, storedCount, vertexCount, placementTextured, out var cached))
      return cached;

    var physicalCount = placementTextured
      ? CheckedMultiply(storedCount, 3, shapeName, $"mesh {meshIndex} placement index count")
      : storedCount;
    var bytes = ReadArray(
      source, address, physicalCount, sizeof(uint), shapeName, $"mesh {meshIndex} indices", context);
    context.ReserveBytes(
      Convert.ToUInt64(physicalCount) * sizeof(uint), shapeName, $"mesh {meshIndex} decoded indices");
    var indices = new uint[ToCount(physicalCount, shapeName, $"mesh {meshIndex} physical index count")];
    for (var i = 0; i < indices.Length; i++) {
      var index = ReadUInt32(bytes, i * sizeof(uint));
      if (index >= vertexCount)
        throw Invalid(shapeName,
          $"mesh {meshIndex} index {i} references vertex {index}, but only {vertexCount} vertices exist");
      indices[i] = index;
    }

    var layout = !placementTextured
      ? StaticShapeIndexLayout.TriangleList
      : storedCount % 3 == 0
        ? StaticShapeIndexLayout.PlacementAmbiguous
        : StaticShapeIndexLayout.PlacementTriangleList;
    var decoded = new StaticShapeIndexData(layout, indices);

    context.AddIndices(
      address, storedCount, vertexCount, placementTextured, decoded, shapeName, meshIndex);
    return decoded;
  }

  private static IReadOnlyList<ShapeEffect> ReadEffects(
    string shapeName,
    uint shapeAddress,
    uint effectCount,
    IStaticShapeDataSource source,
    DecodeContext context
  ) {
    if (effectCount == 0) return [];

    var positionsAddress = ReadRequiredPointer(
      source, CheckedAdd(shapeAddress, 48, shapeName), shapeName, "effect positions");
    var namesAddress = ReadRequiredPointer(
      source, CheckedAdd(shapeAddress, 52, shapeName), shapeName, "effect names");
    var positionBytes = ReadArray(
      source, positionsAddress, effectCount, MatrixSize, shapeName, "effect positions", context);
    var namePointerBytes = ReadArray(
      source, namesAddress, effectCount, PointerSize, shapeName, "effect name pointers", context);
    var effects = new ShapeEffect[ToCount(effectCount, shapeName, "effect count")];

    for (var i = 0; i < effects.Length; i++) {
      var namePointerAddress = CheckedAdd(namesAddress, Convert.ToUInt32(i * PointerSize), shapeName);
      var nameAddress = ReadRequiredPointer(source, namePointerAddress, shapeName, $"effect {i} name");
      if (ReadUInt32(namePointerBytes, i * PointerSize) != nameAddress)
        throw Invalid(shapeName, $"effect {i} name pointer does not match its relocation target");
      if (!source.TryGetNullTerminatedStringByteLength(
            nameAddress, MaximumEffectNameBytes, out var effectNameLength) || effectNameLength == 0)
        throw Invalid(shapeName,
          $"effect {i} name is missing, unterminated, or exceeds {MaximumEffectNameBytes} bytes");
      context.ReserveBytes(
        Convert.ToUInt64(effectNameLength) + 1,
        shapeName,
        $"effect {i} name");
      if (!source.TryReadNullTerminatedString(
            nameAddress, effectNameLength + 1, out var effectName) || string.IsNullOrEmpty(effectName))
        throw Invalid(shapeName, $"effect {i} name changed while it was being decoded");

      var matrix = ReadMatrix(positionBytes, i * MatrixSize);
      if (!IsFinite(matrix))
        throw Invalid(shapeName, $"effect {i} matrix contains a non-finite value");
      effects[i] = new ShapeEffect(effectName, matrix);
    }
    return effects;
  }

  private static string? ReadResourceReference(
    string shapeName,
    uint shapeAddress,
    int meshIndex,
    string tag,
    uint fieldAddress,
    uint rawValue,
    IStaticShapeDataSource source
  ) {
    // ManagerSHS stores these fields as null and emits a SymbolRefStruct naming the linker target.
    if (source.ResourceReferences.TryGetValue(fieldAddress, out var symbolReference)) {
      if (rawValue != 0)
        throw Invalid(shapeName,
          $"mesh {meshIndex} {tag} has conflicting raw and SymbolRef values");
      if (symbolReference.OwnerAddress != shapeAddress)
        throw Invalid(shapeName,
          $"mesh {meshIndex} {tag} SymbolRef belongs to another loader");
      if (!HasTag(symbolReference.Symbol, tag))
        throw Invalid(shapeName,
          $"mesh {meshIndex} {tag} SymbolRef targets '{symbolReference.Symbol}'");
      if (source.ResourcesByKey.TryGetValue(symbolReference.Symbol, out var symbolResource)) {
        if (!string.Equals(symbolResource.Tag, tag, StringComparison.OrdinalIgnoreCase))
          throw Invalid(shapeName,
            $"mesh {meshIndex} {tag} SymbolRef target '{symbolReference.Symbol}' has conflicting " +
            "archive resource metadata");
      } else if (source.LocalResourceKeys.Contains(symbolReference.Symbol))
        throw Invalid(shapeName,
          $"mesh {meshIndex} {tag} SymbolRef target '{symbolReference.Symbol}' is local but has no " +
          "validated loader metadata");
      // Stock styles such as SIOpaque:txs are external and have no local loader/resource entry.
      // Their serialized type evidence is the fixed SHS field, the verified owning shs loader,
      // the relocation-backed SymbolRef, and its matching tag. Only symbols absent from every local
      // archive symbol may take this path; local targets must match validated loader metadata above.
      return symbolReference.Symbol;
    }

    // Some already-linked archives may instead carry a direct relocated resource pointer.
    if (rawValue == 0) return null;
    if (!source.TryGetRelocationSource(fieldAddress, out var target) || target != rawValue)
      throw Invalid(shapeName, $"mesh {meshIndex} {tag} reference is not a valid relocation");
    if (!source.ResourcesByAddress.TryGetValue(target, out var resource))
      throw Invalid(shapeName, $"mesh {meshIndex} {tag} reference target {target} is not a known resource");
    if (!string.Equals(resource.Tag, tag, StringComparison.OrdinalIgnoreCase) ||
        !HasTag(resource.Key, tag))
      throw Invalid(shapeName,
        $"mesh {meshIndex} {tag} reference target '{resource.Key}' has the wrong resource type");
    return resource.Key;
  }

  private static bool HasTag(string key, string tag) =>
    key.EndsWith($":{tag}", StringComparison.OrdinalIgnoreCase);

  private static byte[] ReadArray(
    IStaticShapeDataSource source,
    uint address,
    uint count,
    int stride,
    string shapeName,
    string description,
    DecodeContext context
  ) {
    var length = ByteCount(count, stride, shapeName, description);
    return ReadExact(source, address, length, shapeName, description, context);
  }

  private static byte[] ReadExact(
    IStaticShapeDataSource source,
    uint address,
    int length,
    string shapeName,
    string description,
    DecodeContext context
  ) {
    context.ReserveBytes(Convert.ToUInt64(length), shapeName, description);
    if (!source.TryReadBytes(address, length, out var bytes) || bytes.Length != length)
      throw Invalid(shapeName, $"{description} is outside the archive or truncated");
    return bytes;
  }

  private static uint ReadRequiredPointer(
    IStaticShapeDataSource source,
    uint sourceAddress,
    string shapeName,
    string description
  ) {
    if (!source.TryGetRelocationSource(sourceAddress, out var target) || target == 0)
      throw Invalid(shapeName, $"{description} is not a non-null relocated pointer");
    return target;
  }

  private static int ByteCount(uint count, int stride, string shapeName, string description) {
    var length = Convert.ToUInt64(count) * Convert.ToUInt64(stride);
    if (length == 0 || length > MaximumArrayBytes)
      throw Invalid(shapeName, $"{description} byte length {length} is outside the decoder limit");
    return Convert.ToInt32(length);
  }

  private static int ToCount(uint count, string shapeName, string description) {
    if (count > int.MaxValue)
      throw Invalid(shapeName, $"{description} {count} exceeds the decoder limit");
    return Convert.ToInt32(count);
  }

  private static uint CheckedMultiply(uint value, uint multiplier, string shapeName, string description) {
    var result = Convert.ToUInt64(value) * multiplier;
    if (result > uint.MaxValue)
      throw Invalid(shapeName, $"{description} overflowed the OVL count range");
    return Convert.ToUInt32(result);
  }

  private static uint CheckedAdd(uint address, uint offset, string shapeName) {
    var result = Convert.ToUInt64(address) + offset;
    if (result > uint.MaxValue)
      throw Invalid(shapeName, "pointer address overflowed the OVL address space");
    return Convert.ToUInt32(result);
  }

  private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);

  private static Vector3 ReadVector3(byte[] bytes, int offset) => new(
    BitConverter.ToSingle(bytes, offset),
    BitConverter.ToSingle(bytes, offset + 4),
    BitConverter.ToSingle(bytes, offset + 8));

  private static Matrix4x4 ReadMatrix(byte[] bytes, int offset) => new(
    BitConverter.ToSingle(bytes, offset),
    BitConverter.ToSingle(bytes, offset + 4),
    BitConverter.ToSingle(bytes, offset + 8),
    BitConverter.ToSingle(bytes, offset + 12),
    BitConverter.ToSingle(bytes, offset + 16),
    BitConverter.ToSingle(bytes, offset + 20),
    BitConverter.ToSingle(bytes, offset + 24),
    BitConverter.ToSingle(bytes, offset + 28),
    BitConverter.ToSingle(bytes, offset + 32),
    BitConverter.ToSingle(bytes, offset + 36),
    BitConverter.ToSingle(bytes, offset + 40),
    BitConverter.ToSingle(bytes, offset + 44),
    BitConverter.ToSingle(bytes, offset + 48),
    BitConverter.ToSingle(bytes, offset + 52),
    BitConverter.ToSingle(bytes, offset + 56),
    BitConverter.ToSingle(bytes, offset + 60));

  private static void ValidateBounds(string name, Vector3 min, Vector3 max) {
    if (!IsFinite(min) || !IsFinite(max))
      throw Invalid(name, "bounding box contains a non-finite value");
    if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
      throw Invalid(name, "bounding box minimum exceeds its maximum");
  }

  private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
  private static bool IsFinite(Vector3 value) =>
    float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
  private static bool IsFinite(Matrix4x4 value) =>
    float.IsFinite(value.M11) && float.IsFinite(value.M12) && float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
    float.IsFinite(value.M21) && float.IsFinite(value.M22) && float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
    float.IsFinite(value.M31) && float.IsFinite(value.M32) && float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
    float.IsFinite(value.M41) && float.IsFinite(value.M42) && float.IsFinite(value.M43) && float.IsFinite(value.M44);

  private static InvalidDataException Invalid(string name, string message) =>
    new($"Static shape '{name}' is malformed: {message}.");

  private sealed record StaticShapeIndexData(
    StaticShapeIndexLayout Layout,
    IReadOnlyList<uint> Indices
  );

  private sealed class DecodeContext(StaticShapeDecodeLimits limits) {
    private readonly Dictionary<uint, (uint Count, IReadOnlyList<StaticShapeVertex> Vertices)> vertices = [];
    private readonly Dictionary<uint, IndexCacheEntry> indices = [];
    private ulong decodedBytes;
    private ulong decodedObjects;

    public void ReserveBytes(ulong count, string shapeName, string description) {
      if (count > limits.MaximumBytes || decodedBytes > limits.MaximumBytes - count)
        throw Invalid(shapeName,
          $"aggregate decode bytes exceed the limit {limits.MaximumBytes} while reading {description}");
      decodedBytes += count;
    }

    public void ReserveObjects(ulong count, string shapeName, string description) {
      if (count > limits.MaximumObjects || decodedObjects > limits.MaximumObjects - count)
        throw Invalid(shapeName,
          $"aggregate decoded objects exceed the limit {limits.MaximumObjects} while reading {description}");
      decodedObjects += count;
    }

    public bool TryGetVertices(
      uint address,
      uint count,
      out IReadOnlyList<StaticShapeVertex> value
    ) {
      if (!vertices.TryGetValue(address, out var cached)) {
        value = [];
        return false;
      }
      if (cached.Count != count)
        throw new InvalidDataException(
          $"Aliased SHS vertex data at {address} has conflicting counts {cached.Count} and {count}.");
      value = cached.Vertices;
      return true;
    }

    public void AddVertices(
      uint address,
      uint count,
      IReadOnlyList<StaticShapeVertex> value,
      string shapeName,
      int meshIndex
    ) {
      if (!vertices.TryAdd(address, (count, value)))
        throw Invalid(shapeName, $"mesh {meshIndex} vertex alias changed during decoding");
    }

    public bool TryGetIndices(
      uint address,
      uint storedCount,
      uint vertexCount,
      bool placementTextured,
      out StaticShapeIndexData value
    ) {
      if (!indices.TryGetValue(address, out var cached)) {
        value = null!;
        return false;
      }
      if (cached.StoredCount != storedCount || cached.VertexCount != vertexCount ||
          cached.PlacementTextured != placementTextured)
        throw new InvalidDataException(
          $"Aliased SHS index data at {address} has conflicting layout or count metadata.");
      value = cached.Data;
      return true;
    }

    public void AddIndices(
      uint address,
      uint storedCount,
      uint vertexCount,
      bool placementTextured,
      StaticShapeIndexData value,
      string shapeName,
      int meshIndex
    ) {
      var entry = new IndexCacheEntry(storedCount, vertexCount, placementTextured, value);
      if (!indices.TryAdd(address, entry))
        throw Invalid(shapeName, $"mesh {meshIndex} index alias changed during decoding");
    }

    private sealed record IndexCacheEntry(
      uint StoredCount,
      uint VertexCount,
      bool PlacementTextured,
      StaticShapeIndexData Data
    );
  }

  private sealed class OvlStaticShapeDataSource : IStaticShapeDataSource {
    private readonly Ovl ovl;
    private readonly DecodeContext context;
    private readonly IReadOnlyDictionary<uint, OvlLoaderEntry> loaderEntriesByStructAddress;
    private readonly Dictionary<uint, string> symbolStrings = [];

    public OvlStaticShapeDataSource(Ovl ovl, DecodeContext context) {
      this.ovl = ovl;
      this.context = context;
      if (ovl.LoaderEntriesInOrder.Count > MaximumResourceCount)
        throw new InvalidDataException(
          $"OVL loader count {ovl.LoaderEntriesInOrder.Count} exceeds the SHS decoder limit " +
          $"{MaximumResourceCount}.");

      context.ReserveObjects(
        Convert.ToUInt64(ovl.LoaderEntriesInOrder.Count) * 4,
        "OVL",
        "loader metadata index");
      var byStructAddress = new Dictionary<uint, OvlLoaderEntry>();
      var loaderMetadata = new Dictionary<uint, OvlLoaderEntry>();
      var shapeLoaders = new HashSet<uint>();
      foreach (var entry in ovl.LoaderEntriesInOrder) {
        if (!byStructAddress.TryAdd(entry.StructAddress, entry))
          throw new InvalidDataException(
            $"OVL loader struct address {entry.StructAddress} is duplicated.");
        if (loaderMetadata.TryGetValue(entry.DataAddress, out var existing) &&
            !string.Equals(existing.Tag, entry.Tag, StringComparison.OrdinalIgnoreCase))
          throw new InvalidDataException(
            $"OVL loader data address {entry.DataAddress} has conflicting archive types.");
        loaderMetadata[entry.DataAddress] = entry;
        if (entry.Tag.ToFileType() == FileType.StaticShape)
          shapeLoaders.Add(entry.DataAddress);
      }
      loaderEntriesByStructAddress = byStructAddress;
      StaticShapeLoaderDataAddresses = shapeLoaders;

      var byAddress = new Dictionary<uint, StaticShapeResourceMetadata>();
      var byKey = new Dictionary<string, StaticShapeResourceMetadata>(StringComparer.OrdinalIgnoreCase);
      var localKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var file in ovl.Keys) {
        var key = CreateResourceKey(file, context);
        context.ReserveObjects(1, key, "local resource key index");
        localKeys.Add(key);
        if (!ovl.TryGetDataPointer(file, out var address) ||
            !loaderMetadata.TryGetValue(address, out var loader))
          continue;
        context.ReserveObjects(3, key, "resource metadata indexes");
        var metadata = new StaticShapeResourceMetadata(key, loader.Tag);
        byAddress.TryAdd(address, metadata);
        if (!byKey.TryAdd(key, metadata) && byKey[key] != metadata)
          throw new InvalidDataException($"OVL resource key '{key}' has conflicting archive metadata.");
      }
      ResourcesByAddress = byAddress;
      ResourcesByKey = byKey;
      LocalResourceKeys = localKeys;
      ResourceReferences = ReadResourceReferences();
    }

    public IReadOnlyDictionary<uint, StaticShapeResourceMetadata> ResourcesByAddress { get; }
    public IReadOnlyDictionary<string, StaticShapeResourceMetadata> ResourcesByKey { get; }
    public IReadOnlySet<string> LocalResourceKeys { get; }
    public IReadOnlyDictionary<uint, StaticShapeResourceReference> ResourceReferences { get; }
    public IReadOnlySet<uint> StaticShapeLoaderDataAddresses { get; }

    public bool TryReadBytes(uint address, int length, out byte[] bytes) {
      if (ovl.TryReadBytes(address, length, out var resolved)) {
        bytes = resolved;
        return true;
      }
      bytes = [];
      return false;
    }

    public bool TryGetRelocationSource(uint address, out uint value) =>
      ovl.TryGetRelocationSource(address, out value);

    public bool TryGetNullTerminatedStringByteLength(
      uint address,
      int maximumLength,
      out int length
    ) {
      if (!ovl.TryResolveRelocation(address, out var block, out var offset)) {
        length = 0;
        return false;
      }
      return TryGetNullTerminatedByteLength(block, Convert.ToInt32(offset), maximumLength, out length);
    }

    public bool TryReadNullTerminatedString(uint address, int maximumLength, out string value) {
      if (!ovl.TryResolveRelocation(address, out var block, out var offset)) {
        value = string.Empty;
        return false;
      }

      var start = Convert.ToInt32(offset);
      var available = Math.Min(maximumLength, block.Length - start);
      var end = Array.IndexOf(block, Convert.ToByte(0), start, available);
      if (end < 0) {
        value = string.Empty;
        return false;
      }
      value = Encoding.ASCII.GetString(block, start, end - start);
      return true;
    }

    private static string CreateResourceKey(OvlFile file, DecodeContext context) {
      if (file.Type == FileType.Unknown && file.Name.Contains(':')) {
        ReserveResourceKey(file.Name, file.Name, context);
        return file.Name;
      }
      var tag = file.Type.ToTagString();
      var length = Encoding.ASCII.GetByteCount(file.Name) + 1 + Encoding.ASCII.GetByteCount(tag);
      ReserveResourceKey(length, file.Name, context);
      return $"{file.Name}:{tag}";
    }

    private static void ReserveResourceKey(string key, string name, DecodeContext context) =>
      ReserveResourceKey(Encoding.ASCII.GetByteCount(key), name, context);

    private static void ReserveResourceKey(int length, string name, DecodeContext context) {
      if (length > MaximumResourceNameBytes)
        throw new InvalidDataException(
          $"OVL resource key exceeds the SHS decoder limit {MaximumResourceNameBytes} bytes.");
      context.ReserveBytes(Convert.ToUInt64(length) + 1, name, "resource index key");
    }

    private IReadOnlyDictionary<uint, StaticShapeResourceReference> ReadResourceReferences() {
      var references = new Dictionary<uint, StaticShapeResourceReference>();
      ulong indexedRecordCount = 0;

      if (ovl.SymbolReferenceBlocksInOrder.Count > MaximumSymbolReferenceCount)
        throw new InvalidDataException(
          $"SHS SymbolRef block count exceeds the decoder limit {MaximumSymbolReferenceCount}.");
      foreach (var block in ovl.SymbolReferenceBlocksInOrder) {
        context.ReserveObjects(1, "OVL", "SymbolRef block index");
        ReadSymbolReferenceBlock(block, references, ref indexedRecordCount);
      }
      return references;
    }

    private void ReadSymbolReferenceBlock(
      OvlBlockEntry blockEntry,
      Dictionary<uint, StaticShapeResourceReference> references,
      ref ulong indexedRecordCount
    ) {
      var block = blockEntry.Data;
      var stride = blockEntry.RecordStride;
      var expectedLength = Convert.ToUInt64(blockEntry.RecordCount) * Convert.ToUInt64(stride);
      if (expectedLength != Convert.ToUInt64(block.Length))
        throw new InvalidDataException(
          $"OVL SymbolRef block size {block.Length} does not match its parsed count " +
          $"{blockEntry.RecordCount} and stride {stride}.");
      if (blockEntry.RecordCount > MaximumSymbolReferenceCount)
        throw new InvalidDataException(
          $"SHS SymbolRef count exceeds the decoder limit {MaximumSymbolReferenceCount}.");

      // Validate the complete relocation-backed record block before allocating or indexing it.
      for (var offset = 0; offset < block.Length; offset += stride) {
        var recordAddressValue = Convert.ToUInt64(blockEntry.Address) + Convert.ToUInt64(offset);
        if (recordAddressValue + 8 > uint.MaxValue)
          throw new InvalidDataException("OVL SymbolRef record address exceeds the address space.");
        var recordAddress = Convert.ToUInt32(recordAddressValue);
        var hasReferenceRelocation =
          ovl.TryGetRelocationSource(recordAddress, out var referenceAddress);
        var hasSymbolRelocation =
          ovl.TryGetRelocationSource(recordAddress + 4, out var symbolAddress);
        var hasLoaderRelocation =
          ovl.TryGetRelocationSource(recordAddress + 8, out var loaderAddress);
        if (!hasLoaderRelocation ||
            loaderAddress == 0 ||
            !loaderEntriesByStructAddress.TryGetValue(loaderAddress, out var loader) ||
            !string.Equals(
              loader.SourcePath, blockEntry.SourcePath, StringComparison.OrdinalIgnoreCase))
          throw new InvalidDataException(
            $"OVL SymbolRef owner {loaderAddress} at {recordAddress} in '{blockEntry.SourcePath}' " +
            "is not an exact loader-table entry.");
        if (!hasReferenceRelocation || referenceAddress == 0 ||
            !hasSymbolRelocation ||
            !ovl.TryResolveRelocation(referenceAddress, out _, out _) ||
            !TryGetSymbolStringLocation(
              symbolAddress, out var symbolBlock, out var symbolStart, out var symbolLength) ||
            Array.IndexOf(
              symbolBlock, Convert.ToByte(':'), symbolStart, symbolLength) < 0)
          throw new InvalidDataException(
            $"OVL SymbolRef record at {recordAddress} has invalid relocation or symbol data.");
      }

      if (indexedRecordCount + blockEntry.RecordCount > MaximumSymbolReferenceCount)
        throw new InvalidDataException(
          $"SHS SymbolRef count exceeds the decoder limit {MaximumSymbolReferenceCount}.");
      indexedRecordCount += blockEntry.RecordCount;

      for (var offset = 0; offset < block.Length; offset += stride) {
        var recordAddress = blockEntry.Address + Convert.ToUInt32(offset);
        ovl.TryGetRelocationSource(recordAddress + 8, out var loaderAddress);
        var loader = loaderEntriesByStructAddress[loaderAddress];
        if (loader.Tag.ToFileType() != FileType.StaticShape)
          continue;
        ovl.TryGetRelocationSource(recordAddress, out var referenceAddress);
        ovl.TryGetRelocationSource(recordAddress + 4, out var symbolAddress);
        if (!TryResolveSymbolString(symbolAddress, out var symbol) || !symbol.Contains(':'))
          throw new InvalidDataException($"SHS SymbolRef field {referenceAddress} has an invalid symbol.");
        var reference = new StaticShapeResourceReference(symbol, loader.DataAddress);
        if (references.TryGetValue(referenceAddress, out var existing) && existing != reference)
          throw new InvalidDataException(
            $"SHS SymbolRef field {referenceAddress} has conflicting targets or owners.");
        if (references.ContainsKey(referenceAddress)) continue;
        context.ReserveObjects(1, symbol, "SymbolRef index");
        references.Add(referenceAddress, reference);
      }
    }

    private bool TryResolveSymbolString(uint address, out string value) {
      if (symbolStrings.TryGetValue(address, out value!)) return true;
      if (!TryGetSymbolStringLocation(address, out var block, out var start, out var length)) {
        value = string.Empty;
        return false;
      }
      context.ReserveBytes(Convert.ToUInt64(length) + 1, "OVL", "SymbolRef string");
      context.ReserveObjects(1, "OVL", "SymbolRef string cache entry");
      value = Encoding.ASCII.GetString(block, start, length);
      symbolStrings.Add(address, value);
      return true;
    }

    private bool TryGetSymbolStringLocation(
      uint address,
      out byte[] block,
      out int start,
      out int length
    ) {
      if (address != 0) {
        if (!ovl.TryResolveRelocation(address, out block!, out var offset)) {
          start = 0;
          length = 0;
          return false;
        }
        start = Convert.ToInt32(offset);
        return TryGetNullTerminatedByteLength(block, start, MaximumResourceNameBytes, out length);
      }

      // Symbol strings may legitimately begin at virtual address zero. The relocation on the
      // SymbolRef field proves pointer intent; resolving address one proves a block starts at zero.
      if (!ovl.TryResolveRelocation(1, out block!, out var offsetAtOne) || offsetAtOne != 1) {
        start = 0;
        length = 0;
        return false;
      }
      start = 0;
      return TryGetNullTerminatedByteLength(block, start, MaximumResourceNameBytes, out length);
    }

    private static bool TryGetNullTerminatedByteLength(
      byte[] block,
      int start,
      int maximumLength,
      out int length
    ) {
      if (start < 0 || start >= block.Length || maximumLength <= 0) {
        length = 0;
        return false;
      }
      var available = Math.Min(maximumLength, block.Length - start);
      var end = Array.IndexOf(block, Convert.ToByte(0), start, available);
      length = end < 0 ? 0 : end - start;
      return end >= 0;
    }
  }
}

internal sealed record StaticShapeResourceReference(string Symbol, uint OwnerAddress);
internal sealed record StaticShapeResourceMetadata(string Key, string Tag);

internal readonly record struct StaticShapeDecodeLimits(ulong MaximumBytes, ulong MaximumObjects) {
  public static StaticShapeDecodeLimits Default { get; } = new(256UL * 1024 * 1024, 1_000_000);
}

internal interface IStaticShapeDataSource {
  IReadOnlyDictionary<uint, StaticShapeResourceMetadata> ResourcesByAddress { get; }
  IReadOnlyDictionary<string, StaticShapeResourceMetadata> ResourcesByKey { get; }
  IReadOnlySet<string> LocalResourceKeys { get; }
  IReadOnlyDictionary<uint, StaticShapeResourceReference> ResourceReferences { get; }
  IReadOnlySet<uint> StaticShapeLoaderDataAddresses { get; }
  bool TryReadBytes(uint address, int length, out byte[] bytes);
  bool TryGetRelocationSource(uint address, out uint value);
  bool TryGetNullTerminatedStringByteLength(uint address, int maximumLength, out int length);
  bool TryReadNullTerminatedString(uint address, int maximumLength, out string value);
}

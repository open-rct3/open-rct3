// SceneryItemVisuals
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes "svd" (SceneryItemVisual) entries per rct3-importer's sceneryvisual.h/ManagerSVD.cpp.
using System.Collections.Concurrent;
using NLog;

namespace OpenCobra.OVL.Files;

/// <summary>
/// A single LOD model, resolved from the on-disk 72-byte <c>SceneryItemVisualLOD</c> struct
/// (<c>type</c>, <c>lod_name*</c>, <c>shs_ref*</c>, <c>bsh_ref*</c>, <c>ftx_ref*</c>, <c>txs_ref*</c>,
/// <c>distance</c>, <c>animation_count</c>, <c>animations_ref*</c>). Mesh/texture refs are kept as raw
/// symbol names - see the plan's Dependencies section for why SHS/FTX/TXS aren't decoded here.
/// </summary>
public readonly record struct LodEntry(
  string Name,
  SvdLodType MeshType,
  string? StaticShapeRef,
  string? BoneShapeRef,
  string? FtsRef,
  string? TxsRef,
  float LodDistance,
  IReadOnlyList<string> AnimationRefs
);

/// <summary>
/// Resolved from the on-disk 52-byte <c>SceneryItemVisual_V</c> struct (<c>sivflags</c>, <c>sway</c>,
/// <c>brightness</c>, <c>scale</c>, <c>lod_count</c>, <c>lods*</c>). <see cref="ProxyMesh"/> comes from
/// the further 4-byte <c>SceneryItemVisual_Sext</c> block (<c>proxy_ref*</c>) appended when
/// <see cref="Flags"/> has <see cref="SvdFlags.Soaked"/> or <see cref="SvdFlags.Wild"/> set.
/// </summary>
public readonly record struct SceneryItemVisual(
  string Name,
  SvdFlags Flags,
  float Sway,
  float Brightness,
  float Scale,
  IReadOnlyList<LodEntry> Lods,
  ManifoldMesh? ProxyMesh
);

public static class SceneryItemVisuals {
  private const int VisualStructSize = 52;
  private const int LodStructSize = 72;

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static IReadOnlyList<SceneryItemVisual> Extract(Ovl ovl) {
    var svdFiles = ovl.Keys.Where(file => file.Type == FileType.SceneryItemVisual).ToList();
    var bag = new ConcurrentBag<SceneryItemVisual>();
    var failures = new ConcurrentBag<OvlFile>();

    Parallel.ForEach(svdFiles, file => {
      try {
        bag.Add(ReadSvd(ovl, file));
      } catch (Exception ex) {
        logger.Error(ex, "Failed to decode {FileName}", file.ToString());
        failures.Add(file);
      }
    });

    if (!failures.IsEmpty)
      logger.Error(
        "Failed to decode {count} scenery item visuals from {x} OVL{suffix}.",
        failures.Count,
        svdFiles.Count,
        svdFiles.Count != 1 ? "s" : string.Empty
      );

    return [.. bag];
  }

  /// <summary>
  /// Decodes a single <c>svd</c>-tagged symbol without scanning the rest of the archive - for callers
  /// (e.g. Dumper's <c>sid-viewer</c> host integration) that need one visual on demand while resolving
  /// a <c>SceneryItem.SvdRefs</c> entry. Returns <c>null</c> instead of throwing on decode failure,
  /// mirroring <see cref="StaticShapes.TryExtractOne"/>'s per-symbol failure handling.
  /// </summary>
  public static SceneryItemVisual? TryExtractOne(Ovl ovl, OvlFile file) {
    try {
      return ReadSvd(ovl, file);
    } catch (Exception ex) {
      logger.Error(ex, "Failed to decode {FileName}", file.ToString());
      return null;
    }
  }

  private static SceneryItemVisual ReadSvd(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var svdAddress))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(svdAddress, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve SceneryItemVisual block for {file.Name}");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, VisualStructSize);
    var flags = (SvdFlags) BitConverter.ToUInt32(span[0..4]);
    var sway = BitConverter.ToSingle(span[4..8]);
    var brightness = BitConverter.ToSingle(span[8..12]);
    var scale = BitConverter.ToSingle(span[16..20]);
    var lodCount = BitConverter.ToUInt32(span[20..24]);

    var lods = new List<LodEntry>(Convert.ToInt32(lodCount));
    if (lodCount > 0 && ovl.TryGetRelocationSource(svdAddress + 24, out var lodArrayAddress)) {
      for (var i = 0; i < lodCount; i++) {
        var slotAddress = lodArrayAddress + Convert.ToUInt32(i * 4);
        if (!ovl.TryGetRelocationSource(slotAddress, out var lodAddress)) continue;
        lods.Add(ReadLod(ovl, lodAddress));
      }
    }

    // SceneryItemVisual_Sext (proxy_ref) is appended directly after the V struct whenever Soaked or
    // Wild is set - same offset regardless of which of the two flags is set (Wext, if present, comes
    // after Sext, so it never shifts proxy_ref's own offset). proxy_ref is assignSymbolReference-driven
    // (ManagerSVD.cpp), a cross-resource reference resolved via the symbol-reference table, not the
    // base relocation-fixup table.
    ManifoldMesh? proxyMesh = null;
    if ((flags & SvdFlags.SoakedOrWild) != 0
        && ovl.TryResolveSymbolReference(svdAddress + Convert.ToUInt32(VisualStructSize), out var proxyFile)) {
      proxyMesh = ManifoldMeshes.TryExtractOne(ovl, proxyFile);
    }

    return new SceneryItemVisual(file.Name, flags, sway, brightness, scale, lods, proxyMesh);
  }

  private static LodEntry ReadLod(Ovl ovl, uint lodAddress) {
    if (!ovl.TryResolveRelocation(lodAddress, out var block, out var offset))
      throw new InvalidOperationException("Failed to resolve SceneryItemVisualLOD block.");

    var o = Convert.ToInt32(offset);
    var span = block.AsSpan(o, LodStructSize);
    var meshType = (SvdLodType) BitConverter.ToUInt32(span[0..4]);
    // lod_name is a plain string (base relocation); shs_ref/bsh_ref/ftx_ref/txs_ref are
    // assignSymbolReference-driven cross-resource references (ManagerSVD.cpp), resolved via the
    // symbol-reference table instead.
    var name = ovl.TryGetRelocationSource(lodAddress + 4, out var nameAddress)
      && ovl.TryResolveString(nameAddress, out var resolvedName) ? resolvedName : string.Empty;
    var staticShapeRef = ovl.TryResolveSymbolReference(lodAddress + 8, out var shsFile) ? shsFile.Name : null;
    var boneShapeRef = ovl.TryResolveSymbolReference(lodAddress + 16, out var bshFile) ? bshFile.Name : null;
    var ftsRef = ovl.TryResolveSymbolReference(lodAddress + 24, out var ftxFile) ? ftxFile.Name : null;
    var txsRef = ovl.TryResolveSymbolReference(lodAddress + 28, out var txsFile) ? txsFile.Name : null;
    var distance = BitConverter.ToSingle(span[56..60]);
    var animationCount = BitConverter.ToUInt32(span[60..64]);

    var animationRefs = new List<string>(Convert.ToInt32(animationCount));
    if (animationCount > 0 && ovl.TryGetRelocationSource(lodAddress + 68, out var animArrayAddress)) {
      for (var a = 0; a < animationCount; a++) {
        // Double indirection (BoneAnim*** on disk): the array slot is a regular (base) relocation to
        // an extra single-pointer slot ManagerSVD.cpp allocates per animation; THAT slot's address is
        // the actual assignSymbolReference target, resolved via the symbol-reference table.
        var arraySlotAddress = animArrayAddress + Convert.ToUInt32(a * 4);
        if (!ovl.TryGetRelocationSource(arraySlotAddress, out var ptrSlotAddress)) continue;
        if (ovl.TryResolveSymbolReference(ptrSlotAddress, out var banFile)) animationRefs.Add(banFile.Name);
      }
    }

    return new LodEntry(name, meshType, staticShapeRef, boneShapeRef, ftsRef, txsRef, distance, animationRefs);
  }
}

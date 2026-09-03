// Track Segment Decoding and Data Structures
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.OVL.Files;

/// <summary>Decoded distance and derivative lookup table for a spline segment.</summary>
public record struct Segment {
  /// <summary>14 distance samples along the spline segment.</summary>
  public byte[] Samples;

  /// <summary>
  /// Decodes normalized distance samples into actual distances along the segment.
  /// </summary>
  public readonly float[] GetCumulativeDistances(float segmentLength) {
    if (Samples == null || Samples.Length == 0) return [];
    var result = new float[Samples.Length];
    for (var i = 0; i < Samples.Length; i++) {
      result[i] = (Samples[i] / 255.0f) * segmentLength;
    }
    return result;
  }
}

/// <summary>Single node in a 3D cubic Bezier spline with control points.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SplineNode {
  /// <summary>3D coordinate position in world space.</summary>
  public Vector3 Pos;
  /// <summary>First cubic Bezier control point relative to Pos.</summary>
  public Vector3 ControlPoint1;
  /// <summary>Second cubic Bezier control point relative to Pos.</summary>
  public Vector3 ControlPoint2;
}

/// <summary>
/// On-disk binary header for Spline resources (32 bytes).
/// Matches rct3-importer's Spline structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SplineData {
  /// <summary>Number of nodes in the spline (each node is 36 bytes).</summary>
  public uint NodeCount;
  /// <summary>Relocated pointer to array of SplineNode structs.</summary>
  public uint NodesPtr;
  /// <summary>1 if spline loops back to start, 0 if open.</summary>
  public uint Cyclic;
  /// <summary>Total arc length of the spline curve in world units.</summary>
  public float TotalLength;
  /// <summary>Precomputed reciprocal (1.0 / TotalLength) for normalization.</summary>
  public float InvTotalLength;
  /// <summary>Relocated pointer to array of float segment lengths.</summary>
  public uint LengthsPtr;
  /// <summary>Relocated pointer to array of Segment data structs.</summary>
  public uint DataPtr;
  /// <summary>Maximum Y (elevation) value reached along the curve.</summary>
  public float MaxY;
}

/// <summary>
/// Represents a decoded Spline resource from an OVL archive.
/// </summary>
/// <remarks>
/// Domain-agnostic DTO: contains raw geometric curve data without interpretation.
/// See rct3-importer's spline.h for binary structure definition.
/// </remarks>
public readonly record struct Spline(
  /// <summary>Spline resource identifier (OVL symbol name).</summary>
  string Id,
  /// <summary>Number of control nodes defining the spline curve.</summary>
  uint NodeCount,
  /// <summary>Array of 3D node positions along the curve.</summary>
  Vector3[] Nodes,
  /// <summary>First Bezier control point for each node (relative offset).</summary>
  Vector3[] ControlPoint1,
  /// <summary>Second Bezier control point for each node (relative offset).</summary>
  Vector3[] ControlPoint2,
  /// <summary>True if spline forms a closed loop, false if open.</summary>
  bool Cyclic,
  /// <summary>Total arc length of the entire spline curve in world units.</summary>
  float TotalLength,
  /// <summary>Reciprocal of TotalLength for fast normalization (1 / TotalLength).</summary>
  float InvTotalLength,
  /// <summary>
  /// Distance between each node.
  /// Length = NodeCount - 1 for open splines, NodeCount for cyclic.
  /// </summary>
  float[] SegmentLengths,
  /// <summary>Decoded segment data with 14 distance samples per segment.</summary>
  Segment[] Segments,
  /// <summary>Maximum Y coordinate (height) in the spline.</summary>
  float MaxY
);

/// <summary>
/// Binary representation of vanilla base-game track section.
/// Does not include Soaked/Wild extensions.
/// </summary>
/// <remarks>
/// Mirrors rct3-importer's <c>TrackSection_V</c> structure.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 140)]
internal struct TrackSectionVanilla {
  public uint InternalNamePtr;
  public uint SceneryItemRef;
  public uint EntryCurve;
  public uint ExitCurve;
  public uint SpecialCurves;
  public uint Direction;
  public uint EntryFlags;
  public uint ExitFlags;
  public uint SplineLeftRef;
  public uint SplineRightRef;
  public uint JoinSplineLeftRef;
  public uint JoinSplineRightRef;
  public uint ExtraSplineLeftRef;
  public uint ExtraSplineRightRef;
  public uint Unk15;
  public uint Unk16;
  public uint Unk17;
  public uint Unk18;
  public uint EntrySlope;
  public uint EntryBank;
  public uint EntryTrackGroupPtr;
  public uint Unk22;
  public uint Unk23;
  public uint Unk24;
  public uint Unk25;
  public uint ExitSlope;
  public uint ExitBank;
  public uint ExitTrackGroupPtr;
  public uint SpeedCount;
  public uint SpeedsPtr;
  public uint TowerRideBaseFlag;
  public float TowerUnkf01;
  public float WaterSplash01;
  public float WaterSplash02;
  public float ReverserVal;
}

/// <summary>
/// Represents a TrackSection entry from an OVL archive.
/// </summary>
/// <remarks>
/// Domain-agnostic DTO: contains raw binary-decoded data without interpretation.
/// Tracks vanilla (V) format only; Soaked/Wild extensions not yet decoded.
/// See rct3-importer's tracksection.h for binary structure definition.
/// </remarks>
public readonly record struct TrackSection(
  /// <summary>TrackSection resource identifier (OVL symbol name).</summary>
  string Id,
  /// <summary>Human-readable internal name from the resource.</summary>
  string InternalName,
  /// <summary>Vehicle/scenery item reference identifier (Train ID / SID pointer).</summary>
  uint SceneryItemRef,
  /// <summary>Entry curve type: purpose unclear from RCT3 source.</summary>
  uint EntryCurve,
  /// <summary>Exit curve type: purpose unclear from RCT3 source.</summary>
  uint ExitCurve,
  /// <summary>Entry slope: 0=flat, 1-2=medium, 3-4=steep, 5=vertical.</summary>
  uint EntrySlope,
  /// <summary>Exit slope: 0=flat, 1-2=medium, 3-4=steep, 5=vertical.</summary>
  uint ExitSlope,
  /// <summary>Entry banking: 0=flat, 1-2=left, 3=inverted-left, 4=inverted, 5-6=right, 7=bank-right.</summary>
  uint EntryBank,
  /// <summary>Exit banking: 0=flat, 1-2=left, 3=inverted-left, 4=inverted, 5-6=right, 7=bank-right.</summary>
  uint ExitBank,
  /// <summary>Entry direction: 0=straight, 1=left, 2=right.</summary>
  uint EntryDirection,
  /// <summary>Exit direction: 0=straight, 1=left, 2=right.</summary>
  uint ExitDirection,
  /// <summary>Special curve type classification bitflags.</summary>
  uint SpecialCurves,
  /// <summary>Entry behavior bitflags.</summary>
  uint EntryFlags,
  /// <summary>Exit behavior bitflags.</summary>
  uint ExitFlags,
  /// <summary>
  /// Six spline references (all required for track geometry).
  /// Order: [left, right, join-left, join-right, extra-left, extra-right].
  /// </summary>
  string[] SplineRefs,
  /// <summary>Tower ride base height value, usually 0.</summary>
  float TowerRideBase,
  /// <summary>Water splash effect value 1, usually 0.</summary>
  float WaterSplash1,
  /// <summary>Water splash effect value 2, usually 0.</summary>
  float WaterSplash2,
  /// <summary>Reverser track value, usually 0.</summary>
  float ReverserVal,
  /// <summary>Elevator top value, usually 0.</summary>
  float ElevatorTopVal,
  /// <summary>Number of speed modifier entries.</summary>
  uint SpeedCount,
  /// <summary>True if all referenced splines exist in the archive, false if any reference is unresolved.</summary>
  bool IsValid
);

public static class TrackData {
  /// <summary>Extracts all Spline entries from an OVL archive.</summary>
  public static IReadOnlyList<Spline> ExtractSplines(Ovl ovl) {
    var splFiles = ovl.Keys.Where(file => file.Type == FileType.Spline).ToList();
    var splines = new List<Spline>(splFiles.Count);

    foreach (var file in splFiles) {
      splines.Add(ReadSpline(ovl, file));
    }

    return splines;
  }

  /// <summary>Extracts all TrackSection entries from an OVL archive with referential validation.</summary>
  public static IReadOnlyList<TrackSection> ExtractTrackSections(Ovl ovl) {
    var splFiles = ExtractSplines(ovl);
    var splById = splFiles.ToDictionary(spl => spl.Id);

    var tksFiles = ovl.Keys.Where(file => file.Type == FileType.TrackSection).ToList();
    var sections = new List<TrackSection>(tksFiles.Count);

    foreach (var file in tksFiles) {
      var section = ReadTrackSection(ovl, file);
      var hasSplines = section.SplineRefs.Any(r => !string.IsNullOrEmpty(r));
      var isValid = hasSplines && section.SplineRefs.Where(r => !string.IsNullOrEmpty(r)).All(r => !r.StartsWith("<unresolved") && splById.ContainsKey(r));
      sections.Add(section with { IsValid = isValid });
    }

    return sections;
  }

  /// <summary>Parses a standalone spline binary payload into an <see cref="Spline"/>.</summary>
  public static Spline ParseSpline(
    ReadOnlySpan<byte> headerBytes,
    ReadOnlySpan<byte> nodesBytes = default,
    ReadOnlySpan<byte> lengthsBytes = default,
    ReadOnlySpan<byte> dataBytes = default,
    string id = "spline"
  ) {
    if (headerBytes.Length < 32)
      throw new InvalidDataException("Spline binary data too short (minimum 32 bytes required)");

    var nodeCount = BitConverter.ToUInt32(headerBytes[0..4]);
    var cyclic = BitConverter.ToUInt32(headerBytes[8..12]) != 0;
    var totalLength = BitConverter.ToSingle(headerBytes[12..16]);
    var invTotalLength = BitConverter.ToSingle(headerBytes[16..20]);
    var maxY = BitConverter.ToSingle(headerBytes[28..32]);

    var nodes = new Vector3[nodeCount];
    var cp1 = new Vector3[nodeCount];
    var cp2 = new Vector3[nodeCount];

    var nBytes = nodesBytes.IsEmpty && headerBytes.Length >= 32 + (int)nodeCount * 36 ? headerBytes[32..] : nodesBytes;
    if (nodeCount > 0) {
      if (nBytes.Length < (int)nodeCount * 36)
        throw new InvalidDataException($"Spline nodes data too short: expected {(int)nodeCount * 36} bytes, got {nBytes.Length}");

      for (var i = 0; i < (int)nodeCount; i++) {
        var offset = i * 36;
        var px = BitConverter.ToSingle(nBytes.Slice(offset, 4));
        var py = BitConverter.ToSingle(nBytes.Slice(offset + 4, 4));
        var pz = BitConverter.ToSingle(nBytes.Slice(offset + 8, 4));
        var c1x = BitConverter.ToSingle(nBytes.Slice(offset + 12, 4));
        var c1y = BitConverter.ToSingle(nBytes.Slice(offset + 16, 4));
        var c1z = BitConverter.ToSingle(nBytes.Slice(offset + 20, 4));
        var c2x = BitConverter.ToSingle(nBytes.Slice(offset + 24, 4));
        var c2y = BitConverter.ToSingle(nBytes.Slice(offset + 28, 4));
        var c2z = BitConverter.ToSingle(nBytes.Slice(offset + 32, 4));

        if (!float.IsFinite(px) || !float.IsFinite(py) || !float.IsFinite(pz) ||
            !float.IsFinite(c1x) || !float.IsFinite(c1y) || !float.IsFinite(c1z) ||
            !float.IsFinite(c2x) || !float.IsFinite(c2y) || !float.IsFinite(c2z)) {
          throw new InvalidDataException($"Spline node {i} contains non-finite coordinates or control points");
        }

        nodes[i] = new Vector3(px, py, pz);
        cp1[i] = new Vector3(c1x, c1y, c1z);
        cp2[i] = new Vector3(c2x, c2y, c2z);
      }
    }

    var segmentCount = (int)(cyclic ? nodeCount : (nodeCount > 0 ? nodeCount - 1 : 0));
    var segmentLengths = new float[segmentCount];
    if (!lengthsBytes.IsEmpty && lengthsBytes.Length >= segmentCount * 4) {
      for (var i = 0; i < segmentCount; i++)
        segmentLengths[i] = BitConverter.ToSingle(lengthsBytes.Slice(i * 4, 4));
    }

    var segments = new Segment[segmentCount];
    if (!dataBytes.IsEmpty && dataBytes.Length >= segmentCount * 14) {
      for (var i = 0; i < segmentCount; i++) {
        var segBytes = dataBytes.Slice(i * 14, 14).ToArray();
        segments[i] = new Segment { Samples = segBytes };
      }
    }

    return new Spline(
      id,
      nodeCount,
      nodes,
      cp1,
      cp2,
      cyclic,
      totalLength,
      invTotalLength,
      segmentLengths,
      segments,
      maxY
    );
  }

  /// <summary>Parses a standalone TrackSection binary payload into an <see cref="TrackSection"/>.</summary>
  public static TrackSection ParseTrackSection(
    ReadOnlySpan<byte> data,
    string id = "test_section",
    string internalName = "Test Section",
    Func<uint, string>? resolveSplineSymbol = null,
    IReadOnlySet<string>? availableSplines = null
  ) {
    if (data.Length < 140)
      throw new InvalidDataException("TrackSection binary data too short (minimum 140 bytes required)");

    var sceneryItemRef = BitConverter.ToUInt32(data[4..8]);
    var entryCurve = BitConverter.ToUInt32(data[8..12]);
    var exitCurve = BitConverter.ToUInt32(data[12..16]);
    var specialCurves = BitConverter.ToUInt32(data[16..20]);
    var direction = BitConverter.ToUInt32(data[20..24]);
    var entryFlags = BitConverter.ToUInt32(data[24..28]);
    var exitFlags = BitConverter.ToUInt32(data[28..32]);

    var splineLeftRef = BitConverter.ToUInt32(data[32..36]);
    var splineRightRef = BitConverter.ToUInt32(data[36..40]);
    var joinSplineLeftRef = BitConverter.ToUInt32(data[40..44]);
    var joinSplineRightRef = BitConverter.ToUInt32(data[44..48]);
    var extraSplineLeftRef = BitConverter.ToUInt32(data[48..52]);
    var extraSplineRightRef = BitConverter.ToUInt32(data[52..56]);

    var entrySlope = BitConverter.ToUInt32(data[72..76]);
    var entryBank = BitConverter.ToUInt32(data[76..80]);
    var exitSlope = BitConverter.ToUInt32(data[100..104]);
    var exitBank = BitConverter.ToUInt32(data[104..108]);

    var speedCount = BitConverter.ToUInt32(data[112..116]);
    var towerUnkf01 = BitConverter.ToSingle(data[124..128]);
    var waterSplash01 = BitConverter.ToSingle(data[128..132]);
    var waterSplash02 = BitConverter.ToSingle(data[132..136]);
    var reverserVal = BitConverter.ToSingle(data[136..140]);

    string Resolve(uint ptr) {
      if (ptr == 0) return string.Empty;
      if (resolveSplineSymbol != null) {
        var s = resolveSplineSymbol(ptr);
        return string.IsNullOrEmpty(s) ? $"<unresolved:0x{ptr:X}>" : s;
      }
      return $"spline_0x{ptr:X}";
    }

    var splRefs = new[] {
      Resolve(splineLeftRef),
      Resolve(splineRightRef),
      Resolve(joinSplineLeftRef),
      Resolve(joinSplineRightRef),
      Resolve(extraSplineLeftRef),
      Resolve(extraSplineRightRef)
    };

    var hasSplines = splRefs.Any(r => !string.IsNullOrEmpty(r));
    var isValid = hasSplines && splRefs.Where(r => !string.IsNullOrEmpty(r)).All(r => !r.StartsWith("<unresolved") && (availableSplines == null || availableSplines.Contains(r)));

    return new TrackSection(
      id,
      internalName,
      sceneryItemRef,
      entryCurve,
      exitCurve,
      entrySlope,
      exitSlope,
      entryBank,
      exitBank,
      direction & 0x3,
      (direction >> 2) & 0x3,
      specialCurves,
      entryFlags,
      exitFlags,
      splRefs,
      towerUnkf01,
      waterSplash01,
      waterSplash02,
      reverserVal,
      0f,
      speedCount,
      isValid
    );
  }

  private static Spline ReadSpline(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve Spline block for {file.Name}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    if (reader.Read<SplineData>(out var splineData) == 0)
      throw new InvalidDataException($"Failed to read Spline header for {file.Name}");

    var nodeCount = splineData.NodeCount;
    var cyclic = splineData.Cyclic != 0;

    var nodes = new Vector3[nodeCount];
    var cp1 = new Vector3[nodeCount];
    var cp2 = new Vector3[nodeCount];
    ReadSplineNodes(ovl, splineData.NodesPtr, nodeCount, nodes, cp1, cp2);

    var segmentLengths = ReadFloatArray(ovl, splineData.LengthsPtr, cyclic ? nodeCount : nodeCount - 1);
    var segments = ReadSegmentDataArray(ovl, splineData.DataPtr, cyclic ? nodeCount : nodeCount - 1);

    return new Spline(
      file.Name,
      nodeCount,
      nodes,
      cp1,
      cp2,
      cyclic,
      splineData.TotalLength,
      splineData.InvTotalLength,
      segmentLengths,
      segments,
      splineData.MaxY
    );
  }

  private static TrackSection ReadTrackSection(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve TrackSection block for {file.Name}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    if (reader.Read<TrackSectionVanilla>(out var tksData) == 0)
      throw new InvalidDataException($"Failed to read TrackSection header for {file.Name}");

    var internalName = string.Empty;
    if (ovl.TryGetRelocationSource(address, out var nameAddress) && ovl.TryResolveString(nameAddress, out var resolvedName))
      internalName = resolvedName;
    else if (tksData.InternalNamePtr != 0 && ovl.TryResolveString(tksData.InternalNamePtr, out var resolvedName2))
      internalName = resolvedName2;
    else
      internalName = tksData.InternalNamePtr != 0 ? $"<unresolved:{tksData.InternalNamePtr:X}>" : string.Empty;

    var splRefs = new string[6] {
      ResolveSplineReference(ovl, address + 32, tksData.SplineLeftRef),
      ResolveSplineReference(ovl, address + 36, tksData.SplineRightRef),
      ResolveSplineReference(ovl, address + 40, tksData.JoinSplineLeftRef),
      ResolveSplineReference(ovl, address + 44, tksData.JoinSplineRightRef),
      ResolveSplineReference(ovl, address + 48, tksData.ExtraSplineLeftRef),
      ResolveSplineReference(ovl, address + 52, tksData.ExtraSplineRightRef)
    };

    return new TrackSection(
      file.Name,
      internalName,
      tksData.SceneryItemRef,
      tksData.EntryCurve,
      tksData.ExitCurve,
      tksData.EntrySlope,
      tksData.ExitSlope,
      tksData.EntryBank,
      tksData.ExitBank,
      tksData.Direction & 0x3,
      (tksData.Direction >> 2) & 0x3,
      tksData.SpecialCurves,
      tksData.EntryFlags,
      tksData.ExitFlags,
      splRefs,
      tksData.TowerUnkf01,
      tksData.WaterSplash01,
      tksData.WaterSplash02,
      tksData.ReverserVal,
      0f,
      tksData.SpeedCount,
      false
    );
  }

  private static void ReadSplineNodes(Ovl ovl, uint ptr, uint count, Vector3[] nodes, Vector3[] cp1, Vector3[] cp2) {
    if (ptr == 0)
      throw new InvalidDataException("Spline nodes pointer is null");
    if (!ovl.TryResolveRelocation(ptr, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve spline nodes pointer 0x{ptr:X}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    for (var i = 0; i < count; i++) {
      if (reader.Read<SplineNode>(out var nodeData) == 0)
        throw new InvalidDataException($"Failed to read spline node {i}");
      if (!float.IsFinite(nodeData.Pos.X) || !float.IsFinite(nodeData.Pos.Y) || !float.IsFinite(nodeData.Pos.Z) ||
          !float.IsFinite(nodeData.ControlPoint1.X) || !float.IsFinite(nodeData.ControlPoint1.Y) || !float.IsFinite(nodeData.ControlPoint1.Z) ||
          !float.IsFinite(nodeData.ControlPoint2.X) || !float.IsFinite(nodeData.ControlPoint2.Y) || !float.IsFinite(nodeData.ControlPoint2.Z)) {
        throw new InvalidDataException($"Spline node {i} contains non-finite coordinates or control points");
      }
      nodes[i] = nodeData.Pos;
      cp1[i] = nodeData.ControlPoint1;
      cp2[i] = nodeData.ControlPoint2;
    }
  }

  private static float[] ReadFloatArray(Ovl ovl, uint ptr, uint count) {
    if (ptr == 0)
      throw new InvalidDataException("Float array pointer is null");
    if (!ovl.TryResolveRelocation(ptr, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve float array pointer 0x{ptr:X}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    var result = new float[count];
    for (var i = 0; i < count; i++) {
      result[i] = reader.ReadSingle();
    }
    return result;
  }

  private static Segment[] ReadSegmentDataArray(Ovl ovl, uint ptr, uint count) {
    if (ptr == 0)
      throw new InvalidDataException("Segment data pointer is null");
    if (!ovl.TryResolveRelocation(ptr, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve segment data pointer 0x{ptr:X}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    var result = new Segment[count];
    for (var i = 0; i < count; i++) {
      if (reader.BaseStream.Position + 14 > reader.BaseStream.Length)
        throw new InvalidDataException($"Failed to read segment {i}");
      result[i] = new Segment { Samples = reader.ReadBytes(14) };
    }
    return result;
  }

  private static string ResolveSplineReference(Ovl ovl, uint fieldAddress, uint rawPtr) {
    if (ovl.TryResolveSymbolReference(fieldAddress, out var symFile))
      return symFile.Name;
    if (ovl.TryGetRelocationSource(fieldAddress, out var targetAddress) && ovl.TryFindSymbol(targetAddress, out var relSym))
      return relSym.Name;
    if (rawPtr != 0 && ovl.TryFindSymbol(rawPtr, out var rawSym))
      return rawSym.Name;
    return rawPtr != 0 ? $"<unresolved:0x{rawPtr:X}>" : string.Empty;
  }
}

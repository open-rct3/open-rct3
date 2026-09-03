// TrackData
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
//
// Decodes "spl" (Spline) and "tks" (TrackSection) entries per rct3-importer's
// spline.h/tracksection.h and corresponding Manager implementations.

using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.OVL.Files;

/// <summary>
/// Binary representation of a SplineNode (pos + 2 control points).
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 36)]
internal struct SplineNode {
  public Vector3 Pos;
  public Vector3 ControlPoint1;
  public Vector3 ControlPoint2;
}

/// <summary>
/// Decoded segment data: 14 normalized distance markers at 1/15th intervals along a cubic bezier curve.
/// Each byte (0-255) represents cumulative distance along the segment.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 14)]
public readonly struct Segment {
  /// <remarks>
  /// Each byte represents a distance marker at 1/15th intervals along the segment's cubic bezier curve.
  /// Encoding: 255 - floor((2*k + 16*k) - (255 * cumulative_distance[k] / segment_length))
  /// Used for efficient curve traversal without re-computing bezier distances at runtime.
  /// </remarks>
  private readonly byte S0, S1, S2, S3, S4, S5, S6, S7, S8, S9, S10, S11, S12, S13;

  public static int Length => 14;

  public readonly byte this[int index] => index switch {
    0 => S0,
    1 => S1,
    2 => S2,
    3 => S3,
    4 => S4,
    5 => S5,
    6 => S6,
    7 => S7,
    8 => S8,
    9 => S9,
    10 => S10,
    11 => S11,
    12 => S12,
    13 => S13,
    _ => throw new IndexOutOfRangeException($"Sample index {index} out of range [0-13]")
  };

  public readonly IEnumerable<byte> Samples {
    get {
      yield return S0; yield return S1; yield return S2; yield return S3;
      yield return S4; yield return S5; yield return S6; yield return S7;
      yield return S8; yield return S9; yield return S10; yield return S11;
      yield return S12; yield return S13;
    }
  }

  /// <summary>
  /// Decode sample to cumulative distance along segment.
  /// Formula: cumDist[k] = segmentLength * (34*k - byte[k-1]) / 255
  /// where k = sampleIndex + 1 (1-indexed in formula, 0-indexed for array access).
  /// </summary>
  public readonly float GetCumulativeDistance(int sampleIndex, float segmentLength) {
    if (sampleIndex < 0 || sampleIndex >= 14)
      throw new IndexOutOfRangeException($"Sample index {sampleIndex} out of range [0-13]");
    var k = sampleIndex + 1;
    var byte_value = this[sampleIndex];
    return segmentLength * (34 * k - byte_value) / 255f;
  }

  /// <summary>
  /// Decode all samples to cumulative distances along segment.
  /// </summary>
  public readonly float[] GetCumulativeDistances(float segmentLength) {
    var distances = new float[14];
    for (var i = 0; i < 14; i++) {
      distances[i] = GetCumulativeDistance(i, segmentLength);
    }
    return distances;
  }
}

/// <summary>
/// Binary representation of Spline header.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 32)]
internal struct Spline {
  public uint NodeCount;
  public uint NodesPtr;
  public uint Cyclic;
  public float TotalLength;
  public float InvTotalLength;
  public uint LengthsPtr;
  public uint DataPtr;
  public float MaxY;
}

/// <summary>
/// Represents a Spline entry from an OVL archive.
/// Domain-agnostic DTO: contains raw binary-decoded data without interpretation.
/// See rct3-importer's spline.h for binary structure definition.
/// </summary>
public readonly record struct OvlSpline(
  /// <summary>Spline resource identifier (OVL symbol name).</summary>
  string Id,
  /// <summary>Number of nodes in this spline polyline.</summary>
  uint NodeCount,
  /// <summary>Node positions in local space (length = NodeCount).</summary>
  Vector3[] Nodes,
  /// <summary>Control point 1 per node, relative to node position (towards previous node).</summary>
  Vector3[] ControlPoint1,
  /// <summary>Control point 2 per node, relative to node position (towards next node).</summary>
  Vector3[] ControlPoint2,
  /// <summary>True for closed splines (cyclic), false for open.</summary>
  bool Cyclic,
  /// <summary>Sum of all segment lengths.</summary>
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
/// Binary representation of TrackSection_V (vanilla format header, 140 bytes).
/// Mirrors rct3-importer's TrackSection_V structure.
/// Does not include Soaked/Wild extensions.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TrackSection {
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
/// Represents a TrackSection entry from an OVL archive (base structure).
/// Domain-agnostic DTO: contains raw binary-decoded data without interpretation.
/// Tracks vanilla (V) format only; Soaked/Wild extensions not yet decoded.
/// See rct3-importer's tracksection.h for binary structure definition.
/// </summary>
public readonly record struct OvlTrackSection(
  /// <summary>TrackSection resource identifier (OVL symbol name).</summary>
  string Id,
  /// <summary>Human-readable internal name from the resource.</summary>
  string InternalName,
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
  public static IReadOnlyList<OvlSpline> ExtractSplines(Ovl ovl) {
    var splFiles = ovl.Keys.Where(file => file.Type == FileType.Spline).ToList();
    var splines = new List<OvlSpline>(splFiles.Count);

    foreach (var file in splFiles) {
      splines.Add(ReadSpline(ovl, file));
    }

    return splines;
  }

  /// <summary>Extracts all TrackSection entries from an OVL archive with referential validation.</summary>
  public static IReadOnlyList<OvlTrackSection> ExtractTrackSections(Ovl ovl) {
    var splFiles = ExtractSplines(ovl);
    var splById = splFiles.ToDictionary(spl => spl.Id);

    var tksFiles = ovl.Keys.Where(file => file.Type == FileType.TrackSection).ToList();
    var sections = new List<OvlTrackSection>(tksFiles.Count);

    foreach (var file in tksFiles) {
      var section = ReadTrackSection(ovl, file);
      var isValid = section.SplineRefs.Where(r => !string.IsNullOrEmpty(r)).All(splById.ContainsKey);
      sections.Add(section with { IsValid = isValid });
    }

    return sections;
  }

  private static OvlSpline ReadSpline(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve Spline block for {file.Name}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    if (reader.Read<Spline>(out var splineData) == 0)
      throw new InvalidDataException($"Failed to read Spline header for {file.Name}");

    var nodeCount = splineData.NodeCount;
    var cyclic = splineData.Cyclic != 0;

    var nodes = new Vector3[nodeCount];
    var cp1 = new Vector3[nodeCount];
    var cp2 = new Vector3[nodeCount];
    ReadSplineNodes(ovl, splineData.NodesPtr, nodeCount, nodes, cp1, cp2);

    var segmentLengths = ReadFloatArray(ovl, splineData.LengthsPtr, cyclic ? nodeCount : nodeCount - 1);
    var segments = ReadSegmentDataArray(ovl, splineData.DataPtr, cyclic ? nodeCount : nodeCount - 1);

    return new OvlSpline(
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

  private static OvlTrackSection ReadTrackSection(Ovl ovl, OvlFile file) {
    if (!ovl.TryGetDataPointer(file, out var address))
      throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
    if (!ovl.TryResolveRelocation(address, out var block, out var offset))
      throw new InvalidOperationException($"Failed to resolve TrackSection block for {file.Name}");

    using var reader = new BinaryReader(new MemoryStream(block, (int)offset, block.Length - (int)offset));
    if (reader.Read<TrackSection>(out var tksData) == 0)
      throw new InvalidDataException($"Failed to read TrackSection header for {file.Name}");

    var internalName = ovl.TryResolveString(tksData.InternalNamePtr, out var name) ? name : $"<unresolved:{tksData.InternalNamePtr:X}>";

    var splRefs = new string[6] {
      ResolveSplineReference(ovl, tksData.SplineLeftRef),
      ResolveSplineReference(ovl, tksData.SplineRightRef),
      ResolveSplineReference(ovl, tksData.JoinSplineLeftRef),
      ResolveSplineReference(ovl, tksData.JoinSplineRightRef),
      ResolveSplineReference(ovl, tksData.ExtraSplineLeftRef),
      ResolveSplineReference(ovl, tksData.ExtraSplineRightRef)
    };

    return new OvlTrackSection(
      file.Name,
      internalName,
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
      if (reader.Read<Segment>(out var segment) == 0)
        throw new InvalidDataException($"Failed to read segment {i}");
      result[i] = segment;
    }
    return result;
  }

  private static string ResolveSplineReference(Ovl ovl, uint ptr) {
    if (ptr == 0) return string.Empty;
    if (!ovl.TryFindSymbol(ptr, out var splFile))
      throw new InvalidOperationException($"Failed to resolve spline reference: pointer {ptr:X} not found");
    return splFile.Name;
  }
}

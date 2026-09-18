// Track Segment Library for Tracked Rides
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections;
using System.Numerics;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

namespace OpenRCT3.Rides;

/// <summary>
/// The palette of reusable track-segment shapes available to each tracked-ride type.
/// </summary>
/// <remarks>
/// <para>
/// A tracked-ride OVL (<c>Track*.ovl</c> / <c>TrackBased*.ovl</c>) is not a ride. Its
/// <c>tks</c>/<c>spl</c> resources are the segment shapes (straight, medium curve, half-loop,
/// station-middle, …) a ride type can be built from, each authored in local segment space with no
/// ordering or world placement. Constructed rides (RCT3 <c>.trk</c> designs and imported
/// RCT1/RCT2 <c>.TD4</c>/<c>.TD6</c> designs) build a <see cref="TrackSpline.TrackGraph"/> by
/// naming segments from the library entry for their ride type.
/// </para>
/// </remarks>
public sealed class TrackLibrary : IReadOnlyDictionary<TrackedRide, TrackSegments> {
  private readonly Dictionary<TrackedRide, TrackSegments> _byRide;

  /// <summary>Creates a library from an existing ride-to-segments mapping, copied on construction.</summary>
  public TrackLibrary(IEnumerable<KeyValuePair<TrackedRide, TrackSegments>> segmentsByRide) {
    ArgumentNullException.ThrowIfNull(segmentsByRide);
    _byRide = new Dictionary<TrackedRide, TrackSegments>(
      segmentsByRide as IEnumerable<KeyValuePair<TrackedRide, TrackSegments>> ?? []);
  }

  /// <summary>
  /// Decodes every <c>tks</c> resource in <paramref name="ovl"/> — with the <c>spl</c> rails it
  /// references — into the segment set for <paramref name="ride"/>.
  /// </summary>
  /// <param name="ride">The tracked-ride type these segments belong to; the archive does not name it.</param>
  /// <param name="ovl">A loaded tracked-ride OVL archive.</param>
  /// <exception cref="System.IO.InvalidDataException">
  /// A <c>spl</c> or <c>tks</c> resource is malformed (thrown by <see cref="TrackData"/>).
  /// </exception>
  public static TrackSegments Read(TrackedRide ride, Ovl ovl) {
    ArgumentNullException.ThrowIfNull(ride);
    ArgumentNullException.ThrowIfNull(ovl);

    var splines = TrackData.ExtractSplines(ovl).ToDictionary(spline => spline.Id);
    var segments = TrackData.ExtractTrackSections(ovl).Select(section => {
      var rails = section.SplineRefs
        .Where(name => !string.IsNullOrEmpty(name)
          && !name.StartsWith("<unresolved", StringComparison.Ordinal)
          && splines.ContainsKey(name))
        .Select(name => splines[name])
        .ToArray();
      return new TrackSegment(section, rails);
    });

    return new TrackSegments(ride, segments);
  }

  /// <inheritdoc />
  public TrackSegments this[TrackedRide key] => _byRide[key];
  /// <inheritdoc />
  public IEnumerable<TrackedRide> Keys => _byRide.Keys;
  /// <inheritdoc />
  public IEnumerable<TrackSegments> Values => _byRide.Values;
  /// <inheritdoc />
  public int Count => _byRide.Count;
  /// <inheritdoc />
  public bool ContainsKey(TrackedRide key) => _byRide.ContainsKey(key);
  /// <inheritdoc />
  public bool TryGetValue(TrackedRide key, out TrackSegments value) => _byRide.TryGetValue(key, out value!);
  /// <inheritdoc />
  public IEnumerator<KeyValuePair<TrackedRide, TrackSegments>> GetEnumerator() => _byRide.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// One tracked-ride type's fixed set of track segments: an immutable collection indexable both by
/// position and by OVL <c>tks</c> symbol name.
/// </summary>
/// <remarks>
/// Names are the reference key used by imported designs, so name lookup is first-class here
/// alongside ordered access.
/// </remarks>
public sealed class TrackSegments : IReadOnlyList<TrackSegment>, IReadOnlyDictionary<string, TrackSegment> {
  private readonly TrackSegment[] segments;
  private readonly Dictionary<string, TrackSegment> byName;

  /// <summary>The tracked-ride type this segment set belongs to.</summary>
  public TrackedRide Ride { get; }

  /// <param name="ride">The owning tracked-ride type.</param>
  /// <param name="segments">The segments; order is preserved, names must be unique.</param>
  public TrackSegments(TrackedRide ride, IEnumerable<TrackSegment> segments) {
    ArgumentNullException.ThrowIfNull(ride);
    ArgumentNullException.ThrowIfNull(segments);
    Ride = ride;
    this.segments = [.. segments];
    byName = this.segments.ToDictionary(segment => segment.Name);
  }

  /// <inheritdoc cref="IReadOnlyList{T}.this" />
  public TrackSegment this[int index] => segments[index];
  /// <summary>Gets the segment with the given OVL <c>tks</c> symbol name.</summary>
  public TrackSegment this[string name] => byName[name];
  /// <inheritdoc />
  public int Count => segments.Length;
  /// <inheritdoc />
  public IEnumerable<string> Keys => byName.Keys;
  IEnumerable<TrackSegment> IReadOnlyDictionary<string, TrackSegment>.Values => byName.Values;
  /// <inheritdoc />
  public bool ContainsKey(string name) => byName.ContainsKey(name);
  /// <inheritdoc />
  public bool TryGetValue(string name, out TrackSegment value) => byName.TryGetValue(name, out value!);
  /// <inheritdoc />
  public IEnumerator<TrackSegment> GetEnumerator() => ((IEnumerable<TrackSegment>)segments).GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  IEnumerator<KeyValuePair<string, TrackSegment>> IEnumerable<KeyValuePair<string, TrackSegment>>.GetEnumerator() =>
    byName.GetEnumerator();
}

/// <summary>
/// One reusable track-segment shape: decoded <see cref="TrackSection"/> metadata plus the rail
/// <see cref="Spline"/>s it resolves to, all in local segment space.
/// </summary>
public sealed class TrackSegment {
  private readonly Lazy<SegmentConnectors> _connectors;

  /// <param name="section">Decoded track-section metadata (slopes, banks, directions, flags, spline refs).</param>
  /// <param name="rails">
  /// Resolved rail splines in <see cref="TrackSection.SplineRefs"/> order — left, right, then any
  /// join/extra rails — with unresolved references dropped.
  /// </param>
  public TrackSegment(TrackSection section, IReadOnlyList<Spline> rails) {
    Section = section;
    Rails = rails;
    _connectors = new Lazy<SegmentConnectors>(() => SegmentConnectors.Derive(Rails));
  }

  /// <summary>OVL <c>tks</c> symbol name; the key imported designs reference this segment by.</summary>
  public string Name => Section.Id;

  /// <summary>Decoded track-section metadata.</summary>
  public TrackSection Section { get; }

  /// <summary>Resolved rail splines, in <see cref="TrackSection.SplineRefs"/> order.</summary>
  public IReadOnlyList<Spline> Rails { get; }

  /// <summary>
  /// Entry/exit sockets in local segment space, derived from the rail endpoints on first access.
  /// Chaining and placement match an exit connector to the next segment's entry connector.
  /// </summary>
  public SegmentConnectors Connectors => _connectors.Value;
}

/// <summary>A track-segment attachment point in local segment space.</summary>
/// <remarks>
/// Geometry derivation is intentionally simple (rail endpoints and endpoint control vectors); it
/// has not been tuned against real chaining and may be refined.
/// </remarks>
public readonly record struct TrackConnector(
  /// <summary>Midpoint between the left and right rail at this end of the segment.</summary>
  Vector3 Position,
  /// <summary>Unit travel direction pointing out of the segment at this end.</summary>
  Vector3 Tangent,
  /// <summary>Roll about <see cref="Tangent"/>, in radians, from the left/right rail height delta.</summary>
  float Bank,
  /// <summary>Distance between the left and right rail at this end, in local units.</summary>
  float Gauge
);

/// <summary>The entry and exit <see cref="TrackConnector"/>s of a <see cref="TrackSegment"/>.</summary>
public readonly record struct SegmentConnectors(TrackConnector Entry, TrackConnector Exit) {
  /// <summary>
  /// Sentinel for a segment with no usable rail geometry (every <see cref="TrackSection.SplineRefs"/>
  /// entry unresolved, or the first rail has no nodes).
  /// </summary>
  /// <remarks>
  /// <see cref="TrackConnector.Position"/>,
  /// <see cref="TrackConnector.Bank"/>, and <see cref="TrackConnector.Gauge"/> are zero;
  /// <see cref="TrackConnector.Tangent"/> is <see cref="Vector3.UnitZ"/> so the unit-direction
  /// contract still holds. Distinct from a real segment that happens to sit at the origin.
  /// </remarks>
  public static SegmentConnectors None { get; } = new(
    new TrackConnector(Vector3.Zero, Vector3.UnitZ, 0f, 0f),
    new TrackConnector(Vector3.Zero, Vector3.UnitZ, 0f, 0f));

  /// <summary>
  /// Derives connectors from the first (and, if present, second) rail spline. With one rail the
  /// gauge is zero and both connectors sit on that rail; with no usable rail geometry the result
  /// is <see cref="None"/>.
  /// </summary>
  public static SegmentConnectors Derive(IReadOnlyList<Spline> rails) {
    if (rails.Count == 0) return None;
    var left = rails[0];
    if (left.NodeCount == 0) return None;
    var right = rails.Count > 1 && rails[1].NodeCount == left.NodeCount ? rails[1] : left;
    var last = (int)left.NodeCount - 1;

    return new SegmentConnectors(
      DeriveEnd(left, right, 0, outward: -1f),
      DeriveEnd(left, right, last, outward: 1f));
  }

  private static TrackConnector DeriveEnd(Spline left, Spline right, int i, float outward) {
    var position = (left.Nodes[i] + right.Nodes[i]) * 0.5f;
    // ControlPoint2 points toward the next node, ControlPoint1 toward the previous one.
    var along = outward > 0 ? left.ControlPoint2[i] : left.ControlPoint1[i];
    var tangent = along.LengthSquared() > 1e-12f ? Vector3.Normalize(along) * outward : Vector3.UnitZ * outward;
    var acrossVec = right.Nodes[i] - left.Nodes[i];
    var gauge = acrossVec.Length();
    var bank = gauge > 1e-6f ? MathF.Atan2(acrossVec.Y, MathF.Sqrt(acrossVec.X * acrossVec.X + acrossVec.Z * acrossVec.Z)) : 0f;
    return new TrackConnector(position, tangent, bank, gauge);
  }
}

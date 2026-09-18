// Track Piece
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace OpenRCT3.Simulation.Tracks;

/// <summary>
/// One independently addressable dual-rail track piece with analytic Hermite curves and a shared,
/// midpoint-derived local arc-length bake.
/// </summary>
public sealed class TrackPiece {
  private readonly HermiteRailSpline leftSpline;
  private readonly HermiteRailSpline rightSpline;
  private readonly float[] arcLengths;
  private readonly BakedRailPoint[] leftBake;
  private readonly BakedRailPoint[] rightBake;
  private readonly ReadOnlyCollection<float> bakedArcLengths;

  public TrackPieceGeometry Geometry { get; }
  public Matrix4x4 Placement { get; }
  public TrackBakeSettings BakeSettings { get; }
  public float Length => arcLengths[^1];
  public int BakedSampleCount => arcLengths.Length;
  public IReadOnlyList<float> BakedArcLengths => bakedArcLengths;
  public TrackPieceEndpoint Entry { get; }
  public TrackPieceEndpoint Exit { get; }

  public TrackPiece(TrackPieceGeometry geometry)
    : this(geometry, Matrix4x4.Identity, TrackBakeSettings.Default) { }

  public TrackPiece(TrackPieceGeometry geometry, Matrix4x4 placement)
    : this(geometry, placement, TrackBakeSettings.Default) { }

  public TrackPiece(
    TrackPieceGeometry geometry,
    Matrix4x4 placement,
    TrackBakeSettings bakeSettings
  ) {
    ArgumentNullException.ThrowIfNull(geometry);
    ArgumentNullException.ThrowIfNull(bakeSettings);
    ValidatePlacement(placement);
    bakeSettings.Validate();

    Geometry = geometry;
    Placement = placement;
    BakeSettings = bakeSettings;

    var transformed = geometry.ControlPoints.Select(point => Transform(point, placement)).ToArray();
    leftSpline = new(transformed.Select(point => new RailSplinePoint(
      point.Parameter,
      point.LeftPosition,
      point.LeftTangent,
      point.BankRadians
    )));
    rightSpline = new(transformed.Select(point => new RailSplinePoint(
      point.Parameter,
      point.RightPosition,
      point.RightTangent,
      point.BankRadians
    )));
    var centerSpline = new HermiteRailSpline(transformed.Select(point => new RailSplinePoint(
      point.Parameter,
      TrackMath.Midpoint(point.LeftPosition, point.RightPosition),
      TrackMath.Midpoint(point.LeftTangent, point.RightTangent),
      point.BankRadians
    )));
    leftSpline.ValidateRegularity();
    rightSpline.ValidateRegularity();
    centerSpline.ValidateRegularity();

    Entry = GetEndpoint(0f);
    Exit = GetEndpoint(1f);

    var bake = Bake(transformed);
    arcLengths = bake.ArcLengths;
    leftBake = bake.Left;
    rightBake = bake.Right;
    bakedArcLengths = Array.AsReadOnly(arcLengths);
  }

  /// <summary>Evaluates the retained analytic spline without using the runtime bake.</summary>
  public RailEvaluation EvaluateRail(RailSide side, float parameter) {
    if (!float.IsFinite(parameter) || parameter is < 0f or > 1f)
      throw new ArgumentOutOfRangeException(nameof(parameter));

    var evaluation = GetSpline(side).Evaluate(parameter);
    return new(
      parameter,
      evaluation.Position,
      TrackMath.Normalize(evaluation.Derivative),
      evaluation.BankRadians
    );
  }

  /// <summary>Samples one rail from the adaptive bake at a piece-local arc length.</summary>
  public RailSample SampleRail(RailSide side, float arcLength) {
    if (!float.IsFinite(arcLength) || arcLength < 0f || arcLength > Length)
      throw new ArgumentOutOfRangeException(nameof(arcLength));

    var bake = side switch {
      RailSide.Left => leftBake,
      RailSide.Right => rightBake,
      _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    var index = Array.BinarySearch(arcLengths, arcLength);
    if (index >= 0) return bake[index].ToSample(arcLength);

    var upper = ~index;
    var lower = upper - 1;
    var intervalLength = arcLengths[upper] - arcLengths[lower];
    var amount = (arcLength - arcLengths[lower]) / intervalLength;
    return Interpolate(bake[lower], bake[upper], arcLength, intervalLength, amount);
  }

  /// <summary>Samples both wheel-contact rails at one shared piece-local arc length.</summary>
  public TrackContactPoints SampleContactPoints(float arcLength)
    => new(arcLength, SampleRail(RailSide.Left, arcLength), SampleRail(RailSide.Right, arcLength));

  private BakeResult Bake(IReadOnlyList<RailControlPair> controlPoints) {
    var averageGauge = controlPoints.Average(point =>
      TrackMath.Distance(point.LeftPosition, point.RightPosition));
    var chordTolerance = Math.Max(
      BakeSettings.MinimumChordTolerance,
      averageGauge * BakeSettings.ChordToleranceGaugeFraction
    );

    var parameters = new List<float> { 0f };
    for (var index = 0; index < controlPoints.Count - 1; index++)
      Subdivide(
        controlPoints[index].Parameter,
        controlPoints[index + 1].Parameter,
        depth: 0,
        chordTolerance,
        parameters
      );

    var arcs = new float[parameters.Count];
    var left = new BakedRailPoint[parameters.Count];
    var right = new BakedRailPoint[parameters.Count];
    var evaluations = parameters.Select(EvaluatePair).ToArray();

    for (var index = 1; index < evaluations.Length; index++) {
      var previousMidpoint = evaluations[index - 1].Midpoint;
      var midpoint = evaluations[index].Midpoint;
      var segmentLength = TrackMath.Distance(previousMidpoint, midpoint);
      var cumulativeLength = Convert.ToDouble(arcs[index - 1]) + segmentLength;
      if (!double.IsFinite(segmentLength)
          || segmentLength <= 0d
          || cumulativeLength > float.MaxValue)
        throw new ArgumentException(
          "A track bake must contain finite, strictly increasing arc-length intervals.",
          nameof(Geometry)
        );

      arcs[index] = Convert.ToSingle(cumulativeLength);
      if (arcs[index] <= arcs[index - 1])
        throw new ArgumentException(
          "A track bake must contain finite, strictly increasing arc-length intervals.",
          nameof(Geometry)
        );
    }
    if (arcs[^1] <= TrackMath.Epsilon)
      throw new ArgumentException("A track piece must have non-zero centerline arc length.", nameof(Geometry));

    for (var index = 0; index < evaluations.Length; index++) {
      var pair = evaluations[index];
      var centerDerivative = TrackMath.Midpoint(pair.Left.Derivative, pair.Right.Derivative);
      var arcDerivative = TrackMath.Length(centerDerivative);
      if (arcDerivative <= TrackMath.Epsilon)
        throw new ArgumentException("A track piece cannot contain a stationary centerline tangent.", nameof(Geometry));

      left[index] = BakedRailPoint.Create(
        pair.Left,
        pair.Left.Position,
        pair.Right.Position,
        arcDerivative
      );
      right[index] = BakedRailPoint.Create(
        pair.Right,
        pair.Left.Position,
        pair.Right.Position,
        arcDerivative
      );
    }

    return new(arcs, left, right);
  }

  private void Subdivide(
    float startParameter,
    float endParameter,
    int depth,
    double chordTolerance,
    ICollection<float> output
  ) {
    var midpointParameter = (startParameter + endParameter) * 0.5f;
    var firstQuarterParameter = (startParameter + midpointParameter) * 0.5f;
    var thirdQuarterParameter = (midpointParameter + endParameter) * 0.5f;
    var start = EvaluatePair(startParameter);
    var firstQuarter = EvaluatePair(firstQuarterParameter);
    var midpoint = EvaluatePair(midpointParameter);
    var thirdQuarter = EvaluatePair(thirdQuarterParameter);
    var end = EvaluatePair(endParameter);

    var leftDeviation = MaximumChordDeviation(
      start.Left.Position,
      firstQuarter.Left.Position,
      midpoint.Left.Position,
      thirdQuarter.Left.Position,
      end.Left.Position
    );
    var rightDeviation = MaximumChordDeviation(
      start.Right.Position,
      firstQuarter.Right.Position,
      midpoint.Right.Position,
      thirdQuarter.Right.Position,
      end.Right.Position
    );
    var bankChange = Math.Abs(Convert.ToDouble(end.Left.BankRadians) - start.Left.BankRadians);
    if (!double.IsFinite(bankChange))
      throw new ArgumentException("Bank-angle subtraction produced a non-finite result.");
    var needsSubdivision = Math.Max(leftDeviation, rightDeviation) > chordTolerance
      || bankChange > BakeSettings.MaximumBankAngleChangeRadians + TrackMath.Epsilon;

    if (needsSubdivision && depth < BakeSettings.MaximumSubdivisionDepth) {
      Subdivide(startParameter, midpointParameter, depth + 1, chordTolerance, output);
      Subdivide(midpointParameter, endParameter, depth + 1, chordTolerance, output);
      return;
    }
    if (needsSubdivision)
      throw new InvalidOperationException(
        "Adaptive track bake could not meet its tolerances within the subdivision limit."
      );

    output.Add(endParameter);
  }

  private static double MaximumChordDeviation(
    Vector3 start,
    Vector3 firstQuarter,
    Vector3 midpoint,
    Vector3 thirdQuarter,
    Vector3 end
  )
    => Math.Max(
      TrackMath.Distance(firstQuarter, TrackMath.Lerp(start, end, 0.25d)),
      Math.Max(
        TrackMath.Distance(midpoint, TrackMath.Lerp(start, end, 0.5d)),
        TrackMath.Distance(thirdQuarter, TrackMath.Lerp(start, end, 0.75d))
      )
    );

  private RailPairEvaluation EvaluatePair(float parameter)
    => new(leftSpline.Evaluate(parameter), rightSpline.Evaluate(parameter));

  private TrackPieceEndpoint GetEndpoint(float parameter) {
    var pair = EvaluatePair(parameter);
    return new(
      new(pair.Left.Position, pair.Left.Derivative, pair.Left.BankRadians),
      new(pair.Right.Position, pair.Right.Derivative, pair.Right.BankRadians)
    );
  }

  private HermiteRailSpline GetSpline(RailSide side) => side switch {
    RailSide.Left => leftSpline,
    RailSide.Right => rightSpline,
    _ => throw new ArgumentOutOfRangeException(nameof(side)),
  };

  private static RailSample Interpolate(
    BakedRailPoint start,
    BakedRailPoint end,
    float arcLength,
    float intervalLength,
    float amount
  ) {
    var amountSquared = amount * amount;
    var amountCubed = amountSquared * amount;
    var h00 = (2f * amountCubed) - (3f * amountSquared) + 1f;
    var h10 = amountCubed - (2f * amountSquared) + amount;
    var h01 = (-2f * amountCubed) + (3f * amountSquared);
    var h11 = amountCubed - amountSquared;
    var startTangent = start.DerivativePerArc * intervalLength;
    var endTangent = end.DerivativePerArc * intervalLength;
    var position = (h00 * start.Position) + (h10 * startTangent)
      + (h01 * end.Position) + (h11 * endTangent);

    var derivative = ((6f * amountSquared) - (6f * amount)) * start.Position
      + ((3f * amountSquared) - (4f * amount) + 1f) * startTangent
      + ((-6f * amountSquared) + (6f * amount)) * end.Position
      + ((3f * amountSquared) - (2f * amount)) * endTangent;
    var tangentSource = derivative;
    if (tangentSource.LengthSquared() <= TrackMath.EpsilonSquared)
      tangentSource = Vector3.Lerp(start.Tangent, end.Tangent, amount);
    if (tangentSource.LengthSquared() <= TrackMath.EpsilonSquared)
      tangentSource = amount < 0.5f ? start.Tangent : end.Tangent;
    var tangent = TrackMath.Normalize(tangentSource);
    var bank = TrackMath.LerpUnwrapped(start.BankRadians, end.BankRadians, amount);
    var lateral = ApplyBank(tangent, InterpolateLateral(start, end, tangent, amount), bank);

    return new(
      arcLength,
      position,
      tangent,
      CreateOrientation(tangent, lateral),
      bank
    );
  }

  private static Vector3 InterpolateLateral(
    BakedRailPoint start,
    BakedRailPoint end,
    Vector3 tangent,
    float amount
  ) {
    var lateral = Vector3.Lerp(start.Lateral, end.Lateral, amount);
    lateral -= Vector3.Dot(lateral, tangent) * tangent;
    if (lateral.LengthSquared() > TrackMath.EpsilonSquared)
      return TrackMath.Normalize(lateral);

    var source = amount < 0.5f ? start : end;
    var axis = Vector3.Cross(source.Tangent, tangent);
    if (axis.LengthSquared() <= TrackMath.EpsilonSquared) {
      lateral = source.Lateral - (Vector3.Dot(source.Lateral, tangent) * tangent);
      return TrackMath.Normalize(lateral);
    }

    axis = TrackMath.Normalize(axis);
    var angle = MathF.Acos(Math.Clamp(Vector3.Dot(source.Tangent, tangent), -1f, 1f));
    lateral = Vector3.Transform(
      source.Lateral,
      Quaternion.CreateFromAxisAngle(axis, angle)
    );
    lateral -= Vector3.Dot(lateral, tangent) * tangent;
    return TrackMath.Normalize(lateral);
  }

  private static Quaternion CreateOrientation(Vector3 tangent, Vector3 lateral) {
    var up = TrackMath.Normalize(Vector3.Cross(tangent, lateral));
    var orientationMatrix = new Matrix4x4(
      tangent.X, tangent.Y, tangent.Z, 0f,
      lateral.X, lateral.Y, lateral.Z, 0f,
      up.X, up.Y, up.Z, 0f,
      0f, 0f, 0f, 1f
    );
    var orientation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(orientationMatrix));
    if (!TrackMath.IsFinite(orientation))
      throw new ArgumentException("Rail-frame construction produced a non-finite orientation.");
    return orientation;
  }

  private static Vector3 ApplyBank(Vector3 tangent, Vector3 lateral, float bankRadians)
    => TrackMath.Normalize(Vector3.Transform(
      lateral,
      Quaternion.CreateFromAxisAngle(tangent, TrackMath.NormalizeAngleForRotation(bankRadians))
    ));

  private static RailControlPair Transform(RailControlPair point, Matrix4x4 placement) {
    var transformed = point with {
      LeftPosition = Vector3.Transform(point.LeftPosition, placement),
      LeftTangent = Vector3.TransformNormal(point.LeftTangent, placement),
      RightPosition = Vector3.Transform(point.RightPosition, placement),
      RightTangent = Vector3.TransformNormal(point.RightTangent, placement),
    };
    if (!TrackMath.IsFinite(transformed.LeftPosition)
        || !TrackMath.IsFinite(transformed.LeftTangent)
        || !TrackMath.IsFinite(transformed.RightPosition)
        || !TrackMath.IsFinite(transformed.RightTangent))
      throw new ArgumentException("Track placement produced non-finite rail control data.");
    return transformed;
  }

  private static void ValidatePlacement(Matrix4x4 placement) {
    var determinant = placement.GetDeterminant();
    if (!TrackMath.IsFinite(placement)
        || placement.M14 != 0f
        || placement.M24 != 0f
        || placement.M34 != 0f
        || placement.M44 != 1f
        || !float.IsFinite(determinant)
        || MathF.Abs(determinant) <= TrackMath.Epsilon)
      throw new ArgumentException("Track placement must be a finite, invertible affine transform.", nameof(placement));
  }

  private readonly record struct BakeResult(
    float[] ArcLengths,
    BakedRailPoint[] Left,
    BakedRailPoint[] Right
  );

  private readonly record struct RailPairEvaluation(
    RailCurveEvaluation Left,
    RailCurveEvaluation Right
  ) {
    public Vector3 Midpoint => TrackMath.Midpoint(Left.Position, Right.Position);
  }

  private readonly record struct BakedRailPoint(
    Vector3 Position,
    Vector3 Tangent,
    Vector3 DerivativePerArc,
    Vector3 Lateral,
    float BankRadians
  ) {
    public static BakedRailPoint Create(
      RailCurveEvaluation rail,
      Vector3 leftPosition,
      Vector3 rightPosition,
      double centerArcDerivative
    ) {
      var tangent = TrackMath.Normalize(rail.Derivative);
      var gauge = TrackMath.Direction(leftPosition, rightPosition);
      var right = gauge - (Vector3.Dot(gauge, tangent) * tangent);
      if (!TrackMath.IsFinite(right) || right.LengthSquared() <= TrackMath.EpsilonSquared)
        throw new ArgumentException("The rail gauge cannot be parallel to its tangent.");
      right = TrackMath.Normalize(right);
      var derivativePerArc = new Vector3(
        Convert.ToSingle(Convert.ToDouble(rail.Derivative.X) / centerArcDerivative),
        Convert.ToSingle(Convert.ToDouble(rail.Derivative.Y) / centerArcDerivative),
        Convert.ToSingle(Convert.ToDouble(rail.Derivative.Z) / centerArcDerivative)
      );
      if (!TrackMath.IsFinite(derivativePerArc))
        throw new ArgumentException("Rail-frame construction produced a non-finite derivative.");

      return new(
        rail.Position,
        tangent,
        derivativePerArc,
        right,
        rail.BankRadians
      );
    }

    public RailSample ToSample(float arcLength)
      => new(
        arcLength,
        Position,
        Tangent,
        CreateOrientation(Tangent, ApplyBank(Tangent, Lateral, BankRadians)),
        BankRadians
      );
  }
}

internal readonly record struct RailSplinePoint(
  float Parameter,
  Vector3 Position,
  Vector3 Tangent,
  float BankRadians
);

internal readonly record struct RailCurveEvaluation(
  Vector3 Position,
  Vector3 Derivative,
  float BankRadians
);

internal sealed class HermiteRailSpline {
  private const int MaximumRegularitySubdivisionDepth = 20;
  private readonly RailSplinePoint[] points;
  private readonly float[] parameters;

  public HermiteRailSpline(IEnumerable<RailSplinePoint> points) {
    this.points = points.ToArray();
    parameters = this.points.Select(point => point.Parameter).ToArray();
  }

  public void ValidateRegularity() {
    for (var index = 0; index < points.Length - 1; index++) {
      var start = points[index];
      var end = points[index + 1];
      var parameterRange = Convert.ToDouble(end.Parameter) - start.Parameter;
      var derivativeStart = DoubleVector3.From(start.Tangent);
      var derivativeMiddle = (3d * (
        DoubleVector3.From(end.Position) - DoubleVector3.From(start.Position)
      ) / parameterRange) - derivativeStart - DoubleVector3.From(end.Tangent);
      var derivativeEnd = DoubleVector3.From(end.Tangent);
      var derivativeScale = Math.Max(
        derivativeStart.Length(),
        Math.Max(derivativeMiddle.Length(), derivativeEnd.Length())
      );
      if (!double.IsFinite(derivativeScale))
        throw new ArgumentException("Spline derivative construction produced a non-finite result.");
      var minimumDerivative = Math.Max(TrackMath.Epsilon, derivativeScale * 0.00001d);
      ValidateDerivativeBounds(
        derivativeStart,
        derivativeMiddle,
        derivativeEnd,
        minimumDerivative,
        depth: 0
      );
    }
  }

  private static void ValidateDerivativeBounds(
    DoubleVector3 start,
    DoubleVector3 middle,
    DoubleVector3 end,
    double minimumDerivative,
    int depth
  ) {
    if (DistanceFromOriginToTriangle(start, middle, end) > minimumDerivative) return;
    if (depth >= MaximumRegularitySubdivisionDepth)
      throw new ArgumentException(
        "A track piece cannot contain a stationary or near-stationary spline tangent."
      );

    var startMiddle = (start + middle) * 0.5f;
    var middleEnd = (middle + end) * 0.5f;
    var split = (startMiddle + middleEnd) * 0.5f;
    ValidateDerivativeBounds(start, startMiddle, split, minimumDerivative, depth + 1);
    ValidateDerivativeBounds(split, middleEnd, end, minimumDerivative, depth + 1);
  }

  private static double DistanceFromOriginToTriangle(
    DoubleVector3 first,
    DoubleVector3 second,
    DoubleVector3 third
  ) {
    var firstEdge = second - first;
    var secondEdge = third - first;
    var normal = DoubleVector3.Cross(firstEdge, secondEdge);
    var normalLengthSquared = DoubleVector3.Dot(normal, normal);

    if (normalLengthSquared > 0d && double.IsFinite(normalLengthSquared)) {
      var projection = (DoubleVector3.Dot(normal, first) / normalLengthSquared) * normal;
      var projectionOffset = projection - first;
      var firstDot = DoubleVector3.Dot(firstEdge, firstEdge);
      var crossDot = DoubleVector3.Dot(firstEdge, secondEdge);
      var secondDot = DoubleVector3.Dot(secondEdge, secondEdge);
      var projectionFirstDot = DoubleVector3.Dot(projectionOffset, firstEdge);
      var projectionSecondDot = DoubleVector3.Dot(projectionOffset, secondEdge);
      var denominator = (firstDot * secondDot) - (crossDot * crossDot);

      if (denominator > 0d && double.IsFinite(denominator)) {
        var secondWeight = ((secondDot * projectionFirstDot)
          - (crossDot * projectionSecondDot)) / denominator;
        var thirdWeight = ((firstDot * projectionSecondDot)
          - (crossDot * projectionFirstDot)) / denominator;
        var firstWeight = 1d - secondWeight - thirdWeight;
        if (firstWeight >= 0d && secondWeight >= 0d && thirdWeight >= 0d)
          return projection.Length();
      }
    }

    return Math.Min(
      DistanceFromOriginToSegment(first, second),
      Math.Min(
        DistanceFromOriginToSegment(second, third),
        DistanceFromOriginToSegment(third, first)
      )
    );
  }

  private static double DistanceFromOriginToSegment(
    DoubleVector3 start,
    DoubleVector3 end
  ) {
    var segment = end - start;
    var lengthSquared = DoubleVector3.Dot(segment, segment);
    if (lengthSquared <= 0d) return start.Length();

    var amount = Math.Clamp(-DoubleVector3.Dot(start, segment) / lengthSquared, 0d, 1d);
    return (start + (amount * segment)).Length();
  }

  public RailCurveEvaluation Evaluate(float parameter) {
    var exactIndex = Array.BinarySearch(parameters, parameter);
    var lowerIndex = exactIndex >= 0 ? exactIndex : (~exactIndex) - 1;
    if (lowerIndex >= points.Length - 1) lowerIndex = points.Length - 2;
    if (lowerIndex < 0) lowerIndex = 0;

    var start = points[lowerIndex];
    var end = points[lowerIndex + 1];
    var parameterRange = end.Parameter - start.Parameter;
    var amount = (parameter - start.Parameter) / parameterRange;
    var amountSquared = amount * amount;
    var amountCubed = amountSquared * amount;
    var h00 = (2f * amountCubed) - (3f * amountSquared) + 1f;
    var h10 = amountCubed - (2f * amountSquared) + amount;
    var h01 = (-2f * amountCubed) + (3f * amountSquared);
    var h11 = amountCubed - amountSquared;
    var startTangent = start.Tangent * parameterRange;
    var endTangent = end.Tangent * parameterRange;
    var position = (h00 * start.Position) + (h10 * startTangent)
      + (h01 * end.Position) + (h11 * endTangent);

    var derivative = (((6f * amountSquared) - (6f * amount)) * start.Position
      + ((3f * amountSquared) - (4f * amount) + 1f) * startTangent
      + ((-6f * amountSquared) + (6f * amount)) * end.Position
      + ((3f * amountSquared) - (2f * amount)) * endTangent) / parameterRange;

    var bank = TrackMath.LerpUnwrapped(start.BankRadians, end.BankRadians, amount);
    if (!TrackMath.IsFinite(position) || !TrackMath.IsFinite(derivative) || !float.IsFinite(bank))
      throw new ArgumentException("Spline evaluation produced non-finite derived data.");

    return new(
      position,
      derivative,
      bank
    );
  }

  private readonly record struct DoubleVector3(double X, double Y, double Z) {
    public static DoubleVector3 From(Vector3 value)
      => new(value.X, value.Y, value.Z);

    public double Length()
      => Math.Sqrt(Dot(this, this));

    public static double Dot(DoubleVector3 left, DoubleVector3 right)
      => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    public static DoubleVector3 Cross(DoubleVector3 left, DoubleVector3 right)
      => new(
        (left.Y * right.Z) - (left.Z * right.Y),
        (left.Z * right.X) - (left.X * right.Z),
        (left.X * right.Y) - (left.Y * right.X)
      );

    public static DoubleVector3 operator +(DoubleVector3 left, DoubleVector3 right)
      => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static DoubleVector3 operator -(DoubleVector3 left, DoubleVector3 right)
      => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static DoubleVector3 operator *(double scalar, DoubleVector3 value)
      => new(scalar * value.X, scalar * value.Y, scalar * value.Z);

    public static DoubleVector3 operator *(DoubleVector3 value, double scalar)
      => scalar * value;

    public static DoubleVector3 operator /(DoubleVector3 value, double scalar)
      => new(value.X / scalar, value.Y / scalar, value.Z / scalar);
  }
}

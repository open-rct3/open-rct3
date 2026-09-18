// TrackPieceTests
//
// Authors:
//   - OpenRCT3 Contributors
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenRCT3.Simulation.Tracks;

namespace OpenRCT3.Tests.Simulation.Tracks;

[TestFixture]
public class TrackPieceTests {
  [Test]
  public void HandAuthoredPiece_SamplesArcLengthBoundariesAndContactPoints() {
    var piece = StraightPiece(length: 10f);

    var entry = piece.SampleRail(RailSide.Left, 0f);
    var exit = piece.SampleRail(RailSide.Right, piece.Length);
    var contacts = piece.SampleContactPoints(piece.Length * 0.5f);

    Assert.That(piece.Length, Is.EqualTo(10f).Within(0.0001f));
    AssertVector(entry.Position, new(0f, -0.5f, 0f));
    AssertVector(exit.Position, new(10f, 0.5f, 0f));
    AssertVector(contacts.Midpoint, new(5f, 0f, 0f));
    Assert.That(contacts.Left.ArcLength, Is.EqualTo(contacts.Right.ArcLength));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      piece.SampleRail(RailSide.Left, -0.001f)));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      piece.SampleRail(RailSide.Right, piece.Length + 0.001f)));
  }

  [Test]
  public void ProceduralGeometry_AuthorsTheWholePieceAndKeepsAnalyticEvaluation() {
    var geometry = TrackPieceGeometry.FromProcedural(
      controlPointCount: 3,
      parameter => new(
        new(parameter * 8f, -0.5f, 0f),
        new(8f, 0f, 0f),
        new(parameter * 8f, 0.5f, 0f),
        new(8f, 0f, 0f),
        0f
      )
    );
    var piece = new TrackPiece(geometry, Matrix4x4.CreateTranslation(2f, 3f, 4f));

    var analytic = piece.EvaluateRail(RailSide.Right, 0.5f);

    Assert.That(geometry.AuthoringMode, Is.EqualTo(TrackPieceAuthoringMode.Procedural));
    Assert.That(geometry.ControlPoints, Has.Count.EqualTo(3));
    AssertVector(analytic.Position, new(6f, 3.5f, 4f));
    AssertVector(analytic.Tangent, Vector3.UnitX);
  }

  [Test]
  public void AdaptiveBake_IsDeterministicAndRespondsToCurveAndBankRate() {
    var strictChord = new TrackBakeSettings(
      ChordToleranceGaugeFraction: 0f,
      MinimumChordTolerance: 0.01f,
      MaximumBankAngleChangeRadians: TrackBakeSettings.MaximumSafeBankAngleChangeRadians,
      MaximumSubdivisionDepth: 12
    );
    var curvedGeometry = TrackPieceGeometry.FromHandAuthored([
      Pair(0f, new(0f, 0f, 0f), new(10f, 0f, 0f), Vector3.UnitZ),
      Pair(1f, new(10f, 10f, 0f), new(0f, 10f, 0f), Vector3.UnitZ),
    ]);
    var firstCurve = new TrackPiece(curvedGeometry, Matrix4x4.Identity, strictChord);
    var secondCurve = new TrackPiece(curvedGeometry, Matrix4x4.Identity, strictChord);
    var inflectedCurve = new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, new(10f, 20f, 0f), Vector3.UnitZ),
        Pair(1f, new(10f, 0f, 0f), new(10f, 20f, 0f), Vector3.UnitZ),
      ]),
      Matrix4x4.Identity,
      strictChord
    );

    var bankDriven = new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, new(10f, 0f, 0f), Vector3.UnitY, bank: 0f),
        Pair(1f, new(10f, 0f, 0f), new(10f, 0f, 0f), Vector3.UnitY, bank: MathF.PI / 2f),
      ]),
      Matrix4x4.Identity,
      new(
        ChordToleranceGaugeFraction: 0f,
        MinimumChordTolerance: 1f,
        MaximumBankAngleChangeRadians: 0.1f,
        MaximumSubdivisionDepth: 12
      )
    );

    Assert.That(firstCurve.BakedSampleCount, Is.GreaterThan(2));
    Assert.That(firstCurve.BakedArcLengths, Is.EqualTo(secondCurve.BakedArcLengths));
    Assert.That(inflectedCurve.BakedSampleCount, Is.GreaterThan(2));
    Assert.That(bankDriven.BakedSampleCount, Is.GreaterThan(2));
    Assert.That(
      bankDriven.SampleRail(RailSide.Left, bankDriven.Length).BankRadians,
      Is.EqualTo(MathF.PI / 2f).Within(0.0001f)
    );
  }

  [Test]
  public void FullRoll_PreservesUnwrappedBankThroughBakeAndSampling() {
    var fullTurn = MathF.PI * 2f;
    var piece = new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, new(10f, 0f, 0f), Vector3.UnitY, bank: 0f),
        Pair(1f, new(10f, 0f, 0f), new(10f, 0f, 0f), Vector3.UnitY, bank: fullTurn),
      ]),
      Matrix4x4.Identity,
      new(
        ChordToleranceGaugeFraction: 0f,
        MinimumChordTolerance: 1f,
        MaximumBankAngleChangeRadians: 0.2f,
        MaximumSubdivisionDepth: 12
      )
    );

    var quarter = piece.SampleRail(RailSide.Left, piece.Length * 0.25f);
    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.3f);
    var exit = piece.SampleRail(RailSide.Left, piece.Length);
    var quarterLateral = Vector3.Transform(Vector3.UnitY, quarter.Orientation);

    Assert.That(piece.BakedSampleCount, Is.GreaterThan(2));
    Assert.That(quarter.BankRadians, Is.EqualTo(MathF.PI / 2f).Within(0.0001f));
    Assert.That(Vector3.Dot(quarterLateral, Vector3.UnitZ), Is.GreaterThan(0.9999f));
    Assert.That(sample.BankRadians, Is.EqualTo(fullTurn * 0.3f).Within(0.0001f));
    Assert.That(exit.BankRadians, Is.EqualTo(fullTurn).Within(0.0001f));
  }

  [Test]
  public void Constructor_RejectsStationaryInteriorDerivativeOnShortSpan() {
    var shortSpan = TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, new(0.003f, 0f, 0f), Vector3.UnitY),
      Pair(1f, new(0.001f, 0f, 0f), new(0.003f, 0f, 0f), Vector3.UnitY),
    ]);

    Assert.Throws<ArgumentException>(new Action(() => new TrackPiece(shortSpan)));
  }

  [Test]
  public void Constructor_RejectsStationaryDerivativeRootsBetweenFixedProbes() {
    var hiddenRoots = TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, new(-0.01f, 0f, 0f), Vector3.UnitY),
      Pair(1f, new(0.01f, 0f, 0f), new(-0.01f, 0f, 0f), Vector3.UnitY),
    ]);

    Assert.Throws<ArgumentException>(new Action(() => new TrackPiece(hiddenRoots)));
  }

  [Test]
  public void FullRoll_CoarsestSafeBakeKeepsOrientationAlignedWithUnwrappedBank() {
    var fullTurn = MathF.PI * 2f;
    var geometry = TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, new(10f, 0f, 0f), Vector3.UnitY, bank: 0f),
      Pair(1f, new(10f, 0f, 0f), new(10f, 0f, 0f), Vector3.UnitY, bank: fullTurn),
    ]);
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() => new TrackPiece(
      geometry,
      Matrix4x4.Identity,
      new(MaximumBankAngleChangeRadians: fullTurn)
    )));

    var piece = new TrackPiece(
      geometry,
      Matrix4x4.Identity,
      new(
        ChordToleranceGaugeFraction: 0f,
        MinimumChordTolerance: 1f,
        MaximumBankAngleChangeRadians: TrackBakeSettings.MaximumSafeBankAngleChangeRadians,
        MaximumSubdivisionDepth: 12
      )
    );

    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.1f);
    var actualLateral = Vector3.Transform(Vector3.UnitY, sample.Orientation);
    var expectedLateral = Vector3.Transform(
      Vector3.UnitY,
      Quaternion.CreateFromAxisAngle(Vector3.UnitX, sample.BankRadians)
    );

    Assert.That(piece.BakedSampleCount, Is.EqualTo(5));
    Assert.That(Vector3.Dot(actualLateral, expectedLateral), Is.GreaterThan(0.9999f));
  }

  [Test]
  public void SampleRail_OrientationForwardMatchesHermiteTangentOnCoarseBake() {
    var piece = new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, new(2f, 0f, 0f), Vector3.UnitZ),
        Pair(1f, new(1f, 1f, 0f), new(0f, 0.2f, 0f), Vector3.UnitZ),
      ]),
      Matrix4x4.Identity,
      new(
        ChordToleranceGaugeFraction: 0f,
        MinimumChordTolerance: 100f,
        MaximumBankAngleChangeRadians: TrackBakeSettings.MaximumSafeBankAngleChangeRadians,
        MaximumSubdivisionDepth: 12
      )
    );

    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.5f);
    var orientationForward = Vector3.Transform(Vector3.UnitX, sample.Orientation);

    Assert.That(piece.BakedSampleCount, Is.EqualTo(2));
    Assert.That(Vector3.Dot(orientationForward, sample.Tangent), Is.GreaterThan(0.9999f));
  }

  [Test]
  public void Constructor_RejectsNonIncreasingCoarseBakedArcIntervals() {
    var selfReturning = TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, new(20f, 0f, 0f), Vector3.UnitZ),
      Pair(0.5f, new(10f, 0f, 0f), new(6f, 6f, 0f), Vector3.UnitZ),
      Pair(1f, new(10f, 0f, 0f), new(6f, -6f, 0f), Vector3.UnitZ),
    ]);
    var coarse = new TrackBakeSettings(
      ChordToleranceGaugeFraction: 0f,
      MinimumChordTolerance: 100f,
      MaximumBankAngleChangeRadians: TrackBakeSettings.MaximumSafeBankAngleChangeRadians,
      MaximumSubdivisionDepth: 12
    );

    Assert.Throws<ArgumentException>(new Action(() =>
      new TrackPiece(selfReturning, Matrix4x4.Identity, coarse)));
  }

  [Test]
  public void ExtremeFiniteGauge_KeepsDerivedRailFrameFinite() {
    var halfGauge = new Vector3(0f, 0f, 1e20f);
    var geometry = TrackPieceGeometry.FromHandAuthored([
      new(0f, -halfGauge, new(10f, 0f, 0f), halfGauge, new(10f, 0f, 0f), 0f),
      new(
        1f,
        new(10f, 0f, -1e20f),
        new(10f, 0f, 0f),
        new(10f, 0f, 1e20f),
        new(10f, 0f, 0f),
        0f
      ),
    ]);
    var piece = new TrackPiece(geometry);

    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.5f);
    var lateral = Vector3.Transform(Vector3.UnitY, sample.Orientation);

    Assert.That(float.IsFinite(sample.Orientation.X), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.Y), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.Z), Is.True);
    Assert.That(float.IsFinite(sample.Orientation.W), Is.True);
    Assert.That(float.IsFinite(lateral.X), Is.True);
    Assert.That(float.IsFinite(lateral.Y), Is.True);
    Assert.That(float.IsFinite(lateral.Z), Is.True);
  }

  [Test]
  public void Constructor_RejectsNonFiniteDerivedPlacementDeterminant() {
    var placement = new Matrix4x4(
      1e20f, 1e20f, 0f, 0f,
      1e20f, 1e20f, 0f, 0f,
      0f, 0f, 1e20f, 0f,
      0f, 0f, 0f, 1f
    );

    Assert.That(float.IsNaN(placement.GetDeterminant()), Is.True);
    Assert.Throws<ArgumentException>(new Action(() => new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ),
        Pair(1f, Vector3.UnitX, Vector3.UnitX, Vector3.UnitZ),
      ]),
      placement
    )));
  }

  [Test]
  public void Constructor_AcceptsConstantDiagonalDerivativeAboveEuclideanMinimum() {
    var derivative = new Vector3(0.8e-6f, 0.8e-6f, 0f);
    var piece = new TrackPiece(TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, derivative, Vector3.UnitZ),
      Pair(1f, derivative, derivative, Vector3.UnitZ),
    ]));

    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.5f);

    AssertVector(sample.Tangent, Vector3.Normalize(derivative));
  }

  [Test]
  public void SampleRail_SmallHermiteDerivativeMatchesSampledPositionDerivative() {
    const float scale = 1e-4f;
    var piece = new TrackPiece(
      TrackPieceGeometry.FromHandAuthored([
        Pair(0f, Vector3.Zero, new(2f * scale, 0f, 0f), Vector3.UnitZ),
        Pair(
          1f,
          new(scale, scale, 0f),
          new(0f, 0.2f * scale, 0f),
          Vector3.UnitZ
        ),
      ]),
      Matrix4x4.Identity,
      new(
        ChordToleranceGaugeFraction: 0f,
        MinimumChordTolerance: 100f,
        MaximumBankAngleChangeRadians: TrackBakeSettings.MaximumSafeBankAngleChangeRadians,
        MaximumSubdivisionDepth: 12
      )
    );

    var before = piece.SampleRail(RailSide.Left, piece.Length * 0.499f);
    var sample = piece.SampleRail(RailSide.Left, piece.Length * 0.5f);
    var after = piece.SampleRail(RailSide.Left, piece.Length * 0.501f);
    var numericalDerivative = Vector3.Normalize(after.Position - before.Position);

    Assert.That(piece.BakedSampleCount, Is.EqualTo(2));
    Assert.That(Vector3.Dot(sample.Tangent, numericalDerivative), Is.GreaterThan(0.9999f));
  }

  [Test]
  public void HandAuthoredGeometry_RejectsMalformedRailPairs() {
    Assert.Throws<ArgumentException>(new Action(() =>
      TrackPieceGeometry.FromHandAuthored([
        new(0f, Vector3.Zero, Vector3.UnitX, Vector3.Zero, Vector3.UnitX, 0f),
        new(1f, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, 0f),
      ])));
    Assert.Throws<ArgumentException>(new Action(() =>
      TrackPieceGeometry.FromHandAuthored([
        new(0f, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitX, 0f),
        new(1f, Vector3.UnitX, Vector3.UnitX, Vector3.One, -Vector3.UnitX, 0f),
      ])));
    Assert.Throws<ArgumentException>(new Action(() =>
      TrackPieceGeometry.FromHandAuthored([
        Pair(0.1f, Vector3.Zero, Vector3.UnitX, Vector3.UnitY),
        Pair(1f, Vector3.UnitX, Vector3.UnitX, Vector3.UnitY),
      ])));
  }

  private static TrackPiece StraightPiece(float length)
    => new(TrackPieceGeometry.FromHandAuthored([
      Pair(0f, Vector3.Zero, new(length, 0f, 0f), Vector3.UnitY),
      Pair(1f, new(length, 0f, 0f), new(length, 0f, 0f), Vector3.UnitY),
    ]));

  private static RailControlPair Pair(
    float parameter,
    Vector3 center,
    Vector3 tangent,
    Vector3 gaugeDirection,
    float bank = 0f
  ) {
    var halfGauge = Vector3.Normalize(gaugeDirection) * 0.5f;
    return new(
      parameter,
      center - halfGauge,
      tangent,
      center + halfGauge,
      tangent,
      bank
    );
  }

  private static void AssertVector(Vector3 actual, Vector3 expected)
    => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f));
}

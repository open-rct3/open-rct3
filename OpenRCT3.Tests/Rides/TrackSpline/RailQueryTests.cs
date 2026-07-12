// Rail Query Tests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using System.Numerics;
using OpenRCT3.Rides.TrackSpline;

namespace OpenRCT3.Tests.Rides.TrackSpline;

[TestFixture]
public class RailQueryTests {
  private const float Tolerance = 1e-3f;

  [SetUp]
  public void Setup() {
    ArcLength.ClearCache();
  }

  [Test]
  public void SampleRail_EmptyRail_ReturnsFalse() {
    var rail = new RailSpline();
    var result = RailQuery.SampleRail(rail, 0f, out _, out _, out _);
    Assert.That(result, Is.False);
  }

  [Test]
  public void SampleRail_SingleSample_ReturnsExactSample() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(1, 2, 3),
          Orientation = Quaternion.Identity,
          Bank = 0.5f,
          ArcLength = 0f,
        },
      },
      TotalArcLength = 0f,
    };

    var result = RailQuery.SampleRail(rail, 0f, out var pos, out var orient, out var bank);

    Assert.That(result, Is.True);
    Assert.That(pos.X, Is.EqualTo(1f).Within(Tolerance));
    Assert.That(pos.Y, Is.EqualTo(2f).Within(Tolerance));
    Assert.That(pos.Z, Is.EqualTo(3f).Within(Tolerance));
    Assert.That(bank, Is.EqualTo(0.5f).Within(Tolerance));
  }

  [Test]
  public void SampleRail_QueryAtFirstSample_ReturnsFirstSample() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, 0f, out var pos, out var _, out _);

    Assert.That(result, Is.True);
    Assert.That(pos.X, Is.EqualTo(0f).Within(Tolerance));
  }

  [Test]
  public void SampleRail_QueryAtSecondSample_ReturnsSecondSample() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0.5f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, 10f, out var pos, out var _, out var bank);

    Assert.That(result, Is.True);
    Assert.That(pos.X, Is.EqualTo(10f).Within(Tolerance));
    Assert.That(bank, Is.EqualTo(0.5f).Within(Tolerance));
  }

  [Test]
  public void SampleRail_QueryBetweenSamples_Interpolates() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 1f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, 5f, out var pos, out var _, out var bank);

    Assert.That(result, Is.True);
    // Interpolation should give position somewhere between (0,0,0) and (10,0,0)
    Assert.That(pos.X, Is.GreaterThan(0f));
    Assert.That(pos.X, Is.LessThan(10f));
    // Bank should be interpolated to ~0.5
    Assert.That(bank, Is.GreaterThan(0f));
    Assert.That(bank, Is.LessThan(1f));
  }

  [Test]
  public void SampleRail_QueryExceedsMaxArcLength_ClampsToEnd() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0.5f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, 100f, out var pos, out var _, out var bank);

    Assert.That(result, Is.True);
    Assert.That(pos.X, Is.EqualTo(10f).Within(Tolerance));
    Assert.That(bank, Is.EqualTo(0.5f).Within(Tolerance));
  }

  [Test]
  public void SampleRail_QueryNegativeArcLength_ClampsToStart() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0.5f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 1f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, -5f, out var pos, out var _, out var bank);

    Assert.That(result, Is.True);
    Assert.That(pos.X, Is.EqualTo(0f).Within(Tolerance));
    Assert.That(bank, Is.EqualTo(0.5f).Within(Tolerance));
  }

  [Test]
  public void SampleRail_OrientationNormalized() {
    var rail = new RailSpline {
      BakedSamples = new() {
        new() {
          Position = new Vector3(0, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 0f,
        },
        new() {
          Position = new Vector3(10, 0, 0),
          Orientation = Quaternion.Identity,
          Bank = 0f,
          ArcLength = 10f,
        },
      },
      TotalArcLength = 10f,
    };

    var result = RailQuery.SampleRail(rail, 5f, out _, out var orientation, out _);

    Assert.That(result, Is.True);
    // Quaternion should be normalized
    var mag = orientation.Length();
    Assert.That(mag, Is.EqualTo(1f).Within(Tolerance));
  }
}

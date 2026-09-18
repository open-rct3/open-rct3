// ImDrawTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using NUnit.Framework;
using OpenCobra.GDK;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class ImDrawTests {
  private const float Epsilon = 1e-4f;

  // Line() lays out each segment as six non-indexed vertices, two triangles forming a quad:
  // [0] a/-1, [1] b/-1, [2] a/+1, [3] a/+1, [4] b/-1, [5] b/+1 - see Line()'s own comment for why.
  private const int VerticesPerLine = 6;

  [Test]
  public void Line_AppendsSixVerticesToTheDepthTestedListByDefault() {
    var imDraw = new ImDraw();

    imDraw.Line(Vector3.Zero, Vector3.UnitX, Vector4.One);

    Assert.That(imDraw.vertices, Has.Count.EqualTo(VerticesPerLine));
    Assert.That(imDraw.alwaysOnTopVertices, Is.Empty);
  }

  [Test]
  public void Line_AlwaysOnTop_AppendsToTheAlwaysOnTopListInstead() {
    var imDraw = new ImDraw();

    imDraw.Line(Vector3.Zero, Vector3.UnitX, Vector4.One, alwaysOnTop: true);

    Assert.That(imDraw.vertices, Is.Empty);
    Assert.That(imDraw.alwaysOnTopVertices, Has.Count.EqualTo(VerticesPerLine));
  }

  [Test]
  public void Line_EachEndpointAppearsOnBothSidesOfTheExpandedQuad() {
    var a = new Vector3(1, 2, 3);
    var b = new Vector3(4, 5, 6);
    var imDraw = new ImDraw();

    imDraw.Line(a, b, Vector4.One);

    var atA = imDraw.vertices.Where(v => Vector3.Distance(v.Position, a) < Epsilon).ToArray();
    var atB = imDraw.vertices.Where(v => Vector3.Distance(v.Position, b) < Epsilon).ToArray();
    Assert.That(atA, Has.Length.EqualTo(3), "endpoint a should appear on 3 of the 6 quad vertices");
    Assert.That(atB, Has.Length.EqualTo(3), "endpoint b should appear on 3 of the 6 quad vertices");
    Assert.That(atA.Select(v => v.Side), Is.EquivalentTo(new[] { -1f, 1f, 1f }));
    Assert.That(atB.Select(v => v.Side), Is.EquivalentTo(new[] { -1f, -1f, 1f }));
  }

  [Test]
  public void Line_EveryVertexCarriesTheOtherEndpointForTheShaderToDeriveDirectionFrom() {
    var a = new Vector3(1, 0, 0);
    var b = new Vector3(0, 1, 0);
    var imDraw = new ImDraw();

    imDraw.Line(a, b, Vector4.One);

    foreach (var vertex in imDraw.vertices) {
      var expectedOther = Vector3.Distance(vertex.Position, a) < Epsilon ? b : a;
      Assert.That(Vector3.Distance(vertex.OtherPosition, expectedOther), Is.LessThan(Epsilon));
    }
  }

  [Test]
  public void Line_CarriesWidthAndColorOntoEveryVertex() {
    var color = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
    var imDraw = new ImDraw();

    imDraw.Line(Vector3.Zero, Vector3.UnitX, color, width: 7f);

    Assert.That(imDraw.vertices, Has.All.Matches<ImDrawVertex>(v => v.Width == 7f));
    Assert.That(imDraw.vertices, Has.All.Matches<ImDrawVertex>(v => v.Color == color));
  }

  [Test]
  public void Axis_EmitsThreeArmsOfSixVerticesEach() {
    var imDraw = new ImDraw();

    imDraw.Axis(Vector3.Zero, Quaternion.Identity, size: 2f);

    Assert.That(imDraw.vertices, Has.Count.EqualTo(3 * VerticesPerLine));
  }

  [Test]
  public void Axis_ArmsPointAlongTheRotatedBasisVectorsScaledBySize() {
    var origin = new Vector3(5, 5, 5);
    var imDraw = new ImDraw();

    imDraw.Axis(origin, Quaternion.Identity, size: 3f);

    // vertices[1] is the far ("b") endpoint of the first (X) arm's first triangle - see Line()'s layout.
    var xArmTip = imDraw.vertices[1].Position;
    var yArmTip = imDraw.vertices[VerticesPerLine + 1].Position;
    var zArmTip = imDraw.vertices[(2 * VerticesPerLine) + 1].Position;

    Assert.That(Vector3.Distance(xArmTip, origin + new Vector3(3, 0, 0)), Is.LessThan(Epsilon));
    Assert.That(Vector3.Distance(yArmTip, origin + new Vector3(0, 3, 0)), Is.LessThan(Epsilon));
    Assert.That(Vector3.Distance(zArmTip, origin + new Vector3(0, 0, 3)), Is.LessThan(Epsilon));
  }

  [Test]
  public void Axis_ArmsAreColoredRedGreenBlueRespectively() {
    var imDraw = new ImDraw();

    imDraw.Axis(Vector3.Zero, Quaternion.Identity, size: 1f);

    var xColor = imDraw.vertices[1].Color;
    var yColor = imDraw.vertices[VerticesPerLine + 1].Color;
    var zColor = imDraw.vertices[(2 * VerticesPerLine) + 1].Color;

    Assert.That(xColor.X, Is.GreaterThan(xColor.Y).And.GreaterThan(xColor.Z), "X arm should be red-dominant");
    Assert.That(yColor.Y, Is.GreaterThan(yColor.X).And.GreaterThan(yColor.Z), "Y arm should be green-dominant");
    Assert.That(zColor.Z, Is.GreaterThan(zColor.X).And.GreaterThan(zColor.Y), "Z arm should be blue-dominant");
  }

  [Test]
  public void Axis_ScreenSpaceExtent_ScalesSizeUsingCurrentFrameCameraDistance() {
    var origin = new Vector3(0, 0, 10);
    var imDraw = new ImDraw();
    imDraw.BeginFrame(cameraEye: Vector3.Zero, fieldOfViewYRadians: MathF.PI / 2f, viewportHeightPixels: 100f);

    imDraw.Axis(origin, Quaternion.Identity, size: 50f, screenSpaceExtent: true);

    // WorldSizeForPixels(distance=10, fov=90deg, viewportHeight=100) => 50 * 2*10*tan(45deg)/100 = 10.
    var xArmTip = imDraw.vertices[1].Position;
    Assert.That(Vector3.Distance(xArmTip, origin + new Vector3(10, 0, 0)), Is.LessThan(Epsilon));
  }

  [Test]
  public void Circle_ThrowsForFewerThanThreeSegments() {
    var imDraw = new ImDraw();

    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      imDraw.Circle(Vector3.Zero, Vector3.UnitZ, radius: 1f, Vector4.One, segments: 2)));
  }

  [Test]
  public void Circle_EmitsOneLinePerSegment() {
    var imDraw = new ImDraw();

    imDraw.Circle(Vector3.Zero, Vector3.UnitZ, radius: 1f, Vector4.One, segments: 8);

    Assert.That(imDraw.vertices, Has.Count.EqualTo(8 * VerticesPerLine));
  }

  [Test]
  public void Circle_FormsAClosedRing() {
    const int segments = 4;
    var imDraw = new ImDraw();

    imDraw.Circle(Vector3.Zero, Vector3.UnitZ, radius: 1f, Vector4.One, segments: segments);

    // vertices[0] is the first line's near ("a") endpoint - the ring's starting point. The last line's
    // far ("b") endpoint (index (segments-1)*6 + 1) should land back on that same point.
    var start = imDraw.vertices[0].Position;
    var lastLineFarIndex = ((segments - 1) * VerticesPerLine) + 1;
    var end = imDraw.vertices[lastLineFarIndex].Position;

    Assert.That(Vector3.Distance(start, end), Is.LessThan(Epsilon));
  }

  [Test]
  public void Circle_PointsLieOnTheGivenRadiusFromCenter() {
    var center = new Vector3(1, 2, 3);
    var imDraw = new ImDraw();

    imDraw.Circle(center, Vector3.UnitZ, radius: 5f, Vector4.One, segments: 6);

    foreach (var vertex in imDraw.vertices)
      Assert.That(Vector3.Distance(center, vertex.Position), Is.EqualTo(5f).Within(Epsilon));
  }

  [Test]
  public void Circle_PointsLieInThePlanePerpendicularToNormal() {
    var normal = Vector3.UnitZ;
    var imDraw = new ImDraw();

    imDraw.Circle(Vector3.Zero, normal, radius: 2f, Vector4.One, segments: 6);

    foreach (var vertex in imDraw.vertices)
      Assert.That(Vector3.Dot(vertex.Position, normal), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Arrow_DegenerateZeroLengthOnlyEmitsTheShaftLine() {
    var point = new Vector3(1, 1, 1);
    var imDraw = new ImDraw();

    imDraw.Arrow(point, point, Vector4.One);

    Assert.That(imDraw.vertices, Has.Count.EqualTo(VerticesPerLine));
  }

  [Test]
  public void Arrow_EmitsShaftPlusFourHeadLines() {
    var imDraw = new ImDraw();

    imDraw.Arrow(Vector3.Zero, new Vector3(0, 0, 10), Vector4.One, headSize: 2f);

    Assert.That(imDraw.vertices, Has.Count.EqualTo(5 * VerticesPerLine));
  }

  [Test]
  public void Arrow_AllFourHeadLinesConvergeAtTheArrowTip() {
    var to = new Vector3(0, 0, 10);
    var imDraw = new ImDraw();

    imDraw.Arrow(Vector3.Zero, to, Vector4.One, headSize: 2f);

    // Blocks 1-4 (index 0 is the shaft) are the four head lines; each one's far ("b") endpoint is `to`.
    for (var block = 1; block <= 4; block++) {
      var farIndex = (block * VerticesPerLine) + 1;
      Assert.That(Vector3.Distance(imDraw.vertices[farIndex].Position, to), Is.LessThan(Epsilon),
        $"head line {block} did not converge at the arrow tip");
    }
  }

  [Test]
  public void Arrow_HeadBaseSitsBehindTheTipAlongTheShaftDirection() {
    var to = new Vector3(0, 0, 10);
    var imDraw = new ImDraw();

    imDraw.Arrow(Vector3.Zero, to, Vector4.One, headSize: 2f);

    // Each head line's near ("a") endpoint is a base-ring point, headSize behind the tip along +Z.
    var baseIndex = VerticesPerLine; // first head line's near/"a" endpoint
    Assert.That(imDraw.vertices[baseIndex].Position.Z, Is.EqualTo(8f).Within(Epsilon));
  }

  [Test]
  public void Clear_EmptiesBothVertexLists() {
    var imDraw = new ImDraw();
    imDraw.Line(Vector3.Zero, Vector3.UnitX, Vector4.One);
    imDraw.Line(Vector3.Zero, Vector3.UnitY, Vector4.One, alwaysOnTop: true);

    imDraw.Clear();

    Assert.That(imDraw.vertices, Is.Empty);
    Assert.That(imDraw.alwaysOnTopVertices, Is.Empty);
  }

  [Test]
  public void WorldSizeForPixels_ReturnsPixelsUnchangedWhenBeginFrameWasNeverCalled() {
    // Fallback for callers (e.g. a unit test) that submit shapes with no renderer ever having called
    // BeginFrame - viewportHeightPixels defaults to 0, which WorldSizeForPixels treats as "no camera
    // info available yet" rather than dividing by zero.
    var imDraw = new ImDraw();

    var result = imDraw.WorldSizeForPixels(new Vector3(0, 0, 100), pixels: 42f);

    Assert.That(result, Is.EqualTo(42f));
  }

  [Test]
  public void WorldSizeForPixels_MatchesTheStandardEditorGizmoDistanceFovFormula() {
    var imDraw = new ImDraw();
    imDraw.BeginFrame(cameraEye: Vector3.Zero, fieldOfViewYRadians: MathF.PI / 2f, viewportHeightPixels: 100f);

    var result = imDraw.WorldSizeForPixels(new Vector3(0, 0, 10), pixels: 50f);

    // worldPerPixel = 2 * distance * tan(fov/2) / viewportHeight = 2*10*tan(45deg)/100 = 0.2
    Assert.That(result, Is.EqualTo(50f * 0.2f).Within(Epsilon));
  }

  [Test]
  public void WorldSizeForPixels_ScalesLinearlyWithCameraDistance() {
    var imDraw = new ImDraw();
    imDraw.BeginFrame(cameraEye: Vector3.Zero, fieldOfViewYRadians: MathF.PI / 2f, viewportHeightPixels: 100f);

    var nearResult = imDraw.WorldSizeForPixels(new Vector3(0, 0, 10), pixels: 50f);
    var farResult = imDraw.WorldSizeForPixels(new Vector3(0, 0, 20), pixels: 50f);

    Assert.That(farResult, Is.EqualTo(nearResult * 2f).Within(Epsilon));
  }
}

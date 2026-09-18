using DotNetEnv;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class StaticShapesIntegrationTests {
  [SetUp]
  public void Setup() {
    if (File.Exists(Constants.EnvFilePath))
      Env.NoClobber().Load(Constants.EnvFilePath);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping integration test.")]
  public void Extract_FromInstalledShapesArchive_DecodesGeometry() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var path = Path.Combine(rct3Path, "test", "Shapes.common.ovl");
    Assert.That(path, Does.Exist, $"Installed Shapes OVL not found: {path}");

    using var ovl = Ovl.Load(path);
    var shapes = StaticShapes.Extract(ovl);
    var meshCount = shapes.Sum(shape => shape.Meshes.Count);
    var vertexCount = shapes.Sum(shape => shape.Meshes.Sum(mesh => mesh.Vertices.Count));
    var indexCount = shapes.Sum(shape => shape.Meshes.Sum(mesh => mesh.Indices.Count));
    var referenceCount = shapes.Sum(shape => shape.Meshes.Sum(mesh =>
      Convert.ToInt32(mesh.FtxRef != null) + Convert.ToInt32(mesh.TxsRef != null)));

    TestContext.Progress.WriteLine(
      $"Installed SHS evidence: shapes={shapes.Count}, meshes={meshCount}, vertices={vertexCount}, " +
      $"indices={indexCount}, resolvedReferences={referenceCount}");
    using (Assert.EnterMultipleScope()) {
      Assert.That(shapes, Is.Not.Empty);
      Assert.That(meshCount, Is.GreaterThan(0));
      Assert.That(vertexCount, Is.GreaterThan(0));
      Assert.That(indexCount, Is.GreaterThan(0));
    }
  }
}

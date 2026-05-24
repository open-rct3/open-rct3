using System.Numerics;
using NUnit.Framework;
using OpenCobra.GDK.Meshes;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class MeshTests {
  [Test]
  public void MeshCreation_EmptyVertices() {
    var mesh = new Mesh([], []);
    Assert.That(mesh.Vertices, Is.Empty);
  }

  [Test]
  public void ComputeBoundingBox_CorrectlyCalculatesBounds() {
    var mesh = new Mesh([
      new Vertex { Position = new Vector3(-1, -1, -1) },
      new Vertex { Position = new Vector3( 1,  1,  1) }
    ],  []);
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.BoundingBox.Min, Is.EqualTo(new Vector3(-1, -1, -1)));
      Assert.That(mesh.BoundingBox.Max, Is.EqualTo(new Vector3(1, 1, 1)));
    }
  }
}

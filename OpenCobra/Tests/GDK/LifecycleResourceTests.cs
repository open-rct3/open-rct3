using OpenCobra.GDK;
using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Meshes;

namespace OVL.Tests.GDK;

[TestFixture]
public class LifecycleResourceTests {
  [Test]
  public void ModelDispose_AttemptsMeshAndMaterialAndTransfersOwnershipBeforeFailures() {
    var released = new List<string>();
    var model = new FailingModel(new Mesh([], []), released) { Material = new Flat() };

    var error = Assert.Throws<AggregateException>(new Action(model.Dispose));
    model.Dispose();

    using (Assert.EnterMultipleScope()) {
      Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
      Assert.That(released, Is.EqualTo(new[] { "mesh", "material" }));
      Assert.That(model.Material, Is.Null);
    }
  }

  [Test]
  public void SceneDispose_AttemptsEveryModelAndClearsStateBeforeReturningFailure() {
    var released = new List<string>();
    var scene = new TestScene();
    scene.Models.Add(new FailingModel(new Mesh([], []), released, "first"));
    scene.Models.Add(new FailingModel(new Mesh([], []), released, "second"));

    var error = Assert.Throws<AggregateException>(new Action(scene.Dispose));
    scene.Dispose();

    using (Assert.EnterMultipleScope()) {
      Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
      Assert.That(released, Is.EqualTo(new[] { "first-mesh", "second-mesh" }));
      Assert.That(scene.Models, Is.Empty);
      Assert.That(scene.State, Is.EqualTo(State.Disposed));
    }
  }

  private sealed class FailingModel(
    Mesh mesh,
    List<string> released,
    string? prefix = null
  ) : Model(mesh) {
    protected override void DisposeMesh(Mesh resource) {
      released.Add(prefix == null ? "mesh" : $"{prefix}-mesh");
      throw new InvalidOperationException("Injected mesh disposal failure.");
    }

    protected override void DisposeMaterial(Material resource) {
      released.Add(prefix == null ? "material" : $"{prefix}-material");
      throw new InvalidOperationException("Injected material disposal failure.");
    }
  }

  private sealed class TestScene : Scene {
    public TestScene() : base(null, null) { }
  }
}

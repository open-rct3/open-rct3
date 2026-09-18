using NUnit.Framework;
using OpenCobra.GDK.Game;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class WorldTests {
  [Test]
  public void Dispose_EmptySystemCollection_DoesNotThrow() {
    var world = new EmptyWorld();

    Assert.DoesNotThrow(new Action(world.Dispose));
  }

  [Test]
  public void Dispose_DispatchesToDerivedCleanup() {
    var world = new TrackingWorld();

    world.Dispose();

    Assert.That(world.DerivedCleanupRan, Is.True);
  }

  private sealed class EmptyWorld : World {
    public override void Load() {}
  }

  private sealed class TrackingWorld : World {
    public bool DerivedCleanupRan { get; private set; }

    public override void Load() {}

    protected override void Dispose(bool disposing) {
      DerivedCleanupRan = disposing;
      base.Dispose(disposing);
    }
  }
}

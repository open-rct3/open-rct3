using OpenRCT3.OpenGL;
using Silk.NET.Core.Contexts;

namespace OpenRCT3.Tests.OpenGL;

[TestFixture]
public class MacLifecycleTests {
  [TestCase(1)]
  [TestCase(2)]
  [TestCase(3)]
  [TestCase(4)]
  [TestCase(5)]
  [TestCase(6)]
  public void PartialInitialization_ReleasesEveryAcquiredStageInReverseOrder(int stage) {
    var released = new List<string>();
    var resources = CreateResources(stage, released);

    resources.Dispose(new FakeGlContext(released));
    resources.Dispose(new FakeGlContext(released));

    Assert.That(released, Is.EqualTo(ExpectedReleaseOrder(stage)));
  }

  [Test]
  public void PartialInitialization_AggregatesFailuresAndContinuesReverseRelease() {
    var released = new List<string>();
    var resources = new MacSurfaceResourceOwner();
    resources.OwnContext(() => Release("context"));
    resources.OwnGame(() => Release("game"));
    resources.OwnGl(() => Release("gl"));
    resources.OwnInput(() => Release("input"));
    resources.OwnController(() => Release("controller"));
    resources.OwnRenderer(() => Release("renderer"));

    var error = Assert.Throws<AggregateException>(new Action(() =>
      resources.Dispose(new FakeGlContext(released))));

    using (Assert.EnterMultipleScope()) {
      Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
      Assert.That(released, Is.EqualTo(new[] {
        "make-current", "game", "renderer", "controller", "input", "gl", "context",
      }));
    }
    Assert.DoesNotThrow(new Action(() =>
      resources.Dispose(new FakeGlContext(released))));

    void Release(string name) {
      released.Add(name);
      if (name is "renderer" or "input")
        throw new InvalidOperationException($"Injected {name} release failure.");
    }
  }

  [Test]
  public void GlContextLifetime_ReleaseClearsHandlesAndIsIdempotent() {
    var lifetime = new MacGlContextLifetime(42);
    lifetime.SetCurrentContext(84);

    var released = lifetime.Release();
    var repeated = lifetime.Release();

    using (Assert.EnterMultipleScope()) {
      Assert.That(released.Library, Is.EqualTo((nint)42));
      Assert.That(released.Context, Is.EqualTo((nint)84));
      Assert.That(repeated, Is.EqualTo(default(MacGlContextLifetime.Handles)));
      Assert.That(lifetime.ContextHandle, Is.EqualTo(nint.Zero));
      Assert.That(lifetime.IsCurrent(() => 84), Is.False);
      Assert.That(lifetime.IsCurrent(() =>
        throw new InvalidOperationException("Native current-context query should not run.")),
        Is.False);
      Assert.Throws<ObjectDisposedException>(new Action(() =>
        lifetime.SetCurrentContext(126)));
      Assert.Throws<ObjectDisposedException>(new Action(lifetime.ThrowIfDisposed));
      Assert.Throws<ObjectDisposedException>(new Action(() => {
        _ = lifetime.LibraryHandle;
      }));
    }
  }

  private static MacSurfaceResourceOwner CreateResources(
    int stage,
    List<string> released
  ) {
    var resources = new MacSurfaceResourceOwner();
    resources.OwnContext(() => released.Add("context"));
    if (stage >= 2) resources.OwnGame(() => released.Add("game"));
    if (stage >= 3) resources.OwnGl(() => released.Add("gl"));
    if (stage >= 4) resources.OwnInput(() => released.Add("input"));
    if (stage >= 5) resources.OwnController(() => released.Add("controller"));
    if (stage >= 6) resources.OwnRenderer(() => released.Add("renderer"));
    return resources;
  }

  private static IEnumerable<string> ExpectedReleaseOrder(int stage) {
    if (stage == 1) return ["context"];
    var order = new List<string> { "make-current", "game" };
    if (stage >= 6) order.Add("renderer");
    if (stage >= 5) order.Add("controller");
    if (stage >= 4) order.Add("input");
    if (stage >= 3) order.Add("gl");
    order.Add("context");
    return order;
  }

  private sealed class FakeGlContext(List<string> released) : IGLContext {
    public bool IsCurrent { get; private set; }
    public nint Handle => 1;
    public IGLContextSource? Source => null;

    public void MakeCurrent() {
      released.Add("make-current");
      IsCurrent = true;
    }

    public void Clear() { }
    public void SwapBuffers() { }
    public void SwapInterval(int interval) { }
    public void Dispose() { }
    public nint GetProcAddress(string proc, int? slot = null) => nint.Zero;

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
      addr = nint.Zero;
      return false;
    }
  }
}

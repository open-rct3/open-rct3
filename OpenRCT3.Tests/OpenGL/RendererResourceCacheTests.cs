using OpenCobra.GDK;
using OpenCobra.GDK.Meshes;
using OpenRCT3.OpenGL;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;

namespace OpenRCT3.Tests.OpenGL;

[TestFixture]
public class RendererResourceCacheTests {
  [TestCase(State.Uninitialized, false)]
  [TestCase(State.Ready, true)]
  [TestCase(State.Disposed, false)]
  public void CanRender_DependsOnRendererReadinessNotGameLoopState(
    State state,
    bool expected
  ) => Assert.That(Renderer.CanRender(state), Is.EqualTo(expected));

  [Test]
  public void GetOrAdd_CreatesEquivalentResourceOnce() {
    using var cache = new ResourceCache<string, uint>(_ => { });
    var uploads = 0;

    var first = cache.GetOrAdd("same", _ => {
      uploads++;
      return 42;
    });
    var second = cache.GetOrAdd("same", _ => {
      uploads++;
      return 84;
    });

    using (Assert.EnterMultipleScope()) {
      Assert.That(first, Is.EqualTo(42));
      Assert.That(second, Is.EqualTo(42));
      Assert.That(uploads, Is.EqualTo(1));
    }
  }

  [Test]
  public void Dispose_ReleasesEachCachedResourceOnce() {
    var released = new List<uint>();
    var cache = new ResourceCache<string, uint>(released.Add);
    cache.GetOrAdd("first", _ => 42);
    cache.GetOrAdd("second", _ => 84);

    cache.Dispose();
    cache.Dispose();

    Assert.That(released, Is.EquivalentTo(new uint[] { 42, 84 }));
  }

  [Test]
  public void Dispose_TransfersOwnershipAndAggregatesReleaseFailures() {
    var released = new List<uint>();
    var cache = new ResourceCache<string, uint>(resource => {
      released.Add(resource);
      if (resource == 42) throw new InvalidOperationException("Injected deletion failure.");
    });
    cache.GetOrAdd("first", _ => 42);
    cache.GetOrAdd("second", _ => 84);

    Assert.Throws<AggregateException>(new Action(cache.Dispose));
    Assert.That(released, Is.EqualTo(new uint[] { 42, 84 }));

    cache.Dispose();
    Assert.That(released, Has.Count.EqualTo(2));
    Assert.Throws<ObjectDisposedException>(new Action(() =>
      cache.GetOrAdd("third", _ => 126)));
  }

  [Test]
  public void GetOrAdd_DoesNotCacheFailedCreation() {
    using var cache = new ResourceCache<string, uint>(_ => { });
    var attempts = 0;

    Assert.Throws<InvalidOperationException>(new Action(() =>
      cache.GetOrAdd("same", _ => {
        attempts++;
        throw new InvalidOperationException("Injected upload failure.");
      })));
    var resource = cache.GetOrAdd("same", _ => {
      attempts++;
      return 42;
    });

    using (Assert.EnterMultipleScope()) {
      Assert.That(resource, Is.EqualTo(42));
      Assert.That(attempts, Is.EqualTo(2));
    }
  }

  [Test]
  public void MeshRegistry_ReleasesFirstContextAndReuploadsForSecondContext() {
    var vertices = new List<Vertex>();
    var indices = new List<uint> { 0, 1, 2 };
    var mesh = new Mesh(vertices, indices);
    var firstContext = new StatefulMeshGpuApi(1);
    var secondContext = new StatefulMeshGpuApi(101);
    var firstRegistry = new ContextResourceRegistry<Mesh>();

    mesh.Upload(new Shader(1), firstContext);
    firstRegistry.Track(mesh);
    firstRegistry.Track(mesh);
    firstContext.Draw(mesh);

    firstRegistry.Reset(resource => resource.ResetUpload(firstContext));
    firstRegistry.Reset(resource => resource.ResetUpload(firstContext));
    using (Assert.EnterMultipleScope()) {
      Assert.That(firstRegistry.Count, Is.Zero);
      Assert.That(mesh.State, Is.EqualTo(State.Uninitialized));
      Assert.That(mesh.Vao, Is.Zero);
      Assert.That(mesh.Vbo, Is.Zero);
      Assert.That(mesh.Ebo, Is.Zero);
      Assert.That(mesh.Vertices, Is.SameAs(vertices));
      Assert.That(mesh.Indices, Is.SameAs(indices));
      Assert.That(firstContext.DeletedBuffers, Is.EqualTo(new uint[] { 3, 2 }));
      Assert.That(firstContext.DeletedVertexArrays, Is.EqualTo(new uint[] { 1 }));
    }

    var secondRegistry = new ContextResourceRegistry<Mesh>();
    mesh.Upload(new Shader(2), secondContext);
    secondRegistry.Track(mesh);
    secondContext.Draw(mesh);

    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.State, Is.EqualTo(State.Ready));
      Assert.That((mesh.Vao, mesh.Vbo, mesh.Ebo), Is.EqualTo((101u, 102u, 103u)));
      Assert.That(firstContext.Draws, Is.EqualTo(new[] { (1u, 2u, 3u) }));
      Assert.That(secondContext.Draws, Is.EqualTo(new[] { (101u, 102u, 103u) }));
    }

    secondRegistry.Reset(resource => resource.ResetUpload(secondContext));
    secondRegistry.Reset(resource => resource.ResetUpload(secondContext));
    mesh.Dispose();
    mesh.Dispose();
    using (Assert.EnterMultipleScope()) {
      Assert.That(secondContext.DeletedBuffers, Is.EqualTo(new uint[] { 103, 102 }));
      Assert.That(secondContext.DeletedVertexArrays, Is.EqualTo(new uint[] { 101 }));
      Assert.That(mesh.State, Is.EqualTo(State.Disposed));
    }
  }

  [Test]
  public void MeshRegistry_RetainsOnlyFailedHandlesForRetry() {
    var mesh = new Mesh([], []);
    var context = new StatefulMeshGpuApi(1);
    var registry = new ContextResourceRegistry<Mesh>();
    mesh.Upload(new Shader(1), context);
    registry.Track(mesh);
    context.FailBufferDeletion = 3;

    Assert.Throws<AggregateException>(new Action(() =>
      registry.Reset(resource => resource.ResetUpload(context))));
    using (Assert.EnterMultipleScope()) {
      Assert.That(registry.Count, Is.EqualTo(1));
      Assert.That(mesh.State, Is.EqualTo(State.Ready));
      Assert.That(mesh.Vao, Is.Zero);
      Assert.That(mesh.Vbo, Is.Zero);
      Assert.That(mesh.Ebo, Is.EqualTo(3));
      Assert.That(context.DeletedBuffers, Is.EqualTo(new uint[] { 3, 2 }));
      Assert.That(context.DeletedVertexArrays, Is.EqualTo(new uint[] { 1 }));
    }

    context.FailBufferDeletion = null;
    registry.Reset(resource => resource.ResetUpload(context));
    using (Assert.EnterMultipleScope()) {
      Assert.That(registry.Count, Is.Zero);
      Assert.That(mesh.State, Is.EqualTo(State.Uninitialized));
      Assert.That(context.DeletedBuffers, Is.EqualTo(new uint[] { 3, 2, 3 }));
      Assert.That(context.DeletedVertexArrays, Is.EqualTo(new uint[] { 1 }));
    }
  }

  [Test]
  public void RendererTeardown_MakesContextCurrentBeforeEveryRelease() {
    var order = new List<string>();
    var context = new FakeGlContext(order);

    RendererTeardown.Run(context, [
      () => order.Add("scene"),
      () => order.Add("program"),
      () => order.Add("texture"),
      () => order.Add("context"),
      () => order.Add("gl"),
    ]);

    Assert.That(order, Is.EqualTo(new[] {
      "make-current", "scene", "program", "texture", "context", "gl",
    }));
  }

  [Test]
  public void RendererTeardown_AttemptsLaterReleasesAfterDeletionFailure() {
    var order = new List<string>();
    var context = new FakeGlContext(order);

    Assert.Throws<AggregateException>(new Action(() => RendererTeardown.Run(context, [
      () => throw new InvalidOperationException("Injected program deletion failure."),
      () => order.Add("texture"),
      () => order.Add("context"),
      () => order.Add("gl"),
    ])));

    Assert.That(order, Is.EqualTo(new[] { "make-current", "texture", "context", "gl" }));
  }

  [Test]
  public void RendererTeardown_RejectsContextThatDidNotBecomeCurrent() {
    var order = new List<string>();
    var context = new FakeGlContext(order) { MakesCurrent = false };

    Assert.Throws<InvalidOperationException>(new Action(() =>
      RendererTeardown.Run(context, [() => order.Add("release")])));

    Assert.That(order, Is.EqualTo(new[] { "make-current" }));
  }

  private sealed class FakeGlContext(List<string> order) : IGLContext {
    public bool MakesCurrent { get; set; } = true;
    public bool IsCurrent { get; private set; }
    public nint Handle => 1;
    public IGLContextSource? Source => null;

    public void Clear() { }

    public void MakeCurrent() {
      order.Add("make-current");
      IsCurrent = MakesCurrent;
    }

    public void SwapBuffers() { }
    public void SwapInterval(int interval) { }
    public void Dispose() { }
    public nint GetProcAddress(string proc, int? slot = null) => nint.Zero;

    public bool TryGetProcAddress(string proc, out nint addr, int? slot = null) {
      addr = nint.Zero;
      return false;
    }
  }

  private sealed class StatefulMeshGpuApi(uint nextHandle) : Mesh.IGpuApi {
    private readonly HashSet<uint> vertexArrays = [];
    private readonly HashSet<uint> buffers = [];

    public uint? FailBufferDeletion { get; set; }
    public List<uint> DeletedBuffers { get; } = [];
    public List<uint> DeletedVertexArrays { get; } = [];
    public List<(uint Vao, uint Vbo, uint Ebo)> Draws { get; } = [];

    public uint CreateVertexArray() {
      var handle = nextHandle++;
      vertexArrays.Add(handle);
      return handle;
    }

    public uint CreateBuffer() {
      var handle = nextHandle++;
      buffers.Add(handle);
      return handle;
    }

    public void BindVertexArray(uint handle) { }
    public void BindVertexBuffer(uint handle) { }
    public void UploadVertices(Vertex[] vertices) { }
    public void BindIndexBuffer(uint handle) { }
    public void UploadIndices(uint[] indices) { }

    public int GetAttributeLocation(Shader shader, string name) =>
      name is "a_Position" or "a_Color" ? 0 : -1;

    public void BindVertexAttribute(uint location, int size, uint stride, nint offset) { }
    public void CheckError(string operation) { }
    public void FinishUpload() { }

    public void DeleteVertexArray(uint handle) {
      DeletedVertexArrays.Add(handle);
      vertexArrays.Remove(handle);
    }

    public void DeleteBuffer(uint handle) {
      DeletedBuffers.Add(handle);
      if (FailBufferDeletion == handle)
        throw new InvalidOperationException("Injected buffer deletion failure.");
      buffers.Remove(handle);
    }

    public void Draw(Mesh mesh) {
      if (!vertexArrays.Contains(mesh.Vao) ||
          !buffers.Contains(mesh.Vbo) ||
          !buffers.Contains(mesh.Ebo))
        throw new InvalidOperationException("Mesh handles do not belong to this context.");
      Draws.Add((mesh.Vao, mesh.Vbo, mesh.Ebo));
    }
  }
}

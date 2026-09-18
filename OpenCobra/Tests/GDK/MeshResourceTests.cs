using OpenCobra.GDK;
using OpenCobra.GDK.Meshes;
using Silk.NET.OpenGL;

namespace OVL.Tests.GDK;

[TestFixture]
public class MeshResourceTests {
  [TestCase("UploadVertices", new uint[] { 2 }, new uint[] { 1 })]
  [TestCase("UploadIndices", new uint[] { 3, 2 }, new uint[] { 1 })]
  public void UploadFailure_ReleasesEveryPartialHandleAndAllowsRetry(
    string failure,
    uint[] expectedBuffers,
    uint[] expectedVertexArrays
  ) {
    var mesh = new Mesh([], []);
    var gpu = new FakeMeshGpuApi { Failure = failure };

    Assert.Throws<InvalidOperationException>(new Action(() =>
      mesh.Upload(new Shader(1), gpu)));
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.State, Is.EqualTo(State.Uninitialized));
      Assert.That(mesh.Vao, Is.Zero);
      Assert.That(mesh.Vbo, Is.Zero);
      Assert.That(mesh.Ebo, Is.Zero);
      Assert.That(gpu.DeletedBuffers, Is.EqualTo(expectedBuffers));
      Assert.That(gpu.DeletedVertexArrays, Is.EqualTo(expectedVertexArrays));
    }

    gpu.Failure = null;
    mesh.Upload(new Shader(1), gpu);
    Assert.That(mesh.State, Is.EqualTo(State.Ready));
    mesh.Dispose(gpu);
  }

  [Test]
  public void Dispose_TransfersHandlesAndAttemptsEveryDeletionOnce() {
    var mesh = new Mesh([], []);
    var gpu = new FakeMeshGpuApi();
    mesh.Upload(new Shader(1), gpu);
    gpu.FailFirstBufferDeletion = true;

    Assert.Throws<AggregateException>(new Action(() => mesh.Dispose(gpu)));
    var deletionCount = gpu.DeletedBuffers.Count + gpu.DeletedVertexArrays.Count;
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.State, Is.EqualTo(State.Disposed));
      Assert.That(mesh.Vao, Is.Zero);
      Assert.That(mesh.Vbo, Is.Zero);
      Assert.That(mesh.Ebo, Is.Zero);
      Assert.That(gpu.DeletedBuffers, Has.Count.EqualTo(2));
      Assert.That(gpu.DeletedVertexArrays, Has.Count.EqualTo(1));
    }

    mesh.Dispose(gpu);
    Assert.That(gpu.DeletedBuffers.Count + gpu.DeletedVertexArrays.Count,
      Is.EqualTo(deletionCount));
  }

  [TestCase("Vao", new uint[] { }, new uint[] { })]
  [TestCase("Vbo", new uint[] { }, new uint[] { 1 })]
  [TestCase("Ebo", new uint[] { 2 }, new uint[] { 1 })]
  public void ZeroHandle_ReleasesPriorAllocationsAndAllowsRetryAndDispose(
    string zeroAllocation,
    uint[] expectedBuffers,
    uint[] expectedVertexArrays
  ) {
    var mesh = new Mesh([], []);
    var gpu = new FakeMeshGpuApi { ZeroAllocation = zeroAllocation };

    Assert.Throws<InvalidOperationException>(new Action(() =>
      mesh.Upload(new Shader(1), gpu)));
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.State, Is.EqualTo(State.Uninitialized));
      Assert.That(mesh.Vao, Is.Zero);
      Assert.That(mesh.Vbo, Is.Zero);
      Assert.That(mesh.Ebo, Is.Zero);
      Assert.That(gpu.DeletedBuffers, Is.EqualTo(expectedBuffers));
      Assert.That(gpu.DeletedVertexArrays, Is.EqualTo(expectedVertexArrays));
    }

    gpu.ZeroAllocation = null;
    mesh.Upload(new Shader(1), gpu);
    Assert.That(mesh.State, Is.EqualTo(State.Ready));
    var deletionCount = gpu.DeletedBuffers.Count + gpu.DeletedVertexArrays.Count;

    mesh.Dispose(gpu);
    mesh.Dispose(gpu);
    using (Assert.EnterMultipleScope()) {
      Assert.That(mesh.State, Is.EqualTo(State.Disposed));
      Assert.That(gpu.DeletedBuffers, Has.Count.EqualTo(expectedBuffers.Length + 2));
      Assert.That(gpu.DeletedVertexArrays,
        Has.Count.EqualTo(expectedVertexArrays.Length + 1));
      Assert.That(gpu.DeletedBuffers.Count + gpu.DeletedVertexArrays.Count,
        Is.EqualTo(deletionCount + 3));
    }
  }

  [Test]
  public void DisposingUninitializedMesh_IsIdempotentWithoutGlContext() {
    var mesh = new Mesh([], []);

    mesh.Dispose();
    mesh.Dispose();

    Assert.That(mesh.State, Is.EqualTo(State.Disposed));
    Assert.Throws<ObjectDisposedException>(new Action(() =>
      mesh.Upload(new Shader(0))));
  }

  private sealed class FakeMeshGpuApi : Mesh.IGpuApi {
    private uint nextHandle = 1;

    public string? Failure { get; set; }
    public string? ZeroAllocation { get; set; }
    public bool FailFirstBufferDeletion { get; set; }
    public List<uint> DeletedBuffers { get; } = [];
    public List<uint> DeletedVertexArrays { get; } = [];

    public uint CreateVertexArray() => Allocate(nameof(Mesh.Vao));

    public uint CreateBuffer() => Allocate(
      nextHandle % 3 == 2 ? nameof(Mesh.Vbo) : nameof(Mesh.Ebo));
    public void BindVertexArray(uint handle) => MaybeFail(nameof(BindVertexArray));
    public void BindVertexBuffer(uint handle) => MaybeFail(nameof(BindVertexBuffer));
    public void UploadVertices(Vertex[] vertices) => MaybeFail(nameof(UploadVertices));
    public void BindIndexBuffer(uint handle) => MaybeFail(nameof(BindIndexBuffer));
    public void UploadIndices(uint[] indices) => MaybeFail(nameof(UploadIndices));

    public int GetAttributeLocation(Shader shader, string name) =>
      name is "a_Position" or "a_Color" ? 0 : -1;

    public void BindVertexAttribute(uint location, int size, uint stride, nint offset) =>
      MaybeFail(nameof(BindVertexAttribute));

    public void CheckError(string operation) => MaybeFail(nameof(CheckError));
    public void FinishUpload() => MaybeFail(nameof(FinishUpload));

    public void DeleteVertexArray(uint handle) => DeletedVertexArrays.Add(handle);

    public void DeleteBuffer(uint handle) {
      DeletedBuffers.Add(handle);
      if (!FailFirstBufferDeletion) return;
      FailFirstBufferDeletion = false;
      throw new InvalidOperationException("Injected buffer deletion failure.");
    }

    private void MaybeFail(string operation) {
      if (Failure == operation)
        throw new InvalidOperationException($"Injected {operation} failure.");
    }

    private uint Allocate(string resource) {
      if (ZeroAllocation == resource) return 0;
      return nextHandle++;
    }
  }
}

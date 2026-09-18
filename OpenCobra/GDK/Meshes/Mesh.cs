// Mesh
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Memory;
using OpenCobra.OVL;
using Silk.NET.OpenGL;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.GDK.Meshes;

/// <summary>
/// Stores geometry data for rendering: vertices, indices, and an optional
/// bounding box.
/// </summary>
/// <remarks>
/// Primitives follow the <abbr title="Counter-Clockwise">CCW</abbr> winding convention, matching the industry standard.
/// Counter-clockwise is the default OpenGL and Direct3D front-face rule.
/// </remarks>
public class Mesh(List<Vertex> vertices, List<uint> indices) : IResource {
  [Category("Design")]
  public string? Name { get; set; }
  [Category("Data")]
  // TODO: Optimize: Do not store vertices nor indices in CPU memory
  public List<Vertex> Vertices { get; init; } = vertices;
  [Category("Data")]
  public List<uint> Indices { get; init; } = indices;
  [Category("Data")]
  public BoundingBox BoundingBox { get; private set; } = ComputeBoundingBox(vertices);

  /// <summary>
  /// GPU-resident handles allocated on first upload.
  /// </summary>
  public uint Vao { get; private set; }
  public uint Vbo { get; private set; }
  public uint Ebo { get; private set; }

  [Category("GPU")]
  public State State { get; private set; }

  /// <summary>
  /// Uploads vertex and index data to the GPU and caches GL handles on this instance.
  /// Subsequent renders reuse cached handles; no re-upload unless data changes.
  /// </summary>
  // TODO: Extract this method into the renderer
  public void Upload(Shader shader) {
    ObjectDisposedException.ThrowIf(State == State.Disposed, this);
    if (State == State.Ready) return;
    Upload(shader, new SilkGpuApi(IGame.IoC.Resolve<GL>()));
  }

  /// <summary>
  /// Uploads through a renderer-provided GPU API so allocation and cleanup stay transactional.
  /// </summary>
  public void Upload(Shader shader, IGpuApi gpu) {
    ObjectDisposedException.ThrowIf(State == State.Disposed, this);
    if (State == State.Ready) return;
    var vao = 0u;
    var vbo = 0u;
    var ebo = 0u;
    try {
      // VAO
      vao = gpu.CreateVertexArray();
      ValidateHandle(vao, "vertex array");
      gpu.BindVertexArray(vao);
      gpu.CheckError(string.Format("Binding {0} vertex array", Name));

      // VBO
      vbo = gpu.CreateBuffer();
      ValidateHandle(vbo, "vertex buffer");
      gpu.BindVertexBuffer(vbo);
      gpu.UploadVertices(Vertices.ToArray());
      gpu.CheckError(string.Format("Uploading {0} vertex buffer", Name));

      // EBO
      ebo = gpu.CreateBuffer();
      ValidateHandle(ebo, "index buffer");
      gpu.BindIndexBuffer(ebo);
      gpu.UploadIndices(Indices.ToArray());
      gpu.CheckError(string.Format("Uploading {0} index buffer", Name));

      #region Bind vertex attributes to shader locations
      var stride = Convert.ToUInt32(Marshal.SizeOf<Vertex>());

      var posLoc = gpu.GetAttributeLocation(shader, "a_Position");
      Debug.Assert(posLoc >= 0);
      gpu.BindVertexAttribute(CastFrom<int>.To<uint>(posLoc), 3, stride, 0);
      gpu.CheckError(string.Format("Binding {0} vertex attribute: {1}", Name, "a_Position"));

      var normLoc = gpu.GetAttributeLocation(shader, "a_Normal");
      if (normLoc >= 0) {
        gpu.BindVertexAttribute(CastFrom<int>.To<uint>(normLoc), 3, stride, 12);
        gpu.CheckError(string.Format("Binding {0} vertex attribute: {1}", Name, "a_Normal"));
      }

      var texLoc = gpu.GetAttributeLocation(shader, "a_TexCoord");
      if (texLoc >= 0) {
        gpu.BindVertexAttribute(CastFrom<int>.To<uint>(texLoc), 2, stride, 24);
        gpu.CheckError(string.Format("Binding {0} vertex attribute: {1}", Name, "a_TexCoord"));
      }

      var colLoc = gpu.GetAttributeLocation(shader, "a_Color");
      Debug.Assert(colLoc >= 0);
      gpu.BindVertexAttribute(CastFrom<int>.To<uint>(colLoc), 4, stride, 32);
      gpu.CheckError(string.Format("Binding {0} vertex attribute: {1}", Name, "a_Color"));
      #endregion

      // Cleanup
      gpu.FinishUpload();

      Vao = vao;
      Vbo = vbo;
      Ebo = ebo;
      State = State.Ready;
    } catch (Exception uploadError) {
      var cleanupErrors = ReleaseGpuResources(gpu, vao, vbo, ebo);
      if (cleanupErrors.Count > 0)
        throw new AggregateException([uploadError, .. cleanupErrors]);
      throw;
    }
  }

  /// <summary>
  /// Replaces this mesh's vertex/index data in place and, if already GPU-uploaded, tears down its
  /// GL buffers and reverts to <see cref="Meshes.State.Uninitialized"/> so <see cref="Scene.UninitializedModels"/>
  /// picks it back up and re-uploads the new data on the next render.
  /// </summary>
  public void Replace(List<Vertex> vertices, List<uint> indices) {
    Vertices.Clear();
    Vertices.AddRange(vertices);
    Indices.Clear();
    Indices.AddRange(indices);
    BoundingBox = ComputeBoundingBox(vertices);
    if (State != State.Ready) return;

    var gl = IGame.IoC.Resolve<GL>();
    gl.DeleteVertexArray(Vao);
    Vao = 0;
    gl.DeleteBuffer(Vbo);
    Vbo = 0;
    gl.DeleteBuffer(Ebo);
    Ebo = 0;

    State = State.Uninitialized;
  }

  public void Dispose() {
    if (State == State.Disposed) return;

    if (Vao == 0 && Vbo == 0 && Ebo == 0) {
      State = State.Disposed;
      return;
    }
    Dispose(new SilkGpuApi(IGame.IoC.Resolve<GL>()));
  }

  /// <summary>
  /// Releases handles owned by the current GPU context while preserving CPU geometry for re-upload.
  /// </summary>
  public void ResetUpload() {
    if (State == State.Disposed) return;
    if (Vao == 0 && Vbo == 0 && Ebo == 0) {
      State = State.Uninitialized;
      return;
    }
    ResetUpload(new SilkGpuApi(IGame.IoC.Resolve<GL>()));
  }

  /// <summary>
  /// Releases context-bound handles through a renderer-provided GPU API.
  /// Successfully released handles are cleared immediately; failures remain owned for retry.
  /// </summary>
  public void ResetUpload(IGpuApi gpu) {
    if (State == State.Disposed) return;
    if (Vao == 0 && Vbo == 0 && Ebo == 0) {
      State = State.Uninitialized;
      return;
    }

    var errors = new List<Exception>();
    if (Ebo != 0 && TryRelease(() => gpu.DeleteBuffer(Ebo), errors)) Ebo = 0;
    if (Vbo != 0 && TryRelease(() => gpu.DeleteBuffer(Vbo), errors)) Vbo = 0;
    if (Vao != 0 && TryRelease(() => gpu.DeleteVertexArray(Vao), errors)) Vao = 0;
    if (Vao == 0 && Vbo == 0 && Ebo == 0) State = State.Uninitialized;
    if (errors.Count > 0) throw new AggregateException(errors);
  }

  /// <summary>
  /// Releases every owned GPU handle once, even when an individual deletion fails.
  /// </summary>
  public void Dispose(IGpuApi gpu) {
    if (State == State.Disposed) return;

    var vao = Vao;
    var vbo = Vbo;
    var ebo = Ebo;
    Vao = 0;
    Vbo = 0;
    Ebo = 0;

    State = State.Disposed;
    var cleanupErrors = ReleaseGpuResources(gpu, vao, vbo, ebo);
    if (cleanupErrors.Count > 0) throw new AggregateException(cleanupErrors);
  }

  public interface IGpuApi {
    uint CreateVertexArray();
    uint CreateBuffer();
    void BindVertexArray(uint handle);
    void BindVertexBuffer(uint handle);
    void UploadVertices(Vertex[] vertices);
    void BindIndexBuffer(uint handle);
    void UploadIndices(uint[] indices);
    int GetAttributeLocation(Shader shader, string name);
    void BindVertexAttribute(uint location, int size, uint stride, nint offset);
    void CheckError(string operation);
    void FinishUpload();
    void DeleteVertexArray(uint handle);
    void DeleteBuffer(uint handle);
  }

  private static List<Exception> ReleaseGpuResources(
    IGpuApi gpu,
    uint vao,
    uint vbo,
    uint ebo
  ) {
    var errors = new List<Exception>();
    if (ebo != 0) TryRelease(() => gpu.DeleteBuffer(ebo), errors);
    if (vbo != 0) TryRelease(() => gpu.DeleteBuffer(vbo), errors);
    if (vao != 0) TryRelease(() => gpu.DeleteVertexArray(vao), errors);
    return errors;
  }

  private static void ValidateHandle(uint handle, string resource) {
    if (handle == 0)
      throw new InvalidOperationException($"GPU allocation returned a zero {resource} handle.");
  }

  private static bool TryRelease(Action release, List<Exception> errors) {
    try {
      release();
      return true;
    } catch (Exception error) {
      errors.Add(error);
      return false;
    }
  }

  private sealed class SilkGpuApi(GL gl) : IGpuApi {
    public uint CreateVertexArray() => gl.GenVertexArray();
    public uint CreateBuffer() => gl.GenBuffer();
    public void BindVertexArray(uint handle) => gl.BindVertexArray(handle);

    public void BindVertexBuffer(uint handle) =>
      gl.BindBuffer(BufferTargetARB.ArrayBuffer, handle);

    public void UploadVertices(Vertex[] vertices) => gl.BufferData<Vertex>(
      BufferTargetARB.ArrayBuffer,
      Convert.ToUInt32(vertices.Length * Marshal.SizeOf<Vertex>()),
      vertices,
      BufferUsageARB.StaticDraw);

    public void BindIndexBuffer(uint handle) =>
      gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, handle);

    public void UploadIndices(uint[] indices) => gl.BufferData<uint>(
      BufferTargetARB.ElementArrayBuffer,
      Convert.ToUInt32(indices.Length * sizeof(uint)),
      indices,
      BufferUsageARB.StaticDraw);

    public int GetAttributeLocation(Shader shader, string name) =>
      gl.GetAttribLocation(shader.Handle, name);

    public void BindVertexAttribute(uint location, int size, uint stride, nint offset) {
      gl.EnableVertexAttribArray(location);
      gl.VertexAttribPointer(
        location,
        size,
        VertexAttribPointerType.Float,
        normalized: false,
        stride,
        offset);
    }

    public void CheckError(string operation) => gl.CheckError(operation);

    public void FinishUpload() {
      gl.BindVertexArray(0);
      gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    public void DeleteVertexArray(uint handle) => gl.DeleteVertexArray(handle);
    public void DeleteBuffer(uint handle) => gl.DeleteBuffer(handle);
  }

  private static BoundingBox ComputeBoundingBox(List<Vertex> vertices) {
    if (vertices.Count == 0) return BoundingBox.Empty;

    var min = vertices[0].Position;
    var max = vertices[0].Position;

    foreach (var v in vertices) {
      min = Vector3.Min(min, v.Position);
      max = Vector3.Max(max, v.Position);
    }

    return new BoundingBox(min, max);
  }
}

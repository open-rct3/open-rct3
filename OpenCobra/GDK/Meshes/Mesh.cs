// Mesh
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Memory;
using OpenCobra.GDK.Services;
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
  public BoundingBox BoundingBox { get; } = ComputeBoundingBox(vertices);

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
    if (State == State.Ready) return;
    var gl = Scene.IoC.Resolve<IGL>().Context;

    // VAO
    Vao = gl.GenVertexArray();
    gl.BindVertexArray(Vao);

    // VBO
    Vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
    var vertices = Vertices.ToArray();
    gl.BufferData<Vertex>(
      BufferTargetARB.ElementArrayBuffer,
      Convert.ToUInt32(vertices.Length * Marshal.SizeOf<Vertex>()),
      vertices,
      BufferUsageARB.StaticDraw);

    // EBO
    Ebo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
    var indices = Indices.ToArray();
    gl.BufferData<uint>(
      BufferTargetARB.ElementArrayBuffer,
      Convert.ToUInt32(indices.Length * sizeof(uint)),
      indices,
      BufferUsageARB.StaticDraw);

    // Bind vertex attributes to shader locations
    var stride = Convert.ToUInt32(Marshal.SizeOf<Vertex>());

    var posLoc = gl.GetAttribLocation(shader.Handle, "a_Position");
    Debug.Assert(posLoc >= 0);
    gl.EnableVertexAttribArray(CastFrom<int>.To<uint>(posLoc));
    gl.VertexAttribPointer(
      CastFrom<int>.To<uint>(posLoc),
      3,
      VertexAttribPointerType.Float,
      normalized: false,
      stride,
      CastFrom<int>.To<nint>(0));

    var normLoc = gl.GetAttribLocation(shader.Handle, "a_Normal");
    if (normLoc >= 0) {
      gl.EnableVertexAttribArray(CastFrom<int>.To<uint>(normLoc));
      gl.VertexAttribPointer(
        CastFrom<int>.To<uint>(normLoc),
        3,
        VertexAttribPointerType.Float,
        normalized: false,
        stride,
        CastFrom<int>.To<nint>(12));
    }

    var texLoc = gl.GetAttribLocation(shader.Handle, "a_TexCoord");
    if (texLoc >= 0) {
      gl.EnableVertexAttribArray(CastFrom<int>.To<uint>(texLoc));
      gl.VertexAttribPointer(
        CastFrom<int>.To<uint>(texLoc),
        2,
        VertexAttribPointerType.Float,
        normalized: false,
        stride,
        CastFrom<int>.To<nint>(24));
    }

    var colLoc = gl.GetAttribLocation(shader.Handle, "a_Color");
    Debug.Assert(colLoc >= 0);
    gl.EnableVertexAttribArray(CastFrom<int>.To<uint>(colLoc));
    gl.VertexAttribPointer(
      CastFrom<int>.To<uint>(colLoc),
      4,
      VertexAttribPointerType.Float,
      normalized: false,
      stride,
      CastFrom<int>.To<nint>(32));

    // Cleanup
    gl.BindVertexArray(0);
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

    State = State.Ready;
  }

  public void Dispose() {
    if (State == State.Disposed) return;

    var gl = Scene.IoC.Resolve<IGL>().Context;
    gl.DeleteVertexArray(Vao);
    Vao = 0;
    gl.DeleteBuffer(Vbo);
    Vbo = 0;
    gl.DeleteBuffer(Ebo);
    Ebo = 0;

    State = State.Disposed;
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

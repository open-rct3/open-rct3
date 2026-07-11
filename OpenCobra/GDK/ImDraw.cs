// ImDraw
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Memory;
using OpenCobra.GDK.Shaders;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenCobra.GDK;

/// <summary>
/// A single accumulated triangle vertex for <see cref="ImDraw"/>'s thick-line draw path. Every
/// <see cref="ImDraw"/> primitive is expanded to a pair of these triangles before upload; see
/// <see cref="ImDraw.Line"/>. <see cref="OtherPosition"/> gives the vertex shader the segment's other
/// endpoint so it can derive a screen-space direction and offset this vertex perpendicular to it by a
/// constant pixel amount, regardless of camera distance.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ImDrawVertex {
  public Vector3 Position;
  public Vector3 OtherPosition;
  /// <summary>Which side of the line this vertex is offset to: <c>-1</c> or <c>+1</c>.</summary>
  public float Side;
  /// <summary>Desired line width, in pixels.</summary>
  public float Width;
  public Vector4 Color;
}

/// <summary>
/// Accumulate-then-flush immediate-mode draw primitives: <see cref="Line"/>, and the
/// <see cref="Axis"/>/<see cref="Circle"/>/<see cref="Arrow"/> shapes composed from it. Not debug-only —
/// see <c>.agents/plans/features/terrain/debug-draw.md</c> for why this is named <c>ImDraw</c> (echoing
/// ImGui) rather than a debug-only name: a planned future consumer (the scenery "advanced move" gizmo)
/// is release-build and player-facing, not a dev aid.
/// </summary>
/// <remarks>
/// Callers append shapes once per frame; <c>OpenRCT3.OpenGL.Renderer</c> calls <see cref="Render"/> once
/// per frame (uploading and drawing the accumulated batch), then <see cref="Clear"/>. This type owns its
/// own GPU resources (shader, VAO, VBO) end to end — matching how <c>Mesh</c> owns its VAO/VBO/upload
/// rather than the renderer managing them — so the app layer never touches raw GL handles for this draw
/// path. Every shape is triangles, not <c>GL_LINES</c> — <see cref="Line"/> expands to a
/// screen-space-constant-width quad, computed in the vertex shader from each vertex's
/// <see cref="ImDrawVertex.OtherPosition"/>, since core-profile GL drivers commonly clamp
/// <c>glLineWidth</c> to 1.0.
/// </remarks>
public class ImDraw : IDisposable {
  /// <summary>Default pixel width used when a shape doesn't specify one.</summary>
  public const float DefaultWidth = 2f;
  /// <summary>Default segment count for <see cref="Circle"/>.</summary>
  public const int DefaultCircleSegments = 32;

  private static readonly Vector4 AxisRed = new(1f, 0.2f, 0.2f, 1f);
  private static readonly Vector4 AxisGreen = new(0.2f, 1f, 0.2f, 1f);
  private static readonly Vector4 AxisBlue = new(0.2f, 0.2f, 1f, 1f);

  /// <summary>
  /// Depth-tested vertices — the default, for world-space overlays like a brush outline.
  /// <c>internal</c> rather than <c>private</c> only so <c>Tests</c> (see <c>GDK.csproj</c>'s
  /// <c>InternalsVisibleTo</c>) can assert on accumulated shape data without a GL context; the app layer
  /// (a different assembly) never sees this, only <see cref="Render"/>'s output.
  /// </summary>
  internal readonly List<ImDrawVertex> vertices = [];
  /// <summary>
  /// Vertices submitted with <c>alwaysOnTop: true</c>, drawn with depth testing disabled so they stay
  /// visible through occluding geometry (future gizmo handles; not used by the brush cursor, which
  /// should occlude normally).
  /// </summary>
  internal readonly List<ImDrawVertex> alwaysOnTopVertices = [];

  private Vector3 cameraEye;
  private float fieldOfViewYRadians;
  private float viewportHeightPixels;

  private uint shaderProgram;
  private uint vao;
  private uint vbo;
  private uint vboCapacity;
  private bool gpuResourcesReady;

  /// <summary>
  /// Sets the per-frame camera info <see cref="Axis"/>/<see cref="Circle"/>/<see cref="Arrow"/>'s
  /// <c>screenSpaceExtent</c> mode uses to keep a shape a constant pixel size regardless of camera
  /// distance. Called once per frame by <c>OpenRCT3.OpenGL.Renderer</c> before any
  /// <c>IWindow.Render()</c> call — the point at which shapes actually get submitted — using the
  /// standard editor-gizmo distance/field-of-view scale formula (world-units-per-pixel at a given
  /// distance), computed on the CPU from data already available every frame rather than a shader trick.
  /// </summary>
  public void BeginFrame(Vector3 cameraEye, float fieldOfViewYRadians, float viewportHeightPixels) {
    this.cameraEye = cameraEye;
    this.fieldOfViewYRadians = fieldOfViewYRadians;
    this.viewportHeightPixels = viewportHeightPixels;
  }

  /// <summary>
  /// Appends a screen-space-constant-width line segment from <paramref name="a"/> to <paramref name="b"/>.
  /// </summary>
  /// <param name="alwaysOnTop">
  /// If true, drawn with depth testing disabled, staying visible through occluding geometry. Defaults to
  /// false so a brush outline drawn on sloped/occluded terrain still occludes correctly.
  /// </param>
  public void Line(Vector3 a, Vector3 b, Vector4 color, float width = DefaultWidth, bool alwaysOnTop = false) {
    var target = alwaysOnTop ? alwaysOnTopVertices : vertices;

    // Two triangles forming a quad, expanded perpendicular to the line by the vertex shader. Emitted as
    // six non-indexed vertices (no EBO) since winding/culling never matters for this draw path — face
    // culling is never enabled by the renderer.
    var near = new ImDrawVertex { Position = a, OtherPosition = b, Side = -1f, Width = width, Color = color };
    var nearOpposite = new ImDrawVertex { Position = a, OtherPosition = b, Side = 1f, Width = width, Color = color };
    var far = new ImDrawVertex { Position = b, OtherPosition = a, Side = -1f, Width = width, Color = color };
    var farOpposite = new ImDrawVertex { Position = b, OtherPosition = a, Side = 1f, Width = width, Color = color };

    target.Add(near);
    target.Add(far);
    target.Add(nearOpposite);
    target.Add(nearOpposite);
    target.Add(far);
    target.Add(farOpposite);
  }

  /// <summary>
  /// The Blender-style single-vertex XYZ marker: three <see cref="Line"/> calls from
  /// <paramref name="origin"/> along the rotated +X/+Y/+Z basis vectors, colored red/green/blue
  /// respectively (fixed axis colors, not caller-supplied, so every call site reads unambiguously as
  /// X/Y/Z).
  /// </summary>
  /// <param name="screenSpaceExtent">
  /// If true, <paramref name="size"/> is treated as a pixel size and scaled to world units using the
  /// current frame's camera distance (see <see cref="BeginFrame"/>) so the marker stays a constant
  /// on-screen size; if false (default), <paramref name="size"/> is world units and shrinks/grows with
  /// perspective like the brush-footprint cursor should.
  /// </param>
  public void Axis(
    Vector3 origin,
    Quaternion rotation,
    float size,
    bool screenSpaceExtent = false,
    float width = DefaultWidth,
    bool alwaysOnTop = false) {
    if (screenSpaceExtent) size = WorldSizeForPixels(origin, size);

    var x = Vector3.Transform(Vector3.UnitX, rotation) * size;
    var y = Vector3.Transform(Vector3.UnitY, rotation) * size;
    var z = Vector3.Transform(Vector3.UnitZ, rotation) * size;

    Line(origin, origin + x, AxisRed, width, alwaysOnTop);
    Line(origin, origin + y, AxisGreen, width, alwaysOnTop);
    Line(origin, origin + z, AxisBlue, width, alwaysOnTop);
  }

  /// <summary>
  /// A closed ring of <paramref name="segments"/> <see cref="Line"/> calls in the plane perpendicular to
  /// <paramref name="normal"/>.
  /// </summary>
  /// <param name="screenSpaceExtent">See <see cref="Axis"/>'s parameter of the same name.</param>
  public void Circle(
    Vector3 center,
    Vector3 normal,
    float radius,
    Vector4 color,
    int segments = DefaultCircleSegments,
    bool screenSpaceExtent = false,
    float width = DefaultWidth,
    bool alwaysOnTop = false) {
    if (segments < 3) throw new ArgumentOutOfRangeException(nameof(segments), "A circle needs at least 3 segments.");
    if (screenSpaceExtent) radius = WorldSizeForPixels(center, radius);

    var n = Vector3.Normalize(normal);
    // Any vector not parallel to `n` gives a stable starting basis for the ring via cross products.
    var seed = MathF.Abs(Vector3.Dot(n, Vector3.UnitZ)) < 0.99f ? Vector3.UnitZ : Vector3.UnitX;
    var tangent = Vector3.Normalize(Vector3.Cross(n, seed));
    var bitangent = Vector3.Cross(n, tangent);

    Vector3 PointAt(int i) {
      var theta = i / (float)segments * MathF.Tau;
      return center + ((tangent * MathF.Cos(theta)) + (bitangent * MathF.Sin(theta))) * radius;
    }

    var previous = PointAt(0);
    for (var i = 1; i <= segments; i++) {
      var next = PointAt(i);
      Line(previous, next, color, width, alwaysOnTop);
      previous = next;
    }
  }

  /// <summary>
  /// A <see cref="Line"/> shaft plus a 4-line pyramid head at <paramref name="to"/>, oriented along
  /// <c>to - from</c>.
  /// </summary>
  /// <param name="screenSpaceExtent">
  /// Unlike <see cref="Axis"/>/<see cref="Circle"/>, only scales <paramref name="headSize"/> — the shaft
  /// spans the caller-supplied <paramref name="from"/>/<paramref name="to"/> points as given, since
  /// (unlike a marker with a single origin/size) an arrow's length is usually meaningful on its own
  /// (e.g. a route direction). A future gizmo needing the whole arrow length screen-space-constant would
  /// scale <paramref name="to"/> itself before calling this, not something this parameter covers.
  /// </param>
  public void Arrow(
    Vector3 from,
    Vector3 to,
    Vector4 color,
    float headSize = 0.2f,
    bool screenSpaceExtent = false,
    float width = DefaultWidth,
    bool alwaysOnTop = false) {
    Line(from, to, color, width, alwaysOnTop);

    var direction = to - from;
    var length = direction.Length();
    if (length < 1e-5f) return;
    direction /= length;

    if (screenSpaceExtent) headSize = WorldSizeForPixels(to, headSize);

    var seed = MathF.Abs(Vector3.Dot(direction, Vector3.UnitZ)) < 0.99f ? Vector3.UnitZ : Vector3.UnitX;
    var tangent = Vector3.Normalize(Vector3.Cross(direction, seed));
    var bitangent = Vector3.Cross(direction, tangent);

    var baseCenter = to - (direction * headSize);
    var halfSize = headSize * 0.5f;
    var p0 = baseCenter + (tangent * halfSize);
    var p1 = baseCenter - (tangent * halfSize);
    var p2 = baseCenter + (bitangent * halfSize);
    var p3 = baseCenter - (bitangent * halfSize);

    Line(p0, to, color, width, alwaysOnTop);
    Line(p1, to, color, width, alwaysOnTop);
    Line(p2, to, color, width, alwaysOnTop);
    Line(p3, to, color, width, alwaysOnTop);
  }

  /// <summary>
  /// Converts a desired pixel size at <paramref name="origin"/>'s distance from the current frame's
  /// camera (see <see cref="BeginFrame"/>) into world units, using the standard editor-gizmo
  /// distance/field-of-view formula. Falls back to treating <paramref name="pixels"/> as world units if
  /// <see cref="BeginFrame"/> was never called (e.g. a unit test submitting shapes with no renderer).
  /// <c>internal</c> rather than <c>private</c> so <c>Tests</c> can exercise the formula directly.
  /// </summary>
  internal float WorldSizeForPixels(Vector3 origin, float pixels) {
    if (viewportHeightPixels <= 0f) return pixels;

    var distance = Vector3.Distance(cameraEye, origin);
    var worldPerPixel = 2f * distance * MathF.Tan(fieldOfViewYRadians * 0.5f) / viewportHeightPixels;
    return pixels * worldPerPixel;
  }

  /// <summary>Clears accumulated vertices, ready for the next frame's shapes.</summary>
  public void Clear() {
    vertices.Clear();
    alwaysOnTopVertices.Clear();
  }

  #region GPU resources

  // Matches the Flat/Textured materials' #version 410 core, no attribute/varying/gl_FragColor
  // convention (see Materials/Material.cs's comment for why: core-profile GL removed those).
  private const string VertexSource = @"#version 410 core
in vec3 a_Position;
in vec3 a_OtherPosition;
in float a_Side;
in float a_Width;
in vec4 a_Color;

uniform mat4 u_ViewProj;
uniform vec2 u_ViewportSize;

out vec4 v_Color;

void main() {
    vec4 clip0 = u_ViewProj * vec4(a_Position, 1.0);
    vec4 clip1 = u_ViewProj * vec4(a_OtherPosition, 1.0);

    vec2 screen0 = u_ViewportSize * (0.5 * clip0.xy / clip0.w + 0.5);
    vec2 screen1 = u_ViewportSize * (0.5 * clip1.xy / clip1.w + 0.5);

    vec2 dir = screen0 - screen1;
    float len = length(dir);
    // Degenerate (near-view-parallel) segments collapse to a zero-width quad for this instant rather
    // than rendering a visible artifact from an unstable perpendicular direction.
    vec2 normal = len > 1e-5 ? vec2(-dir.y, dir.x) / len : vec2(0.0);

    vec2 offsetPixels = normal * (a_Width * 0.5) * a_Side;
    // Undo the perspective divide (multiply by clip0.w) so the pixel offset survives it correctly.
    vec2 offsetClip = offsetPixels / u_ViewportSize * 2.0 * clip0.w;

    gl_Position = clip0 + vec4(offsetClip, 0.0, 0.0);
    v_Color = a_Color;
}";

  private const string FragmentSource = @"#version 410 core
in vec4 v_Color;

out vec4 FragColor;

void main() {
    FragColor = v_Color;
}";

  /// <summary>
  /// Uploads and draws this frame's accumulated shapes, then leaves the vertex data in place until the
  /// next <see cref="Clear"/> — call once per frame after every shape for the frame has been submitted.
  /// Lazily compiles the shader and creates the VAO/VBO on first call, matching <c>Mesh</c>'s
  /// upload-on-first-use pattern.
  /// </summary>
  public void Render(Matrix4x4 viewProj, Vector2 viewportSize) {
    if (vertices.Count == 0 && alwaysOnTopVertices.Count == 0) return;

    var gl = IGame.IoC.Resolve<GL>();
    if (!gpuResourcesReady) InitializeGpuResources(gl);

    gl.UseProgram(shaderProgram);
    gl.BindVertexArray(vao);

    gl.UniformMatrix4(
      location: gl.GetUniformLocation(shaderProgram, Camera.UniformName),
      count: 1,
      transpose: false,
      value: viewProj.ToGl().AsSpan()
    );
    gl.Uniform2(gl.GetUniformLocation(shaderProgram, "u_ViewportSize"), viewportSize.X, viewportSize.Y);
    gl.CheckError("Set ImDraw uniforms");

    if (vertices.Count > 0) {
      Upload(gl, vertices);
      gl.DrawArrays(PrimitiveType.Triangles, 0, Convert.ToUInt32(vertices.Count));
      gl.CheckError("Draw ImDraw depth-tested geometry");
    }

    if (alwaysOnTopVertices.Count > 0) {
      Upload(gl, alwaysOnTopVertices);
      gl.Disable(EnableCap.DepthTest);
      gl.DrawArrays(PrimitiveType.Triangles, 0, Convert.ToUInt32(alwaysOnTopVertices.Count));
      gl.CheckError("Draw ImDraw always-on-top geometry");
      gl.Enable(EnableCap.DepthTest);
    }

    gl.BindVertexArray(0);
    gl.UseProgram(0);
  }

  private void InitializeGpuResources(GL gl) {
    var vertexShader = gl.CreateShader(ShaderType.VertexShader);
    gl.ShaderSource(vertexShader, VertexSource);
    gl.CompileShader(vertexShader);
    CheckShaderError(gl, vertexShader);

    var fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
    gl.ShaderSource(fragmentShader, FragmentSource);
    gl.CompileShader(fragmentShader);
    CheckShaderError(gl, fragmentShader);

    shaderProgram = gl.CreateProgram();
    gl.AttachShader(shaderProgram, vertexShader);
    gl.AttachShader(shaderProgram, fragmentShader);
    gl.LinkProgram(shaderProgram);
    CheckProgramError(gl, shaderProgram);

    gl.DeleteShader(vertexShader);
    gl.DeleteShader(fragmentShader);

    vao = gl.GenVertexArray();
    gl.BindVertexArray(vao);
    vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

    var stride = Convert.ToUInt32(Marshal.SizeOf<ImDrawVertex>());
    BindAttribute(gl, "a_Position", 3, 0, stride);
    BindAttribute(gl, "a_OtherPosition", 3, 12, stride);
    BindAttribute(gl, "a_Side", 1, 24, stride);
    BindAttribute(gl, "a_Width", 1, 28, stride);
    BindAttribute(gl, "a_Color", 4, 32, stride);

    gl.BindVertexArray(0);
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

    gpuResourcesReady = true;
  }

  private void BindAttribute(GL gl, string name, int componentCount, int offset, uint stride) {
    var loc = gl.GetAttribLocation(shaderProgram, name);
    Debug.Assert(loc >= 0);
    var location = CastFrom<int>.To<uint>(loc);
    gl.EnableVertexAttribArray(location);
    gl.VertexAttribPointer(location, componentCount, VertexAttribPointerType.Float, normalized: false, stride,
      CastFrom<int>.To<nint>(offset));
    gl.CheckError(string.Format("Binding ImDraw vertex attribute: {0}", name));
  }

  /// <remarks>
  /// Grow-on-demand, never shrink: <see cref="GL.BufferSubData{TData}(BufferTargetARB, nint, nuint, TData[])"/>
  /// within the current capacity when the frame's vertex count fits, only reallocating
  /// (<see cref="GL.BufferData{TData}(BufferTargetARB, nuint, TData[], BufferUsageARB)"/>) to a doubled
  /// capacity when it doesn't. Chosen over per-frame reallocation or buffer-orphaning after surveying
  /// Dear ImGui's OpenGL3 backend, which settled on this shape after orphaning + <c>glBufferSubData</c>
  /// caused NVIDIA multi-viewport glitches — see .agents/plans/features/terrain/debug-draw.md's Research
  /// section.
  /// </remarks>
  private void Upload(GL gl, List<ImDrawVertex> data) {
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

    var vertexSize = Convert.ToUInt32(Marshal.SizeOf<ImDrawVertex>());
    var neededVertices = Convert.ToUInt32(data.Count);
    if (neededVertices > vboCapacity) {
      vboCapacity = Math.Max(neededVertices, Math.Max(vboCapacity * 2, 64));
      gl.BufferData<ImDrawVertex>(
        BufferTargetARB.ArrayBuffer,
        vboCapacity * vertexSize,
        new ImDrawVertex[vboCapacity],
        BufferUsageARB.DynamicDraw);
      gl.CheckError("Growing ImDraw vertex buffer");
    }

    gl.BufferSubData<ImDrawVertex>(BufferTargetARB.ArrayBuffer, 0, neededVertices * vertexSize, data.ToArray());
    gl.CheckError("Uploading ImDraw vertex data");
  }

  private static void CheckShaderError(GL gl, uint shader) {
    var infoLog = gl.GetShaderInfoLog(shader);
    if (!string.IsNullOrEmpty(infoLog)) throw new ShaderError(infoLog);
  }

  private static void CheckProgramError(GL gl, uint program) {
    var infoLog = gl.GetProgramInfoLog(program);
    if (!string.IsNullOrEmpty(infoLog)) throw new ShaderError(infoLog);
  }

  public void Dispose() {
    if (!gpuResourcesReady) return;
    GC.SuppressFinalize(this);

    var gl = IGame.IoC.Resolve<GL>();
    gl.DeleteProgram(shaderProgram);
    gl.DeleteBuffer(vbo);
    gl.DeleteVertexArray(vao);
    gpuResourcesReady = false;
  }

  #endregion
}

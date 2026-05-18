// Renderer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Shaders;
using OpenRCT3.Platforms;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;

namespace OpenRCT3.OpenGL;

public class Renderer(GL gl) : IRenderer {
  private readonly static Logger Logger = LogManager.GetCurrentClassLogger();

  private readonly GL _gl = gl;
  private uint _program;
  private uint _vao;
  private uint _vbo;
  private uint _ebo;
  private uint _texture;
  private bool _initialized;
  private bool _disposed;

  public Color ClearColor { get; set; } = Color.FromArgb(51, 76, 76);
  public event EventHandler? ContextRequested;
  public event EventHandler? Rendered;

  public void Initialize(IGraphicsSurface surface) {
    _gl.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
    _gl.Enable(EnableCap.DepthTest);
  }

  public void Dispose() {
    if (_disposed) return;
    GC.SuppressFinalize(this);

    if (_initialized) {
      _gl.DeleteProgram(_program);
      _gl.DeleteVertexArray(_vao);
      _gl.DeleteBuffer(_vbo);
      _gl.DeleteBuffer(_ebo);
    }
    _disposed = true;
  }

  private void SetupResources(Scene scene) {
    if (_initialized) return;

    // Texture
    if (scene.Model.Material.AlbedoTexture != null) {
      _texture = _gl.GenTexture();
      _gl.BindTexture(TextureTarget.Texture2D, _texture);

      var texture = scene.Model.Material.AlbedoTexture;
      var success = texture.Pixels.DangerousTryGetSinglePixelMemory(out var pixelMemory);
      Debug.Assert(success, "Failed to get pixel memory from albedo texture");
      ReadOnlySpan<Rgba32> pixels = pixelMemory.Span;
      _gl.TexImage2D(
        TextureTarget.Texture2D,
        0,
        InternalFormat.Rgba,
        Convert.ToUInt32(texture.Width),
        Convert.ToUInt32(texture.Height),
        0,
        PixelFormat.Rgba,
        PixelType.UnsignedByte,
        pixels
      );

      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
      _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    // Shader
    var vertexShader = _gl.CreateShader(ShaderType.VertexShader);
    _gl.ShaderSource(vertexShader, scene.Model.Shader.VertexSource);
    _gl.CompileShader(vertexShader);
    CheckShaderError(vertexShader);

    var fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
    _gl.ShaderSource(fragmentShader, scene.Model.Shader.FragmentSource);
    _gl.CompileShader(fragmentShader);
    CheckShaderError(fragmentShader);

    _program = _gl.CreateProgram();
    _gl.AttachShader(_program, vertexShader);
    _gl.AttachShader(_program, fragmentShader);
    _gl.LinkProgram(_program);
    CheckProgramError(_program);

    _gl.DeleteShader(vertexShader);
    _gl.DeleteShader(fragmentShader);

    // Mesh
    _vao = _gl.GenVertexArray();
    _gl.BindVertexArray(_vao);

    _vbo = _gl.GenBuffer();
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    var vertices = scene.Model.Mesh.Vertices.ToArray();
    unsafe {
      fixed (void* v = &vertices[0]) {
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(OpenCobra.GDK.Meshes.Vertex)), v, BufferUsageARB.StaticDraw);
      }
    }

    _ebo = _gl.GenBuffer();
    _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
    var indices = scene.Model.Mesh.Indices.ToArray();
    unsafe {
      fixed (void* i = &indices[0]) {
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
      }
    }

    uint stride;
    unsafe {
      stride = (uint)sizeof(OpenCobra.GDK.Meshes.Vertex);
      // a_Position
      int posLoc = _gl.GetAttribLocation(_program, "a_Position");
      if (posLoc != -1) {
        _gl.EnableVertexAttribArray((uint)posLoc);
        _gl.VertexAttribPointer((uint)posLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
      }
      // a_Normal
      int normLoc = _gl.GetAttribLocation(_program, "a_Normal");
      if (normLoc != -1) {
        _gl.EnableVertexAttribArray((uint)normLoc);
        _gl.VertexAttribPointer((uint)normLoc, 3, VertexAttribPointerType.Float, false, stride, (void*)12);
      }
      // a_TexCoord
      int texLoc = _gl.GetAttribLocation(_program, "a_TexCoord");
      if (texLoc != -1) {
        _gl.EnableVertexAttribArray((uint)texLoc);
        _gl.VertexAttribPointer((uint)texLoc, 2, VertexAttribPointerType.Float, false, stride, (void*)24);
      }
      // a_Color
      int colLoc = _gl.GetAttribLocation(_program, "a_Color");
      if (colLoc != -1) {
        _gl.EnableVertexAttribArray((uint)colLoc);
        _gl.VertexAttribPointer((uint)colLoc, 4, VertexAttribPointerType.Float, false, stride, (void*)32);
      }
    }

    _gl.BindVertexArray(0);
    _initialized = true;
  }

  public void Render(Scene scene) {
    ContextRequested?.Invoke(this, EventArgs.Empty);

    _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

    SetupResources(scene);

    _gl.UseProgram(_program);
    _gl.BindVertexArray(_vao);

    if (scene.Model.Material.AlbedoTexture != null) {
      _gl.ActiveTexture(TextureUnit.Texture0);
      _gl.BindTexture(TextureTarget.Texture2D, _texture);
      int texLoc = _gl.GetUniformLocation(_program, "u_Texture");
      if (texLoc != -1) _gl.Uniform1(texLoc, 0);
    }

    foreach (var uniform in scene.Model.Shader.Uniforms) {
      int location = _gl.GetUniformLocation(_program, uniform.Name);
      if (location == -1) continue;

      if (uniform.Type == OpenCobra.GDK.Shaders.UniformType.Mat4 && uniform.Value is Matrix4x4 matrix) {
        unsafe {
          _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
        }
      }
    }

    unsafe {
      _gl.DrawElements(PrimitiveType.Triangles, (uint)scene.Model.Mesh.Indices.Count, DrawElementsType.UnsignedInt, (void*)0);
    }

    _gl.BindVertexArray(0);
    _gl.UseProgram(0);

    Rendered?.Invoke(this, EventArgs.Empty);
  }

  public void SetViewport(int width, int height) => _gl.Viewport(0, 0, (uint)width, (uint)height);

  private void CheckShaderError(uint shader) {
    string infoLog = _gl.GetShaderInfoLog(shader);
    if (!string.IsNullOrEmpty(infoLog)) {
      Logger.Error($"Shader Error: {infoLog}");
      throw new ShaderError(infoLog);
    }
  }

  private void CheckProgramError(uint program) {
    string infoLog = _gl.GetProgramInfoLog(program);
    if (!string.IsNullOrEmpty(infoLog)) {
      Logger.Error($"Program Error: {infoLog}");
      throw new ShaderError(infoLog);
    }
  }
}

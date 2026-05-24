// Renderer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Services;
using OpenCobra.GDK.Shaders;
using OpenRCT3.Platforms;
using Silk.NET.OpenGL;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Materials = OpenCobra.GDK.Materials;
using Services = OpenCobra.GDK.Services;

namespace OpenRCT3.OpenGL;

public class Renderer : IRenderer {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly GL gl;
  private readonly ConcurrentDictionary<Material, ShaderProgram> shaders = new();

  public State State { get; private set; } = State.Uninitialized;
  public Color ClearColor { get; set; } = Color.FromArgb(45, 45, 48);

  public event EventHandler? ContextRequested;
  public event EventHandler? Rendered;

  public Renderer(GL gl) {
    this.gl = gl;
    Scene.IoC.Register<IGL>(
      Reuse.Singleton,
      Made.Of(() => new Services.GLContext(gl)),
      ifAlreadyRegistered: IfAlreadyRegistered.Replace
    );
  }

  public void Initialize(IGraphicsSurface surface) {
    var clearColor = ClearColor.ToGl();
    gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
    gl.Enable(EnableCap.DepthTest);
    State = State.Ready;
  }

  public void Dispose() {
    if (State == State.Disposed) return;
    GC.SuppressFinalize(this);

    foreach (var program in shaders.Values) gl.DeleteProgram(program.Shader.Handle);
    Scene.IoC.Unregister<IGL>();
    State = State.Disposed;
  }

  public void Render(Scene scene) {
    ContextRequested?.Invoke(this, EventArgs.Empty);

    // Cannot render scene without a camera
    var viewProj = scene.Camera.Value;
    if (!viewProj.HasValue) return;

    // Upload uninitialized models and materials
    var models = scene.UninitializedModels.ToArray();
    if (models.Length > 0) UploadChanges(scene.Camera, models);

    // Render the scene
    gl.Clear(Convert.ToUInt32(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

    foreach (var item in BuildDisplayList(scene)) {
      gl.UseProgram(item.ShaderHandle);
      gl.BindVertexArray(item.Vao);
      gl.BindBuffer(BufferTargetARB.ArrayBuffer, item.Vbo);
      GLError.CheckError(gl, string.Format("Binding {0} vertex buffer", item.Name));

      if (item.TextureHandle != null) {
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, item.TextureHandle.Value);
        var loc = gl.GetUniformLocation(item.ShaderHandle, Materials.Texture.UniformName);
        if (loc != -1) gl.Uniform1(loc, 0);
        GLError.CheckError(gl, string.Format("Binding {0} textures", item.Name));
      }

      // Set model and camera uniforms
      gl.UniformMatrix4(
        gl.GetUniformLocation(item.ShaderHandle, Transform.UniformName),
        count: 1,
        transpose: false,
        item.ModelTransform.ToGl().AsSpan()
      );
      GLError.CheckError(gl, string.Format("Set {0} model transformation uniform", item.Name));
      gl.UniformMatrix4(
        gl.GetUniformLocation(item.ShaderHandle, Camera.UniformName),
        count: 1,
        transpose: false,
        viewProj.Value.ToGl().AsSpan()
      );
      GLError.CheckError(gl, string.Format("Binding {0} camera uniform", item.Name));

      // Draw the model
      gl.DrawElements<uint>(PrimitiveType.Triangles, item.IndexCount, DrawElementsType.UnsignedInt, indices: null);
      GLError.CheckError(gl, string.Format("Draw {0}", item.Name));
    }

    gl.BindVertexArray(0);
    gl.UseProgram(0);

    Rendered?.Invoke(this, EventArgs.Empty);
  }

  public void SetViewport(int width, int height) => gl.Viewport(0, 0, (uint)width, (uint)height);

  private IEnumerable<DrawNode> BuildDisplayList(Scene scene) {
    foreach (var model in scene.Models) {
      var mesh = model.Mesh;
      if (mesh.State != State.Ready) continue;
      if ((model.Material?.State ?? State.Uninitialized) != State.Ready) continue;

      var material = model.Material;
      Debug.Assert(material != null);

      yield return new DrawNode(
        Name: mesh.Name ?? "Mesh",
        Vao: mesh.Vao,
        Vbo: mesh.Vbo,
        TextureHandle: material.AlbedoTexture?.Handle ?? null,
        ShaderHandle: shaders[material].Shader.Handle,
        IndexCount: Convert.ToUInt32(mesh.Indices.Count),
        ModelTransform: model.Transform.Matrix
      );
    }
  }

  private void UploadChanges(Camera camera, IEnumerable<Model> models) {
    foreach (var model in models) {
      Debug.Assert(model.Material != null);
      UploadMaterial(model.Material);

      // Attach model and scene uniforms
      var shaderProgram = shaders[model.Material];
      shaderProgram.Uniforms.Add(model.Transform);
      shaderProgram.Uniforms.Add(camera);
      // Upload mesh data
      Debug.Assert(model.Mesh.State == State.Uninitialized);
      model.Mesh.Upload(shaderProgram.Shader);
    }
  }

  private void UploadMaterial(Material material) {
    // Material textures
    foreach (var texture in material.Textures.Where(t => t.State == State.Uninitialized))
      texture!.Upload();

    // Compile shaders
    var vertexShader = gl.CreateShader(ShaderType.VertexShader);
    gl.ShaderSource(vertexShader, material.Shaders.Vertex);
    gl.CompileShader(vertexShader);
    CheckShaderError(gl, vertexShader);

    var fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
    gl.ShaderSource(fragmentShader, material.Shaders.Fragment);
    gl.CompileShader(fragmentShader);
    CheckShaderError(gl, fragmentShader);

    var program = gl.CreateProgram();
    gl.AttachShader(program, vertexShader);
    gl.AttachShader(program, fragmentShader);
    gl.LinkProgram(program);
    CheckProgramError(gl, program);
    shaders[material] = new(program);

    gl.DeleteShader(vertexShader);
    gl.DeleteShader(fragmentShader);
  }

  private static void CheckShaderError(GL gl, uint shader) {
    string infoLog = gl.GetShaderInfoLog(shader);
    if (!string.IsNullOrEmpty(infoLog)) {
      logger.Error($"Shader Error: {infoLog}");
      throw new ShaderError(infoLog);
    }
  }

  private static void CheckProgramError(GL gl, uint program) {
    string infoLog = gl.GetProgramInfoLog(program);
    if (!string.IsNullOrEmpty(infoLog)) {
      logger.Error($"Shader Program Error: {infoLog}");
      throw new ShaderError(infoLog);
    }
  }
}

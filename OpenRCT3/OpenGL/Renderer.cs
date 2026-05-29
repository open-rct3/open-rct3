// Renderer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using CommunityToolkit.HighPerformance;
using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Platform;
using OpenCobra.GDK.Shaders;
using OpenRCT3.Threading;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GUI = OpenCobra.GDK.GUI;
using Materials = OpenCobra.GDK.Materials;

namespace OpenRCT3.OpenGL;

public class Renderer : ThreadAffine, IRenderer {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly IGLContext context = Scene.IoC.Resolve<IGLContext>();
  private readonly GL gl = Scene.IoC.Resolve<GL>();
  private readonly ConcurrentDictionary<Material, ShaderProgram> shaders = new();
  private readonly Controller gui = Scene.IoC.Resolve<Controller>();

  public State State { get; private set; } = State.Uninitialized;
  public Color ClearColor { get; set; } = Color.FromArgb(45, 45, 48);
  public int MsaaSamples { get; } = 0;

  public void Initialize() => Invoke(() => {
    gl.HookupDebugCallback();

    var clearColor = ClearColor.ToGl();
    gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
    gl.Enable(EnableCap.DepthTest);

    State = State.Ready;
  });

  public void Dispose() => Invoke(() => {
    if (State == State.Disposed) return;
    GC.SuppressFinalize(this);

    foreach (var program in shaders.Values) gl.DeleteProgram(program.Shader.Handle);
    State = State.Disposed;
  });

  public void Render(Scene scene) => Invoke(() => {
    if (!Game.IsRunning) return;
    context.MakeCurrent();
    Debug.Assert(context.IsCurrent);

    // Cannot render scene without a camera
    var viewProj = scene.Camera.Value;
    if (!viewProj.HasValue) return;

    // Upload uninitialized models and materials
    var models = scene.UninitializedModels.ToArray();
    if (models.Length > 0) UploadChanges(scene.Camera, models);

    // Render the scene
    gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

    foreach (var item in BuildDisplayList(scene)) {
      gl.UseProgram(item.ShaderHandle);
      gl.BindVertexArray(item.Vao);
      gl.CheckError(string.Format("Binding {0} vertex array object", item.Name));
      gl.BindBuffer(BufferTargetARB.ArrayBuffer, item.Vbo);
      gl.CheckError(string.Format("Binding {0} vertex buffer", item.Name));

      if (item.TextureHandle != null) {
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, item.TextureHandle.Value);
        var loc = gl.GetUniformLocation(item.ShaderHandle, Materials.Texture.UniformName);
        if (loc != -1) gl.Uniform1(loc, 0);
        gl.CheckError(string.Format("Binding {0} textures", item.Name));
      }

      // Set model and camera uniforms
      gl.UniformMatrix4(
        location: gl.GetUniformLocation(item.ShaderHandle, Transform.UniformName),
        count: 1,
        transpose: false,
        value: item.ModelTransform.ToGl().AsSpan()
      );
      gl.CheckError(string.Format("Set {0} model transformation uniform", item.Name));
      gl.UniformMatrix4(
        location: gl.GetUniformLocation(item.ShaderHandle, Camera.UniformName),
        count: 1,
        transpose: false,
        value: viewProj.Value.ToGl().AsSpan()
      );
      gl.CheckError(string.Format("Binding {0} camera uniform", item.Name));

      // Draw the model
      gl.DrawElements<uint>(PrimitiveType.Triangles, item.IndexCount, DrawElementsType.UnsignedInt, indices: null);
      gl.CheckError(string.Format("Draw {0}", item.Name));
    }

    gl.BindVertexArray(0);
    gl.UseProgram(0);

    RenderGui(scene.Windows);

    // FIXME: Apply VSync settings
    // if (Game.Instance!.VSync) gl.SwapInterval(1);
    // else gl.SwapInterval(0);
    context.SwapBuffers();
  });

  public void SetViewport(int width, int height) => Invoke(() => gl.Viewport(0, 0, (uint)width, (uint)height));

  private void RenderGui(List<GUI.IWindow> windows) {
    using var _ = GLState.Push();
    gui.StartFrame();
    foreach (var window in windows) window.Render();
    gui.Render();
  }

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
      // TODO: Use KhrParallelShaderCompile
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

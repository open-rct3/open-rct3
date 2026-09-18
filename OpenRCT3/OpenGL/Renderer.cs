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
using OpenCobra.GDK.Meshes;
using OpenCobra.GDK.Platform;
using OpenCobra.GDK.Shaders;
using OpenCobra.GDK.Threading;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using GUI = OpenCobra.GDK.GUI;
using Materials = OpenCobra.GDK.Materials;

namespace OpenRCT3.OpenGL;

public class Renderer : ThreadAffine, IRenderer {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly IGLContext context = Game.IoC.Resolve<IGLContext>();
  private readonly GL gl = Game.IoC.Resolve<GL>();
  private readonly Controller gui = Game.IoC.Resolve<Controller>();
  private readonly ResourceCache<MaterialCacheKey, ShaderProgram> shaders;
  private readonly ResourceCache<TextureCacheKey, uint> textures;
  private readonly ContextResourceRegistry<Mesh> uploadedMeshes = new();
  private readonly HashSet<Materials.Texture> uploadedTextures = [];
  private bool? appliedVSync;

  public Renderer() {
    shaders = new(program => gl.DeleteProgram(program.Shader.Handle));
    textures = new(gl.DeleteTexture);
  }

  public State State { get; private set; } = State.Uninitialized;
  public Color ClearColor { get; set; } = Color.FromArgb(45, 45, 48);
  public OpenCobra.GDK.Numerics.Size FramebufferSize { get; set; }
  public int MsaaSamples { get; } = 0;

  public void Initialize() => Invoke(() => {
    context.MakeCurrent();
    Debug.Assert(context.IsCurrent);
    gl.HookupDebugCallback();

    var clearColor = ClearColor.ToGl();
    gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
    gl.Enable(EnableCap.DepthTest);
    gl.Viewport(0, 0, FramebufferSize.Width, FramebufferSize.Height);
    gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    context.SwapBuffers();

    State = State.Ready;
  });

  public void Dispose() => Dispose(null, null, null);

  internal void Dispose(
    Action? disposeScene,
    Action? disposeContext,
    Action? disposeGl
  ) => Invoke(() => {
    if (State == State.Disposed && disposeScene == null
        && disposeContext == null && disposeGl == null) return;
    GC.SuppressFinalize(this);

    var releases = new List<Action>();
    if (disposeScene != null) releases.Add(disposeScene);
    var ownsContextResources = State != State.Disposed;
    if (ownsContextResources) {
      releases.Add(() => uploadedMeshes.Reset(mesh => mesh.ResetUpload()));
      releases.Add(shaders.Dispose);
      releases.Add(textures.Dispose);
      releases.Add(() => {
        foreach (var texture in uploadedTextures) texture.ResetUpload();
        uploadedTextures.Clear();
      });
    }
    if (disposeContext != null) releases.Add(disposeContext);
    if (disposeGl != null) releases.Add(disposeGl);
    RendererTeardown.Run(context, releases);
    if (ownsContextResources) State = State.Disposed;
  });

  public void Render(Scene scene) => Invoke(() => {
    if (!CanRender(State)) return;
    context.MakeCurrent();
    Debug.Assert(context.IsCurrent);

    // Cannot render scene without a camera
    var viewProj = scene.Camera.Value;
    if (!viewProj.HasValue) return;

    // Upload uninitialized models and materials
    var models = scene.Models.Where(NeedsUpload).ToArray();
    if (models.Length > 0) UploadChanges(scene.Camera, models);

    // Render the scene
    gl.Viewport(0, 0, FramebufferSize.Width, FramebufferSize.Height);
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

    RenderGui(scene, viewProj.Value);

    var vsync = Game.Instance?.VSync ?? false;
    if (appliedVSync != vsync) {
      context.SwapInterval(vsync ? 1 : 0);
      appliedVSync = vsync;
    }
    context.SwapBuffers();
  });

  internal static bool CanRender(State state) => state == State.Ready;

  /// <remarks>
  /// <see cref="ImDraw"/> shapes (e.g. a terrain tool's brush cursor) are submitted by
  /// <c>IWindow.Render()</c> below, not before it — so <see cref="ImDraw.Render"/> has to run after that
  /// loop, not "before <c>RenderGui</c>" as an earlier draft of this design assumed. Drawing between the
  /// windows loop and <see cref="Controller.Render"/> puts <see cref="ImDraw"/> geometry over the 3D
  /// scene but under ImGui's own composited overlay, which is the intended layering. All of
  /// <see cref="ImDraw"/>'s GPU resource handling (shader, VAO/VBO, draw calls) lives in
  /// <c>OpenCobra.GDK.ImDraw</c> itself, not here — this is just the orchestration point, same division
  /// of labor as <c>Mesh</c> owning its own upload/VAO/VBO.
  /// </remarks>
  private void RenderGui(Scene scene, Matrix4x4 viewProj) {
    using var _ = GLState.Push();
    gl.CheckError("Before GUI render");

    gui.StartFrame();
    gl.CheckError("Begin ImGui frame");

    scene.ImDraw.BeginFrame(scene.Camera.Eye, Camera.FieldOfView, FramebufferSize.Height);
    foreach (var window in scene.Windows) window.Render();
    gl.CheckError("Rendered GUI windows");

    scene.ImDraw.Render(viewProj, new Vector2(FramebufferSize.Width, FramebufferSize.Height));
    scene.ImDraw.Clear();

    gui.Render();
    gl.CheckError("Rendered ImGui");
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
        ShaderHandle: shaders[material.CacheKey].Shader.Handle,
        IndexCount: Convert.ToUInt32(mesh.Indices.Count),
        ModelTransform: model.Transform.Matrix
      );
    }
  }

  private void UploadChanges(Camera camera, IEnumerable<Model> models) {
    foreach (var model in models) {
      Debug.Assert(model.Material != null);
      // TODO: Use KhrParallelShaderCompile
      var shaderProgram = UploadMaterial(model.Material);

      // Attach model and scene uniforms
      if (!shaderProgram.Uniforms.Contains(model.Transform))
        shaderProgram.Uniforms.Add(model.Transform);
      if (!shaderProgram.Uniforms.Contains(camera)) shaderProgram.Uniforms.Add(camera);
      // Upload mesh data
      model.Mesh.Upload(shaderProgram.Shader);
      uploadedMeshes.Track(model.Mesh);
    }
  }

  private bool NeedsUpload(Model model) {
    var material = model.Material;
    if (model.Mesh.State == State.Disposed || material is not { State: not State.Disposed })
      return false;
    if (model.Mesh.State == State.Uninitialized || !shaders.ContainsKey(material.CacheKey))
      return true;
    return material.Textures.Any(texture =>
      texture.State == State.Uninitialized || !textures.ContainsKey(texture.CacheKey));
  }

  private ShaderProgram UploadMaterial(Material material) {
    // Material textures
    foreach (var texture in material.Textures) {
      var handle = textures.GetOrAdd(texture.CacheKey, _ => {
        texture.Upload();
        return texture.Handle;
      });
      texture.EnsureUploaded(() => handle);
      uploadedTextures.Add(texture);
    }

    return shaders.GetOrAdd(material.CacheKey, _ => CompileShaderProgram(material));
  }

  private ShaderProgram CompileShaderProgram(Material material) {
    // Compile shaders
    var vertexShader = gl.CreateShader(ShaderType.VertexShader);
    var fragmentShader = 0u;
    var program = 0u;
    try {
      gl.ShaderSource(vertexShader, material.Shaders.Vertex);
      gl.CompileShader(vertexShader);
      CheckShaderError(gl, vertexShader);

      fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
      gl.ShaderSource(fragmentShader, material.Shaders.Fragment);
      gl.CompileShader(fragmentShader);
      CheckShaderError(gl, fragmentShader);

      program = gl.CreateProgram();
      gl.AttachShader(program, vertexShader);
      gl.AttachShader(program, fragmentShader);
      gl.LinkProgram(program);
      CheckProgramError(gl, program);
      return new(program);
    } catch {
      if (program != 0) gl.DeleteProgram(program);
      throw;
    } finally {
      gl.DeleteShader(vertexShader);
      if (fragmentShader != 0) gl.DeleteShader(fragmentShader);
    }
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

internal sealed class ResourceCache<TKey, TResource>(Action<TResource> release) : IDisposable
  where TKey : notnull {
  private readonly Dictionary<TKey, TResource> resources = [];
  private bool disposed;

  public TResource this[TKey key] {
    get {
      ObjectDisposedException.ThrowIf(disposed, this);
      return resources[key];
    }
  }

  public bool ContainsKey(TKey key) {
    ObjectDisposedException.ThrowIf(disposed, this);
    return resources.ContainsKey(key);
  }

  public TResource GetOrAdd(TKey key, Func<TKey, TResource> create) {
    ObjectDisposedException.ThrowIf(disposed, this);
    if (resources.TryGetValue(key, out var resource)) return resource;

    resource = create(key);
    resources.Add(key, resource);
    return resource;
  }

  public void Dispose() {
    if (disposed) return;
    var ownedResources = resources.Values.ToArray();
    resources.Clear();
    disposed = true;

    var errors = new List<Exception>();
    foreach (var resource in ownedResources) {
      try {
        release(resource);
      } catch (Exception error) {
        errors.Add(error);
      }
    }
    if (errors.Count > 0) throw new AggregateException(errors);
  }
}

internal sealed class ContextResourceRegistry<T> where T : class {
  private readonly HashSet<T> resources = new(ReferenceEqualityComparer.Instance);

  public int Count => resources.Count;

  public void Track(T resource) => resources.Add(resource);

  public void Reset(Action<T> reset) {
    var errors = new List<Exception>();
    foreach (var resource in resources.ToArray()) {
      try {
        reset(resource);
        resources.Remove(resource);
      } catch (Exception error) {
        errors.Add(error);
      }
    }
    if (errors.Count > 0) throw new AggregateException(errors);
  }
}

internal static class RendererTeardown {
  public static void Run(IGLContext context, IEnumerable<Action> releases) {
    context.MakeCurrent();
    if (!context.IsCurrent)
      throw new InvalidOperationException("The renderer's OpenGL context is not current.");

    ResourceReleaser.Run(releases);
  }
}

internal static class ResourceReleaser {
  public static void Run(IEnumerable<Action> releases) {
    var errors = new List<Exception>();
    Collect(releases, errors);
    if (errors.Count > 0) throw new AggregateException(errors);
  }

  public static void Collect(IEnumerable<Action> releases, List<Exception> errors) {
    foreach (var release in releases) {
      try {
        release();
      } catch (Exception error) {
        errors.Add(error);
      }
    }
  }
}

internal sealed class MacSurfaceResourceOwner {
  private readonly OrderedResourceOwners resources = new();

  public void OwnContext(Action release) => resources.Own(50, release);
  public void OwnGl(Action release) => resources.Own(40, release);
  public void OwnInput(Action release) => resources.Own(30, release);
  public void OwnController(Action release) => resources.Own(20, release, true);
  public void OwnRenderer(Action release) => resources.Own(10, release, true);
  public void OwnGame(Action release) => resources.Own(0, release, true);
  public void Dispose(IGLContext context) => resources.Dispose(context);
}

internal sealed class WindowsSurfaceResourceOwner {
  private readonly OrderedResourceOwners resources = new();

  public bool HasPending => resources.HasPending;
  public void OwnContext(Action release) =>
    resources.Own(40, release, retryOnFailure: true);
  public void OwnDeviceContext(Action release) =>
    resources.Own(50, release, retryOnFailure: true);
  public void OwnGl(Action release) => resources.Own(60, release);
  public void OwnInput(Action release) => resources.Own(30, release);
  public void OwnController(Action release) => resources.Own(20, release, true);
  public void OwnRenderer(Action release) =>
    resources.Own(10, release, requiresCurrent: true, retryOnFailure: true);
  public void Dispose(IGLContext context) => resources.Dispose(context);
}

internal sealed class WindowsSurfaceResourceCycle {
  private WindowsSurfaceResourceOwner? current;

  public bool HasPending => current?.HasPending == true;

  public WindowsSurfaceResourceOwner BeginHandle() {
    if (current?.HasPending == true)
      throw new InvalidOperationException("The previous surface handle still owns resources.");
    current = new WindowsSurfaceResourceOwner();
    return current;
  }

  public void EndHandle(IGLContext context) {
    var resources = current;
    if (resources == null) return;
    try {
      resources.Dispose(context);
    } finally {
      if (!resources.HasPending) current = null;
    }
  }
}

internal sealed class OrderedResourceOwners {
  private readonly object lifetimeLock = new();
  private readonly List<OwnedRelease> releases = [];
  private long nextId;
  private bool disposalStarted;

  public bool HasPending {
    get {
      lock (lifetimeLock) return releases.Count > 0;
    }
  }

  public void Own(
    int order,
    Action release,
    bool requiresCurrent = false,
    bool retryOnFailure = false
  ) {
    lock (lifetimeLock) {
      ObjectDisposedException.ThrowIf(disposalStarted, this);
      releases.Add(new(nextId++, order, release, requiresCurrent, retryOnFailure));
    }
  }

  public void Dispose(IGLContext context) {
    OwnedRelease[] ownedReleases;
    lock (lifetimeLock) {
      disposalStarted = true;
      if (releases.Count == 0) return;
      ownedReleases = [.. releases.OrderBy(resource => resource.Order)];
    }

    if (!ownedReleases.Any(resource => resource.RequiresCurrent)) {
      ReleaseOwned(ownedReleases);
      return;
    }

    try {
      context.MakeCurrent();
      if (!context.IsCurrent)
        throw new InvalidOperationException("The renderer's OpenGL context is not current.");
    } catch (Exception contextError) {
      throw new AggregateException(contextError);
    }

    ReleaseOwned(ownedReleases);
  }

  private void ReleaseOwned(IEnumerable<OwnedRelease> ownedReleases) {
    var errors = new List<Exception>();
    foreach (var resource in ownedReleases) {
      if (resource.RetryOnFailure) {
        try {
          resource.Release();
          Transfer([resource]);
        } catch (Exception error) {
          errors.Add(error);
          break;
        }
        continue;
      }

      Transfer([resource]);
      try {
        resource.Release();
      } catch (Exception error) {
        errors.Add(error);
      }
    }
    if (errors.Count > 0) throw new AggregateException(errors);
  }

  private void Transfer(IEnumerable<OwnedRelease> ownedReleases) {
    var ids = ownedReleases.Select(resource => resource.Id).ToHashSet();
    lock (lifetimeLock) releases.RemoveAll(resource => ids.Contains(resource.Id));
  }

  private readonly record struct OwnedRelease(
    long Id,
    int Order,
    Action Release,
    bool RequiresCurrent,
    bool RetryOnFailure
  );
}

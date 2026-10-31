// MainWindow (Linux)
//
// Authors:
//   - Nicolas Vyčas Nery <vycasnicolas@gmail.com>
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.ComponentModel;
using DryIoc;
using NLog;
using OpenCobra.GDK.Platform;
using OpenRCT3.OpenGL;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Windowing = Silk.NET.Windowing;

namespace OpenRCT3.Platforms.Linux;

/// <summary>
/// Linux windows use Silk.NET.Windowing (GLFW backend), giving us X11 / Wayland windowing and an
/// OpenGL context without rolling native bindings.
/// </summary>
/// <remarks>
/// Wraps a Silk.NET window, forwarding the <see cref="Windowing.IView"/> surface to it. GLFW
/// requires that the window and its GL context be driven from a single thread, so
/// <see cref="Start"/> runs the game loop on the calling thread.
/// </remarks>
public sealed class MainWindow : IWindow, IGraphicsSurface, IDisposable {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  private readonly Windowing.IWindow window;
  private readonly SurfaceSettings settings;
  private GL? gl;
  private IInputContext? input;
  private Renderer? renderer;

  public event SurfaceCreated? SurfaceCreated;
  public event SurfaceChanged? SurfaceChanged;

  public MainWindow() : this(null) { }

  public MainWindow(SurfaceSettings? settings) {
    this.settings = settings?.Clone() ?? new SurfaceSettings();

    var options = Windowing.WindowOptions.Default;
    options.Size = new Vector2D<int>(1024, 768);
    options.Title = "OpenRCT3";
    options.VSync = true;
    options.ShouldSwapAutomatically = true;
    options.API = new Windowing.GraphicsAPI(
      Windowing.ContextAPI.OpenGL,
      this.settings.Profile == ContextProfileMask.CompatibilityProfileBit
        ? Windowing.ContextProfile.Compatability
        : Windowing.ContextProfile.Core,
      this.settings.Flags.HasFlag(ContextFlagMask.ForwardCompatibleBit)
        ? Windowing.ContextFlags.ForwardCompatible
        : Windowing.ContextFlags.Default,
      new Windowing.APIVersion(this.settings.Version.Major, this.settings.Version.Minor)
    );

    window = Windowing.Window.Create(options);
    window.Load += OnLoad;
    window.FramebufferResize += OnFramebufferResize;
    window.Closing += OnClosing;

    // This Program manages the game window's lifetime, same as GameWindow does on Windows.
    Game.IoC.RegisterInstance<IWindow>(this, IfAlreadyRegistered.Replace, Setup.With(preventDisposal: true));
  }

  public string Title {
    get => window.Title;
    set => window.Title = value;
  }

  public Dpi Dpi {
    get {
      var fb = window.FramebufferSize;
      var size = window.Size;
      if (size.X == 0 || size.Y == 0) return new Dpi(1f, 1f);
      return new Dpi((float)fb.X / size.X, (float)fb.Y / size.Y);
    }
  }

  /// <summary>
  /// Creates the window and renderer, then runs the game loop until the window closes.
  /// </summary>
  public void Start() {
    // Creates the window and GL context, and raises Load
    Initialize();
    try {
      logger.Trace("Starting game...");
      var game = new Game();
      game.Run();
    } finally {
      Game.Instance?.Dispose();
      renderer?.Dispose();
      renderer = null;
      input?.Dispose();
      input = null;
      gl?.Dispose();
      gl = null;
      Reset();
    }
  }

  public void Dispose() {
    window.Dispose();
    GC.SuppressFinalize(this);
  }

  private void OnLoad() {
    gl = window.CreateOpenGL();
    input = window.CreateInput();
    logger.Info("Created OpenGL context: {ctxSettings}", settings);

    Game.IoC.RegisterInstance<IGraphicsSurface>(this, IfAlreadyRegistered.Replace);
    Game.IoC.RegisterInstance(gl, IfAlreadyRegistered.Replace);
    Game.IoC.RegisterInstance(window.GLContext!, IfAlreadyRegistered.Replace);

    // Initialize the GUI controller first, renderer implementations depend on it
    Game.IoC.RegisterInstance(new OpenCobra.GDK.GUI.Controller(input), IfAlreadyRegistered.Replace);

    renderer = new Renderer { FramebufferSize = FrameBufferSize };
    renderer.Initialize();
    Game.IoC.RegisterInstance<IRenderer>(renderer, IfAlreadyRegistered.Replace);

    SurfaceCreated?.Invoke(this, renderer);
  }

  private void OnFramebufferResize(Vector2D<int> size) {
    if (renderer == null) return;
    renderer.FramebufferSize = new OpenCobra.GDK.Numerics.Size(size.X, size.Y);
    SurfaceChanged?.Invoke(this);
  }

  private void OnClosing() => Game.Instance?.Quit();

  #region IGraphicsSurface Members
  ISurfaceSettings IGraphicsSurface.Settings => settings;

  public bool IsValid => renderer != null && gl != null && window.GLContext != null;

  public OpenCobra.GDK.Numerics.Size FrameBufferSize {
    get {
      var fb = window.FramebufferSize;
      return new OpenCobra.GDK.Numerics.Size(fb.X, fb.Y);
    }
  }

  public float AspectRatio {
    get {
      var fb = window.FramebufferSize;
      return fb.Y == 0 ? 1f : (float)fb.X / fb.Y;
    }
  }
  #endregion

  #region IView Properties
  [Browsable(false)]
  public nint Handle => window.Handle;

  [Browsable(false)]
  public bool IsClosing => window.IsClosing;

  [Browsable(false)]
  public double Time => window.Time;

  [Browsable(false)]
  public Vector2D<int> FramebufferSize => window.FramebufferSize;

  [Browsable(false)]
  public bool IsInitialized => window.IsInitialized;

  [Browsable(false)]
  public bool ShouldSwapAutomatically {
    get => window.ShouldSwapAutomatically;
    set => window.ShouldSwapAutomatically = value;
  }

  [Browsable(false)]
  public bool IsEventDriven {
    get => window.IsEventDriven;
    set => window.IsEventDriven = value;
  }

  [Browsable(false)]
  public bool IsContextControlDisabled {
    get => window.IsContextControlDisabled;
    set => window.IsContextControlDisabled = value;
  }

  [Browsable(false)]
  Vector2D<int> Windowing.IViewProperties.Size => window.Size;

  [Browsable(false)]
  public double FramesPerSecond {
    get => window.FramesPerSecond;
    set => window.FramesPerSecond = value;
  }

  [Browsable(false)]
  public double UpdatesPerSecond {
    get => window.UpdatesPerSecond;
    set => window.UpdatesPerSecond = value;
  }

  [Browsable(false)]
  public Windowing.GraphicsAPI API => window.API;

  [Browsable(false)]
  public bool VSync {
    get => window.VSync;
    set => window.VSync = value;
  }

  [Browsable(false)]
  public Windowing.VideoMode VideoMode => window.VideoMode;

  [Browsable(false)]
  public int? PreferredDepthBufferBits => window.PreferredDepthBufferBits;

  [Browsable(false)]
  public int? PreferredStencilBufferBits => window.PreferredStencilBufferBits;

  [Browsable(false)]
  public Vector4D<int>? PreferredBitDepth => window.PreferredBitDepth;

  [Browsable(false)]
  public int? Samples => window.Samples;

  [Browsable(false)]
  public IGLContext? GLContext => window.GLContext;

  [Browsable(false)]
  public IVkSurface? VkSurface => window.VkSurface;

  [Browsable(false)]
  public INativeWindow? Native => window.Native;
  #endregion

  #region IView Events
  public event Action<Vector2D<int>>? Resize {
    add => window.Resize += value;
    remove => window.Resize -= value;
  }

  public event Action<Vector2D<int>>? FramebufferResize {
    add => window.FramebufferResize += value;
    remove => window.FramebufferResize -= value;
  }

  public event Action? Closing {
    add => window.Closing += value;
    remove => window.Closing -= value;
  }

  public event Action<bool>? FocusChanged {
    add => window.FocusChanged += value;
    remove => window.FocusChanged -= value;
  }

  public event Action? Load {
    add => window.Load += value;
    remove => window.Load -= value;
  }

  public event Action<double>? Update {
    add => window.Update += value;
    remove => window.Update -= value;
  }

  public event Action<double>? Render {
    add => window.Render += value;
    remove => window.Render -= value;
  }
  #endregion

  #region IView Methods
  public void Initialize() => window.Initialize();
  public void Reset() => window.Reset();
  public void Run(Action onFrame) => window.Run(onFrame);
  public void Focus() => window.Focus();
  public void Close() => window.Close();
  public void ContinueEvents() => window.ContinueEvents();
  public void DoEvents() => window.DoEvents();
  public void DoRender() => window.DoRender();
  public void DoUpdate() => window.DoUpdate();
  public Vector2D<int> PointToClient(Vector2D<int> point) => window.PointToClient(point);
  public Vector2D<int> PointToScreen(Vector2D<int> point) => window.PointToScreen(point);
  public Vector2D<int> PointToFramebuffer(Vector2D<int> point) => window.PointToFramebuffer(point);
  public object Invoke(Delegate d, params object[] args) => window.Invoke(d, args);
  #endregion

  #region IInputPlatform Members
  public bool IsApplicable(Windowing.IView view) => view == this || view == window;
  public IInputContext CreateInput(Windowing.IView view) => window.CreateInput();
  #endregion
}

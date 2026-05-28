// MainWindow (Linux)
//
// Authors:
//   - Nicolas Vyčas Nery <vycasnicolas@gmail.com>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using NLog;
using OpenRCT3.OpenGL;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using SilkWindowing = Silk.NET.Windowing;

namespace OpenRCT3.Platforms.Linux;

// Linux MainWindow uses Silk.NET.Windowing (GLFW backend), giving us X11 /
// Wayland windowing and an OpenGL context without rolling native bindings.
// Implements OpenRCT3.Platforms.IWindow and IGraphicsSurface so the rest of
// the engine remains platform-agnostic.
public sealed class MainWindow : IWindow, IGraphicsSurface, IDisposable {
  private readonly static Logger Logger = LogManager.GetCurrentClassLogger();

  private readonly SilkWindowing.IWindow _window;
  private readonly SurfaceSettings _settings;
  private GL? _gl;
  private Renderer? _renderer;
  private readonly List<IObserver<OpenGLSurface>> _observers = new();

  public MainWindow() : this(null) { }

  public MainWindow(SurfaceSettings? settings) {
    _settings = settings?.Clone() ?? new SurfaceSettings();

    var options = SilkWindowing.WindowOptions.Default;
    options.Size = new Vector2D<int>(1024, 768);
    options.Title = "OpenRCT3";
    options.API = new SilkWindowing.GraphicsAPI(
      SilkWindowing.ContextAPI.OpenGL,
      SilkWindowing.ContextProfile.Compatability,
      SilkWindowing.ContextFlags.ForwardCompatible,
      new SilkWindowing.APIVersion(_settings.Version.Major, _settings.Version.Minor)
    );
    options.VSync = true;

    _window = SilkWindowing.Window.Create(options);
    _window.Load += OnLoad;
    _window.Render += OnRender;
    _window.FramebufferResize += OnFramebufferResize;
    _window.Closing += OnClosing;
  }

  // IWindow
  public string Title {
    get => _window.Title;
    set => _window.Title = value;
  }

  public Dpi Dpi {
    get {
      var fb = _window.FramebufferSize;
      var size = _window.Size;
      if (size.X == 0 || size.Y == 0) return new Dpi(1f, 1f);
      return new Dpi((float)fb.X / size.X, (float)fb.Y / size.Y);
    }
  }

  public Size FrameBufferSize {
    get {
      var fb = _window.FramebufferSize;
      return new Size(fb.X, fb.Y);
    }
  }

  public IDisposable Subscribe(IObserver<OpenGLSurface> observer) {
    if (!_observers.Contains(observer)) _observers.Add(observer);
    return new SurfaceSubscription(_observers, observer);
  }

  // IGraphicsSurface
  public IRenderer Renderer => _renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  public SurfaceSettings Settings => _settings;

  public void Run() {
    // Manual loop equivalent to Silk's Run(Action) extension. Keeping it
    // explicit lets us interleave additional hooks (e.g. simulation updates)
    // without depending on a particular Silk.NET overload signature.
    _window.Initialize();
    while (!_window.IsClosing) {
      _window.DoEvents();
      if (!_window.IsClosing) _window.DoUpdate();
      if (!_window.IsClosing) _window.DoRender();
    }
    _window.DoEvents();
    _window.Reset();
  }

  public void Dispose() {
    _window.Dispose();
    GC.SuppressFinalize(this);
  }

  private void OnLoad() {
    _gl = _window.CreateOpenGL();
    _renderer = new Renderer(_gl);
    _renderer.Initialize(this);
    _ = new Game(new WeakReference<IRenderer>(_renderer));

    var fb = _window.FramebufferSize;
    _gl.Viewport(0, 0, (uint)fb.X, (uint)fb.Y);
    _renderer.SetViewport(fb.X, fb.Y);

    var nativeHandle = _window.Native?.Glfw ?? _window.Handle;
    var surface = new OpenGLSurface(nativeHandle, false);
    foreach (var observer in _observers) observer.OnNext(surface);
  }

  private void OnRender(double deltaSeconds) {
    if (_gl == null || _renderer == null || Game.Instance == null) return;

    var fb = _window.FramebufferSize;
    if (fb.Y != 0)
      Game.Instance.Scene.UpdateCamera((float)fb.X / fb.Y);
    _renderer.Render(Game.Instance.Scene);
  }

  private void OnFramebufferResize(Vector2D<int> size) {
    if (_gl == null || _renderer == null) return;
    _gl.Viewport(0, 0, (uint)size.X, (uint)size.Y);
    _renderer.SetViewport(size.X, size.Y);
    if (Game.Instance != null && size.Y != 0)
      Game.Instance.Scene.UpdateCamera((float)size.X / size.Y);
  }

  private void OnClosing() {
    try {
      Game.Instance?.Dispose();
      _renderer?.Dispose();
      _renderer = null;
      _gl?.Dispose();
      _gl = null;
    } catch (Exception e) {
      Logger.Error(e, "Error during window shutdown.");
    }
  }
}

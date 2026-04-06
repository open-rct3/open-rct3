using Silk.NET.Core.Loader;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenRCT3.OpenGL;
using static OpenRCT3.Platforms.Windows.Win32;

namespace OpenRCT3.Platforms.Windows;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
public class GLSurface : Control, IWindow, IGraphicsSurface {
  private const string OpenGLCreateContextError = "Could not create an OpenGL context.";

  private readonly SurfaceSettings _settings;
  private nint hdc = 0;
  private nint ctx = 0;
  private GL? gl;
  private Renderer? _renderer;
  private readonly HashSet<IObserver<OpenGLSurface>> _observers = new();

  public GLSurface() : this(null) { }

  public GLSurface(SurfaceSettings? settings) {
    SetStyle(ControlStyles.Opaque, true);
    SetStyle(ControlStyles.UserPaint, true);
    SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    DoubleBuffered = false;

    _settings = settings?.Clone() ?? new SurfaceSettings();
  }

  [Category("OpenGL")]
  public GraphicsAPI API {
    get => _settings.API;
    set => _settings.API = value;
  }

  [Category("OpenGL")]
  public ContextProfileMask Profile {
    get => _settings.Profile;
    set => _settings.Profile = value;
  }

  [Category("OpenGL")]
  public ContextFlagMask Flags {
    get => _settings.Flags;
    set => _settings.Flags = value;
  }

  [Category("OpenGL")]
  public Version APIVersion {
    get => _settings.Version;
    set => _settings.Version = value;
  }

  [Browsable(false)]
  public IRenderer Renderer => _renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  [Browsable(false)]
  public SurfaceSettings Settings => _settings;

  [Browsable(false)]
  public bool HasValidContext => IsHandleCreated && ctx != 0 && gl != null;

  [Browsable(false)]
  public GL GL => gl ?? throw new Exception("Current OpenGL context is invalid!");

  // IWindow
  [Category("Behavior")]
  public string Title { get => base.Text; set => Text = value; }

  public Dpi Dpi {
    get {
      using var g = CreateGraphics();
      return new Dpi(g.DpiX / 96f, g.DpiY / 96f);
    }
  }

  public Size FrameBufferSize => ClientSize;

  public IDisposable Subscribe(IObserver<OpenGLSurface> observer) {
    _observers.Add(observer);
    return new Unsubscriber<OpenGLSurface>(_observers, observer);
  }

  protected override CreateParams CreateParams {
    get {
      const int CS_VREDRAW = 0x1;
      const int CS_HREDRAW = 0x2;
      const int CS_OWNDC = 0x20;
      var cp = base.CreateParams;
      cp.ClassStyle |= CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
      return cp;
    }
  }

  public void MakeCurrent() {
    if (DesignMode || !HasValidContext) return;
    if (hdc == 0) throw new Exception("Could not make the GL context current.");
    if (!wglMakeCurrent(hdc, ctx)) throw new Exception("Could not make the GL context current.");
  }

  public void SwapBuffers() {
    if (DesignMode || !HasValidContext) return;
    if (hdc == 0) throw new Exception("Could not swap graphics buffers.");

    Win32.SwapBuffers(hdc);
  }

  protected override void OnHandleCreated(EventArgs e) {
    if (DesignMode) return;

    // Apply necessary window clipping styles for OpenGL rendering
    // See https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles
    var styles = (WindowStyles) Convert.ToUInt32(GetWindowLongPtr(Handle, WindowLongs.GWL_STYLE));
    styles |= WindowStyles.WS_CLIPSIBLINGS | WindowStyles.WS_CLIPCHILDREN;
    SetWindowLongPtr(Handle, WindowLongs.GWL_STYLE, (IntPtr)styles);

    // Try to create an OpenGL context
    hdc = GetDC(Handle);
    var pfd = new PIXELFORMATDESCRIPTOR {
      nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
      nVersion = 1,
      dwFlags = 0x00000004 | 0x00000020, // PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL
      iPixelType = 0, // PFD_TYPE_RGBA
      cColorBits = 32,
      cDepthBits = 24,
      cStencilBits = 8,
      iLayerType = 0 // PFD_MAIN_PLANE
    };
    int pix = ChoosePixelFormat(hdc, ref pfd);
    if (pix == 0) throw new Exception("Could not choose an appropriate pixel format for OpenGL.");
    if (!SetPixelFormat(hdc, pix, ref pfd)) throw new Exception("Could not set the surface's pixel format.");
    if (hdc == 0) throw new InvalidOperationException("Surface HDC context is invalid!");
    ctx = wglCreateContext(hdc);
    if (ctx == 0) throw new Exception(OpenGLCreateContextError);
    MakeCurrent();

    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi(proc => OpenGLProcResolver.GetProc(proc, _settings.Version)) ?? throw new Exception(OpenGLCreateContextError);

    // Start the game
    _renderer = new Renderer(gl);
    _renderer.Initialize(this);
    _ = new Game(new WeakReference<IRenderer>(_renderer));

    base.OnHandleCreated(e);

    MakeCurrent();
    GL.Viewport(0, 0, (uint)ClientSize.Width, (uint)ClientSize.Height);
    _renderer?.SetViewport(ClientSize.Width, ClientSize.Height);
    Invalidate();
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    base.OnHandleDestroyed(e);
    if (DesignMode) return;

    GetDC(Handle);
    if (HasValidContext) {
      Game.Instance?.Dispose();
      _renderer?.Dispose();
      _renderer = null;
      gl?.Dispose();
      gl = null;
      if (hdc != 0) _ = ReleaseDC(Handle, hdc);
    }
  }

  protected override void OnResize(EventArgs e) {
    if (DesignMode || !HasValidContext) return;

    MakeCurrent();
    GL.Viewport(0, 0, (uint)ClientSize.Width, (uint)ClientSize.Height);
    _renderer?.SetViewport(ClientSize.Width, ClientSize.Height);
    Game.Instance?.Scene.UpdateCamera((float)ClientSize.Width / ClientSize.Height);
    Invalidate();

    base.OnResize(e);
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (DesignMode) {
      e.Graphics.Clear(Color.FromArgb(45, 45, 48));
      using var brush = new SolidBrush(Color.FromArgb(200, 200, 200));
      using var font = new Font("Segoe UI", 9);
      e.Graphics.DrawString($"[{GetType().Name}]", font, brush, 8, 8);
    } else {
      OnRenderFrame();
    }

    base.OnPaint(e);
  }

  private void OnRenderFrame() {
    if (!HasValidContext) return;

    // TODO: Extract the rest of this method to prevent duplication between macOS and Windows
    MakeCurrent();
    if (Game.Instance != null && _renderer != null) {
      Game.Instance.Scene.UpdateCamera(ClientSize.Width * 1f / ClientSize.Height);
      _renderer.Render(Game.Instance.Scene);
    }
    SwapBuffers();
  }
}

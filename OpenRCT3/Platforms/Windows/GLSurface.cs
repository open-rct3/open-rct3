// Windows OpenGL Surface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NLog;
using OpenRCT3.OpenGL;
using Silk.NET.OpenGL;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static OpenRCT3.Platforms.Windows.Win32;

namespace OpenRCT3.Platforms.Windows;

public class GLSurface : Control, IWindow, IGraphicsSurface {
  private const string OpenGLCreateContextError = "Could not create an OpenGL context.";

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly SurfaceSettings settings;
  private nint hdc = 0;
  private nint ctx = 0;
  private OpenGLSurface? surface;
  private GLContext? context;
  private GL? gl;
  private Renderer? renderer;

  public event SurfaceChanged? SurfaceChanged;

  public GLSurface() : this(null) { }

  public GLSurface(SurfaceSettings? settings) {
    SetStyle(ControlStyles.Opaque, true);
    SetStyle(ControlStyles.UserPaint, true);
    SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    DoubleBuffered = false;

    this.settings = settings?.Clone() ?? new SurfaceSettings();
  }

  [Category("OpenGL")]
  public GraphicsAPI API {
    get => settings.API;
    set => settings.API = value;
  }

  [Category("OpenGL")]
  public ContextProfileMask Profile {
    get => settings.Profile;
    set => settings.Profile = value;
  }

  [Category("OpenGL")]
  public ContextFlagMask Flags {
    get => settings.Flags;
    set => settings.Flags = value;
  }

  [Category("OpenGL")]
  public Version APIVersion {
    get => settings.Version;
    set => settings.Version = value;
  }

  [Browsable(false)]
  public IRenderer Renderer => renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  [Browsable(false)]
  public SurfaceSettings Settings => settings;

  [Browsable(false)]
  public bool HasValidContext => IsHandleCreated && ctx != 0 && gl != null;

  [Browsable(false)]
  public GL GL => gl ?? throw new InvalidOperationException("Current OpenGL context is invalid!");

  [Browsable(false)]
  public OpenGLSurface Surface => surface ?? throw new InvalidOperationException("Current surface is invalid!");

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
    // FIXME: The surface settings are not taken into account
    context = new GLContext(settings);
    gl = GL.GetApi(proc => context.GetProcAddress(proc))
      ?? throw new Exception(OpenGLCreateContextError);
    logger.Info("Created OpenGL context: {ctxSettings}", settings);
    surface = new(Handle, ownsHandle: false);
    SurfaceChanged?.Invoke(surface);

    // Start the game
    renderer = new Renderer(gl);
    renderer.Initialize(this);
    _ = new Game(renderer);

    base.OnHandleCreated(e);

    MakeCurrent();
    renderer?.SetViewport(ClientSize.Width, ClientSize.Height);
    Invalidate();
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    base.OnHandleDestroyed(e);
    if (DesignMode) return;

    GetDC(Handle);
    if (!HasValidContext) return;

    Game.Instance?.Dispose();
    logger.Trace("Game instance disposed");
    renderer?.Dispose();
    renderer = null;
    logger.Trace("Renderer disposed");
    gl?.Dispose();
    gl = null;
    if (hdc != 0) _ = ReleaseDC(Handle, hdc);
    logger.Trace("Surface disposed");
  }

  protected override void OnResize(EventArgs e) {
    if (DesignMode || !HasValidContext) return;

    MakeCurrent();
    renderer?.SetViewport(ClientSize.Width, ClientSize.Height);
    SurfaceChanged?.Invoke(surface!);
    Game.Instance?.Scene.Update(Convert.ToSingle(ClientSize.Width / ClientSize.Height));
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

    // TODO: Extract the rest of this method to prevent duplication between platforms
    MakeCurrent();
    if (Game.Instance != null && renderer != null) {
      var scene = Game.Instance.Scene;
      scene.Update(ClientSize.Width * 1f / ClientSize.Height);
      renderer.Render(scene);
    }
    SwapBuffers();
  }
}

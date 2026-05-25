// Windows OpenGL Surface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NLog;
using OpenRCT3.OpenGL;
using Silk.NET.OpenGL;
using Silk.NET.WGL;
using Silk.NET.WGL.Extensions.ARB;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static OpenRCT3.Platforms.Windows.Win32;

namespace OpenRCT3.Platforms.Windows;

public class GLSurface : Control, IGraphicsSurface {
  private const string OpenGLCreateContextError =
    "Could not create an OpenGL context. Please upgrade your graphics drivers.";

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly SurfaceSettings settings;
  private nint hdc = 0;
  private nint context = 0;
  private OpenGLSurface? surface;
  private readonly WGL wgl;
  private GL? gl;
  private Renderer? renderer;

  public event SurfaceCreated? SurfaceCreated;
  public event SurfaceChanged? SurfaceChanged;

  public GLSurface() : this(null) { }

  public GLSurface(SurfaceSettings? settings) {
    SetStyle(ControlStyles.Opaque, true);
    SetStyle(ControlStyles.UserPaint, true);
    SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    DoubleBuffered = false;

    this.settings = settings?.Clone() ?? new SurfaceSettings();
    wgl = new WGL(new GLContext(settings!));
  }

  [Browsable(false)]
  public IRenderer Renderer => renderer
    ?? throw new InvalidOperationException("Renderer has not been initialized.");

  [Browsable(false)]
  public SurfaceSettings Settings => settings;

  [Browsable(false)]
  public bool IsValid => IsHandleCreated && context != nint.Zero && gl != null;

  [Browsable(false)]
  public Handle<nint> Surface => surface ?? throw new InvalidOperationException("Current surface is invalid!");

  [Browsable(false)]
  public GL GL => gl ?? throw new InvalidOperationException("Current OpenGL context is invalid!");

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

  // FIXME: This doesn't take the pixel density into account
  public Size FrameBufferSize => ClientSize;

  public float AspectRatio => (float)ClientSize.Width / (float)ClientSize.Height;

  public void MakeCurrent() {
    if (DesignMode || !IsValid) return;
    if (hdc == 0) throw new Exception("Could not make the GL context current.");
    if (!wgl.MakeCurrent(hdc, context)) throw new Exception("Could not make the GL context current.");
  }

  public void SwapBuffers() {
    if (DesignMode || !IsValid) return;
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

    // Try to create an appropriate pixel format
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
    var pix = ChoosePixelFormat(hdc, ref pfd);
    if (pix == nint.Zero) throw new Exception("Could not choose an appropriate pixel format for OpenGL.");
    if (!SetPixelFormat(hdc, pix, ref pfd)) throw new Exception("Could not set the surface's pixel format.");
    if (hdc == nint.Zero) throw new InvalidOperationException("Surface HDC context is invalid!");
    // Create a staging OpenGL context
    var tempContext = context = wgl.CreateContext(hdc);
    if (tempContext == nint.Zero) throw new Exception(OpenGLCreateContextError);
    wgl.MakeCurrent(hdc, tempContext);

    // Create a customized OpenGL context
    if (wgl.TryGetExtension<ArbCreateContext>(out var ext) == false)
      throw new PlatformNotSupportedException("OpenGL wglCreateContextAttribsARB extension is unavilable.");
    var arbCreateContext = ext ?? throw new Exception(OpenGLCreateContextError);
    context = arbCreateContext.CreateContextAttrib(hdc, nint.Zero, [
      (int)ContextAttribute.MajorVersion, settings.Version.Major,
      (int)ContextAttribute.MinorVersion, settings.Version.Minor,
      (int)ContextAttribute.ProfileMask, (int)settings.Profile,
      (int)ContextAttribute.Flags,
      (int)(
        settings.Flags |
#if DEBUG
        // Request a debugging context
        ContextFlagMask.DebugBit
#endif
      ),
      0 // NULL terminator
    ]);
    if (context == nint.Zero) context = wgl.CreateContext(hdc);
    if (context == nint.Zero) throw new Exception(OpenGLCreateContextError);
    // Cleanup temporary context
    wgl.MakeCurrent(hdc, 0);
    wgl.DeleteContext(tempContext);
    wgl.MakeCurrent(hdc, context);

    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi((wgl.Context as GLContext)!.GetProcAddress) ?? throw new Exception(OpenGLCreateContextError);
    logger.Info("Created OpenGL context: {ctxSettings}", settings);
    surface = new(Handle, ownsHandle: false);

    // Create the scene renderer
    renderer = new Renderer(this, gl);
    renderer.ContextRequested += (_, _) => {
      if (!IsValid) return;
      Invoke(() => MakeCurrent());
    };
    renderer.Rendered += (_, _) => {
      if (!IsValid) return;
      Invoke(() => SwapBuffers());
    };

    // Initialize the scene renderer
    MakeCurrent();
    renderer.Initialize(this);
    renderer.SetViewport(ClientSize.Width, ClientSize.Height);
    SurfaceCreated?.Invoke(this, renderer);

    base.OnHandleCreated(e);
    Invalidate();
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    base.OnHandleDestroyed(e);
    if (DesignMode) return;

    hdc = GetDC(Handle);
    if (!IsValid) return;

    Game.Instance?.Dispose();
    logger.Trace("Game instance disposed");
    renderer?.Dispose();
    renderer = null;
    logger.Trace("Renderer disposed");
    gl?.Dispose();
    gl = null;
    if (context != nint.Zero) wgl.DeleteContext(context);
    context = nint.Zero;
    logger.Trace("Context disposed");
    if (hdc != 0) _ = ReleaseDC(Handle, hdc);
    hdc = nint.Zero;
    logger.Trace("Surface disposed");
  }

  protected override void OnResize(EventArgs e) {
    if (DesignMode || !IsValid) return;

    MakeCurrent();
    renderer?.SetViewport(ClientSize.Width, ClientSize.Height);
    SurfaceChanged?.Invoke(this);
    Game.Instance?.Scene.Update(AspectRatio);
    Invalidate();

    base.OnResize(e);
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (DesignMode) {
      e.Graphics.Clear(Color.FromArgb(45, 45, 48));
      using var brush = new SolidBrush(Color.FromArgb(200, 200, 200));
      using var font = new Font("Segoe UI", 9);
      e.Graphics.DrawString($"[{GetType().Name}]", font, brush, 8, 8);
      return;
    }

    base.OnPaint(e);

    // Render the scene
    // TODO: Extract the rest of this method to prevent duplication between platforms
    if (!IsValid) return;
    if (Game.Instance != null && renderer != null) {
      var scene = Game.Instance.Scene;
      scene.Update(AspectRatio);
      renderer.Render(scene);
    } else {
      MakeCurrent();
      gl?.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
      SwapBuffers();
    }
  }
}

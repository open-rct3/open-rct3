// Windows OpenGL Surface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using GUI = OpenCobra.GDK.GUI;
using OpenCobra.GDK.Numerics;
using OpenCobra.GDK.Platform;
using OpenRCT3.OpenGL;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.ComponentModel;
using System.Windows.Forms;
using static OpenRCT3.Platforms.Windows.Win32;
using Drawing = System.Drawing;

namespace OpenRCT3.Platforms.Windows;

public class GLSurface : Control, IGraphicsSurface, IGLContextSource {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly SurfaceSettings settings;
  private GL? gl;
  private Renderer? renderer;

  /// <inheritdoc/>
  /// <remarks>
  /// It is safe to start the game only <i>after</i> this event.
  /// </remarks>
  public event SurfaceCreated? SurfaceCreated;
  public event SurfaceChanged? SurfaceChanged;

  public GLSurface() : this(new SurfaceSettings()) { }

  public GLSurface(SurfaceSettings? settings) {
    SetStyle(ControlStyles.Opaque, true);
    SetStyle(ControlStyles.UserPaint, true);
    SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    DoubleBuffered = false;

    this.settings = settings?.Clone() ?? new SurfaceSettings();
    Context = new GLContext(settings!);
  }

  [Browsable(false)]
  public readonly GLContext Context;

  public SurfaceSettings Settings => settings;
  ISurfaceSettings IGraphicsSurface.Settings => settings;

  [Browsable(false)]
  public bool IsValid => IsHandleCreated && Context.IsValid && gl != null;

  [Browsable(false)]
  public IGLContext? GLContext => Context;

  // FIXME: This doesn't take the pixel density into account
  public Size FrameBufferSize => new((uint)ClientSize.Width, (uint)ClientSize.Height);

  public float AspectRatio => (float)ClientSize.Width / ClientSize.Height;

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

  protected override void OnHandleCreated(EventArgs e) {
    if (DesignMode) return;

    // Apply necessary window clipping styles for OpenGL rendering
    // See https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles
    var styles = (WindowStyles) Convert.ToUInt32(GetWindowLongPtr(Handle, WindowLongs.GWL_STYLE));
    styles |= WindowStyles.WS_CLIPSIBLINGS | WindowStyles.WS_CLIPCHILDREN;
    SetWindowLongPtr(Handle, WindowLongs.GWL_STYLE, (IntPtr)styles);

    // Try to create an appropriate OpenGL context
    Context.Hdc = GetDC(Handle);

    // Load Silk.NET OpenGL with the current context
    gl = GL.GetApi(Context.GetProcAddress);
    Debug.Assert(gl is not null);
    logger.Info("Created OpenGL context: {ctxSettings}", settings);
    Context.MakeCurrent();

    // TODO: Refactor to extract the rest of this method into the GDK
    // Renderer implementations depend on the GUI controller
    Game.IoC.RegisterInstance<IGraphicsSurface>(this);
    Game.IoC.RegisterInstance(gl);
    Game.IoC.RegisterInstance<IGLContext>(Context);
    Game.IoC.Register<IInputContext>(
      Reuse.ScopedOrSingleton,
      Made.Of(r => ServiceInfo.Of<IWindow>(), window => window.CreateInput(Arg.Of<IWindow>())),
      // The input abstraction is kinda heavy, so let services dispose of it
      Setup.With(trackDisposableTransient: true, allowDisposableTransient: true),
      IfAlreadyRegistered.Throw
    );
    Game.IoC.Register<GUI.Controller>(
      Reuse.ScopedOrSingleton,
      Made.Of(r => ServiceInfo.Of<IInputContext>(), input => new GUI.Controller(input)),
      ifAlreadyRegistered: IfAlreadyRegistered.Throw
    );

    // Initialize the scene renderer
    renderer = new Renderer { FramebufferSize = new(ClientSize.Width, ClientSize.Height) };
    renderer.Initialize();
    Game.IoC.RegisterInstance<IRenderer>(renderer);

    SurfaceCreated?.Invoke(this, renderer);
    base.OnHandleCreated(e);
    Invalidate();
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    base.OnHandleDestroyed(e);
    if (DesignMode) return;

    if (!IsValid) return;

    Game.Instance?.Dispose();
    logger.Trace("Game instance disposed");
    renderer?.Dispose();
    renderer = null;
    logger.Trace("Renderer disposed");
    Context.Dispose();
    if (Context.Hdc != nint.Zero) {
      _ = ReleaseDC(Handle, Context.Hdc);
      Context.Hdc = nint.Zero;
    }
    logger.Trace("Context disposed");
    gl?.Dispose();
    gl = null;
    logger.Trace("Surface disposed");
  }

  protected override void OnResize(EventArgs e) {
    if (DesignMode || !IsValid) return;

    Context.MakeCurrent();
    renderer?.FramebufferSize = new(ClientSize.Width, ClientSize.Height);
    SurfaceChanged?.Invoke(this);

    base.OnResize(e);
    Invalidate();
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (DesignMode) {
      e.Graphics.Clear(Drawing.Color.FromArgb(45, 45, 48));
      using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(200, 200, 200));
      using var font = new Drawing.Font("Segoe UI", 9);
      e.Graphics.DrawString($"[{GetType().Name}]", font, brush, 8, 8);
      return;
    }

    // When you drag or resize a window, Windows enters a modal tracking loop that freezes the game loop.
    // To keep the window from tearing, the OS forcefully injects <c>WM_PAINT</c> messages directly into
    // a window's message procedure.
    //
    // The renderer does not support re-entrancy.
    //
    // Do NOT update the scene while processing Windows events, i.e. `Scene.Update` is banned in WM event handlers.
    base.OnPaint(e);

    // Render the scene
    // TODO: Extract the rest of this method to prevent duplication between platforms
    if (!IsValid) return;
    if (Game.Instance != null && renderer != null) {
      renderer.FramebufferSize = new(ClientSize.Width, ClientSize.Height);
      renderer.Render(Game.Instance.Scene);
    } else {
      Context.MakeCurrent();
      Context.Clear();
      Context.SwapBuffers();
    }
  }
}

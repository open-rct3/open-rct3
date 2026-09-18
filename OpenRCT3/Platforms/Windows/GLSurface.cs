// Windows OpenGL Surface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using OpenCobra.GDK.Numerics;
using OpenCobra.GDK.Platform;
using OpenRCT3.OpenGL;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using static OpenRCT3.Platforms.Windows.Win32;
using Drawing = System.Drawing;
using GUI = OpenCobra.GDK.GUI;
using Controller = OpenCobra.GDK.GUI.Controller;

namespace OpenRCT3.Platforms.Windows;

public class GLSurface : Control, IGraphicsSurface, IGLContextSource {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private readonly SurfaceSettings settings;
  private GL? gl;
  private Renderer? renderer;
  private IInputContext? input;
  private Controller? controller;
  private readonly WindowsSurfaceResourceCycle resources = new();
  private Game? finalGame;
  private bool finalDisposalStarted;

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
    if (resources.HasPending) resources.EndHandle(Context);
    var handleResources = resources.BeginHandle();
    handleResources.OwnContext(Context.ReleaseHandle);

    // Apply necessary window clipping styles for OpenGL rendering
    // See https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles
    var styles = (WindowStyles) Convert.ToUInt32(GetWindowLongPtr(Handle, WindowLongs.GWL_STYLE));
    styles |= WindowStyles.WS_CLIPSIBLINGS | WindowStyles.WS_CLIPCHILDREN;
    SetWindowLongPtr(Handle, WindowLongs.GWL_STYLE, (IntPtr)styles);

    // Try to create an appropriate OpenGL context
    var hwnd = Handle;
    var hdc = GetDC(hwnd);
    if (hdc == nint.Zero)
      throw new InvalidOperationException("Could not acquire the surface device context.");
    handleResources.OwnDeviceContext(() => {
      if (ReleaseDC(hwnd, hdc) == 0)
        throw new InvalidOperationException("Could not release the surface device context.");
      Context.Hdc = nint.Zero;
    });
    Context.Hdc = hdc;

    // Load Silk.NET OpenGL with the current context
    var ownedGl = GL.GetApi(Context.GetProcAddress);
    gl = ownedGl;
    handleResources.OwnGl(ownedGl.Dispose);
    Debug.Assert(ownedGl is not null);
    logger.Info("Created OpenGL context: {ctxSettings}", settings);
    Context.MakeCurrent();

    // TODO: Refactor to extract the rest of this method into the GDK
    WindowsSurfaceRegistrations.ReplaceGraphics(Game.IoC, this, ownedGl, Context);

    // Initialize the GUI controller first, renderer implementations depend on it
    var mainWindow = Parent as GameWindow ?? throw new InvalidOperationException();
    var ownedInput = mainWindow.CreateInput();
    input = ownedInput;
    handleResources.OwnInput(ownedInput.Dispose);
    Game.IoC.RegisterInstance<IInputContext>(ownedInput, IfAlreadyRegistered.Replace,
      Setup.With(preventDisposal: true));
    var ownedController = new Controller(ownedInput);
    controller = ownedController;
    handleResources.OwnController(ownedController.Dispose);
    WindowsSurfaceRegistrations.ReplaceController(Game.IoC, ownedController);

    // Initialize the scene renderer
    var ownedRenderer = new Renderer {
      FramebufferSize = new(ClientSize.Width, ClientSize.Height)
    };
    renderer = ownedRenderer;
    handleResources.OwnRenderer(ownedRenderer.Dispose);
    ownedRenderer.Initialize();
    WindowsSurfaceRegistrations.ReplaceRenderer(Game.IoC, ownedRenderer);

    var game = Game.Instance;
    game?.BindInput(ownedInput);
    game?.Scene.BindGui(ownedController);
    game?.BindRenderer(ownedRenderer);
    SurfaceCreated?.Invoke(this, ownedRenderer);
    base.OnHandleCreated(e);
    PresentFrame();
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    var ownedRenderer = renderer;
    var ownedInput = input;
    var ownedController = controller;
    var game = Game.Instance;
    if (ownedRenderer != null) game?.UnbindRenderer(ownedRenderer);
    if (ownedController != null) game?.Scene.UnbindGui(ownedController);
    if (ownedInput != null) game?.UnbindInput(ownedInput);
    try {
      resources.EndHandle(Context);
      logger.Trace("Surface resources disposed");
    } finally {
      renderer = null;
      controller = null;
      input = null;
      gl = null;
      base.OnHandleDestroyed(e);
    }
  }

  protected override void Dispose(bool disposing) {
    if (!disposing) {
      base.Dispose(false);
      return;
    }

    var errors = new List<Exception>();
    if (!finalDisposalStarted) {
      finalDisposalStarted = true;
      finalGame = Game.DetachInstance();
    }

    if (finalGame != null) {
      try {
        // WinForms may destroy the child handle before disposing its component. In that path,
        // renderer teardown already released every context-bound resource.
        if (resources.HasPending || Context.IsValid) {
          Context.MakeCurrent();
          if (!Context.IsCurrent)
            throw new InvalidOperationException("The renderer's OpenGL context is not current.");
        }
        var game = finalGame;
        finalGame = null;
        game.Dispose();
      } catch (Exception error) {
        errors.Add(error);
      }
    }
    if (finalGame != null) throw new AggregateException(errors);

    try {
      resources.EndHandle(Context);
      renderer = null;
      gl = null;
    } catch (Exception error) {
      errors.Add(error);
    }
    if (resources.HasPending) throw new AggregateException(errors);

    try {
      base.Dispose(true);
    } catch (Exception error) {
      errors.Add(error);
    }

    try {
      Context.Dispose();
    } catch (Exception error) {
      errors.Add(error);
    }
    if (errors.Count > 0) throw new AggregateException(errors);
  }

  protected override void OnResize(EventArgs e) {
    if (DesignMode || !IsValid) return;

    Context.MakeCurrent();
    renderer?.FramebufferSize = new(ClientSize.Width, ClientSize.Height);
    SurfaceChanged?.Invoke(this);

    base.OnResize(e);
    Invalidate();
  }

  internal void PresentFrame(Action? prepareFrame = null) {
    if (DesignMode || !IsValid) return;
    WindowsFramePresentation.Present(prepareFrame, Invalidate, Update);
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

internal static class WindowsFramePresentation {
  public static void Present(
    Action? prepareFrame,
    Action invalidate,
    Action update
  ) {
    ArgumentNullException.ThrowIfNull(invalidate);
    ArgumentNullException.ThrowIfNull(update);

    prepareFrame?.Invoke();
    invalidate();
    update();
  }
}

internal static class WindowsSurfaceRegistrations {
  private readonly static Setup ownerManaged = Setup.With(preventDisposal: true);

  public static void ReplaceGraphics(
    DryIoc.Container container,
    IGraphicsSurface surface,
    GL gl,
    IGLContext context
  ) {
    container.RegisterInstance<IGraphicsSurface>(
      surface, IfAlreadyRegistered.Replace, ownerManaged);
    container.RegisterInstance(gl, IfAlreadyRegistered.Replace, ownerManaged);
    container.RegisterInstance<IGLContext>(
      context, IfAlreadyRegistered.Replace, ownerManaged);
  }

  public static void ReplaceController(DryIoc.Container container, Controller controller) =>
    container.RegisterInstance(controller, IfAlreadyRegistered.Replace, ownerManaged);

  public static void ReplaceRenderer(DryIoc.Container container, IRenderer renderer) =>
    container.RegisterInstance<IRenderer>(
      renderer, IfAlreadyRegistered.Replace, ownerManaged);
}

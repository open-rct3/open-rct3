using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ContextFlags = OpenTK.Windowing.Common.ContextFlags;
using GlNativeWindow = OpenTK.Windowing.Desktop.NativeWindow;

namespace OpenRCT3.Platforms.Windows;

public class GLSurface : Control, IWindow {
  private GlNativeWindow? _nativeWindow;
  private GLSurfaceSettings _settings;
  private readonly HashSet<IObserver<OpenGLSurface>> _observers = new();

  public GLSurface() : this(null) { }

  public GLSurface(GLSurfaceSettings? settings) {
    SetStyle(ControlStyles.Opaque, true);
    SetStyle(ControlStyles.UserPaint, true);
    SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    DoubleBuffered = false;
    _settings = settings?.Clone() ?? new GLSurfaceSettings();
  }

  [Category("OpenGL")]
  public ContextAPI API {
    get => _nativeWindow?.API ?? _settings.API;
    set => _settings.API = value;
  }

  [Category("OpenGL")]
  public ContextProfile Profile {
    get => _nativeWindow?.Profile ?? _settings.Profile;
    set => _settings.Profile = value;
  }

  [Category("OpenGL")]
  public ContextFlags Flags {
    get => _nativeWindow?.Flags ?? _settings.Flags;
    set => _settings.Flags = value;
  }

  [Category("OpenGL")]
  public Version APIVersion {
    get => _nativeWindow?.APIVersion ?? _settings.APIVersion;
    set => _settings.APIVersion = value;
  }

  [Browsable(false)]
  public bool HasValidContext => _nativeWindow != null;

  [Browsable(false)]
  public override string Text { get => base.Text; set => base.Text = value; }

  // IWindow
  public string Title { get => Text; set => Text = value; }

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
    if (DesignMode || _nativeWindow == null) return;
    _nativeWindow.MakeCurrent();
  }

  public void SwapBuffers() {
    if (DesignMode || _nativeWindow == null) return;
    _nativeWindow.Context.SwapBuffers();
  }

  protected override void OnHandleCreated(EventArgs e) {
    if (!DesignMode)
      CreateNativeWindow();
    base.OnHandleCreated(e);
  }

  private void CreateNativeWindow() {
    var nativeSettings = new NativeWindowSettings {
      API = _settings.API,
      Profile = _settings.Profile,
      Flags = _settings.Flags,
      APIVersion = _settings.APIVersion,
      ClientSize = new OpenTK.Mathematics.Vector2i(Width, Height),
      StartVisible = false,
    };

    _nativeWindow = new GlNativeWindow(nativeSettings);

    unsafe {
      var hWnd = GLFW.GetWin32Window(_nativeWindow.WindowPtr);
      var style = (IntPtr)(long)(Win32.WindowStyles.WS_CHILD
        | Win32.WindowStyles.WS_DISABLED
        | Win32.WindowStyles.WS_CLIPSIBLINGS
        | Win32.WindowStyles.WS_CLIPCHILDREN);
      Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_STYLE, style);

      var exStyle = (IntPtr)(long)Win32.WindowStylesEx.WS_EX_NOACTIVATE;
      Win32.SetWindowLongPtr(hWnd, Win32.WindowLongs.GWL_EXSTYLE, exStyle);

      Win32.SetParent(hWnd, Handle);
    }

    ResizeNativeWindow();
    _nativeWindow.IsVisible = true;
  }

  private void ResizeNativeWindow() {
    if (_nativeWindow == null || DesignMode) return;
    _nativeWindow.ClientRectangle = new OpenTK.Mathematics.Box2i(0, 0, Width, Height);
  }

  protected override void OnResize(EventArgs e) {
    if (IsHandleCreated) ResizeNativeWindow();
    base.OnResize(e);
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (DesignMode) {
      e.Graphics.Clear(Color.FromArgb(45, 45, 48));
      using var brush = new SolidBrush(Color.FromArgb(200, 200, 200));
      using var font = new Font("Segoe UI", 9);
      e.Graphics.DrawString($"[{GetType().Name}]", font, brush, 8, 8);
    } else this.OnRenderFrame();
    base.OnPaint(e);
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    base.OnHandleDestroyed(e);
    if (_nativeWindow != null) {
      _nativeWindow.Dispose();
      _nativeWindow = null;
    }
  }

  private static readonly object EVENT_RENDERFRAME = new();

  [Category("OpenGL")]
  public event EventHandler? RenderFrame {
    add => Events.AddHandler(EVENT_RENDERFRAME, value);
    remove => Events.RemoveHandler(EVENT_RENDERFRAME, value);
  }

  private void OnRenderFrame() {
    ((EventHandler?)Events[EVENT_RENDERFRAME])?.Invoke(this, EventArgs.Empty);
  }
}

public class GLSurfaceSettings {
  public ContextAPI API { get; set; } = ContextAPI.OpenGL;
  public ContextProfile Profile { get; set; } = ContextProfile.Core;
  public ContextFlags Flags { get; set; } = ContextFlags.ForwardCompatible;
  public Version APIVersion { get; set; } = new(4, 0);

  public GLSurfaceSettings Clone() => (GLSurfaceSettings)MemberwiseClone();
}

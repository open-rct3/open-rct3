// Game Window
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using DryIoc.ImTools;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Platform;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windowing = Silk.NET.Windowing;

namespace OpenRCT3.Platforms.Windows;

internal partial class GameWindow : Form, IWindow {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  private readonly Dictionary<Delegate, EventHandler> handlerMap = [];
  private readonly ManualResetEvent rendererCreated = new(false);
  private readonly Stopwatch stopwatch = new();
  private IRenderer? renderer;
  private bool isClosing = false;
  private event Action<double>? UpdateView;

  public GameWindow() {
    logger.Trace("Initializing game window...");
    InitializeComponent();
    if (!Visible) Show();
    Initialize();
    logger.Trace("Game window created");
  }

  public string Title { get => base.Text; set => Text = value; }

  public Dpi Dpi {
    get {
      using var g = CreateGraphics();
      return new Dpi(g.DpiX / 96f, g.DpiY / 96f);
    }
  }

  #region IView Properties
  // FIXME: This doesn't take the pixel density into account
  [Category("GPU")]
  public Vector2D<int> FramebufferSize => new(glSurface.ClientSize.Width, glSurface.ClientSize.Height);

  [Category("Behavior")]
  public bool IsClosing => isClosing;

  [Category("Behavior")]
  public double Time => stopwatch.Elapsed.TotalSeconds;

  [Category("GPU")]
  public bool IsInitialized => true;

  [Category("Behavior")]
  public bool ShouldSwapAutomatically {
    get => false;
    set {}
  }

  [Category("Behavior")]
  public bool IsEventDriven {
    get => true;
    set {}
  }

  [Category("Behavior")]
  public bool IsContextControlDisabled {
    get => false;
    set {}
  }

  [Category("Behavior")]
  public double FramesPerSecond {
    get => Game.Instance?.TargetFrameRate ?? 60;
    set => Game.Instance?.TargetFrameRate = Convert.ToInt32(
      Math.Round(value, 0, MidpointRounding.AwayFromZero)
    );
  }

  [Category("Behavior")]
  public double UpdatesPerSecond {
    get => Game.Instance?.TargetUpdateRate.TotalSeconds ?? 60;
    set => Game.Instance?.TargetUpdateRate = TimeSpan.FromSeconds(value);
  }

  [Browsable(false)]
  public Windowing.GraphicsAPI API {
    get {
      var flags = Windowing.ContextFlags.Default;
      if (glSurface.Settings.Flags.HasFlag(ContextFlagMask.ForwardCompatibleBit))
        flags |= Windowing.ContextFlags.ForwardCompatible;
      if (glSurface.Settings.Flags.HasFlag(ContextFlagMask.DebugBit))
        flags |= Windowing.ContextFlags.Debug;

      return new Windowing.GraphicsAPI {
        API = Windowing.ContextAPI.OpenGL,
        Profile = glSurface.Settings.Profile switch {
          ContextProfileMask.CompatibilityProfileBit => Windowing.ContextProfile.Compatability,
          ContextProfileMask.CoreProfileBit => Windowing.ContextProfile.Core,
          _ => throw new InvalidOperationException()
        },
        Flags = flags,
        Version = new Windowing.APIVersion(glSurface.Settings.Version)
      };
    }
  }

  [Category("GPU")]
  public bool VSync {
    get => Game.Instance?.VSync ?? false;
    set => Game.Instance?.VSync = value;
  }

  [Category("GPU")]
  public Windowing.VideoMode VideoMode => new(FramebufferSize, Game.Instance!.TargetFrameRate);

  [Category("GPU")]
  public int? PreferredDepthBufferBits => OpenGL.GLContext.PreferredDepthBufferBits;

  [Category("GPU")]
  public int? PreferredStencilBufferBits => OpenGL.GLContext.PreferredStencilBufferBits;

  [Category("GPU")]
  public Vector4D<int>? PreferredBitDepth => new(OpenGL.GLContext.PreferredColorDepth);

  [Category("GPU")]
  public int? Samples => renderer?.MsaaSamples;

  [Browsable(false)]
  public IGLContext? GLContext => glSurface.Context;

  [Category("GPU")]
  public IVkSurface? VkSurface => null;

  [Browsable(false)]
  public INativeWindow? Native => null;

  [Category("GPU")]
  Vector2D<int> Windowing.IViewProperties.Size => new(ClientSize.Width, ClientSize.Height);
  #endregion

  #region IView Events
  public event Action<Vector2D<int>>? FramebufferResize;
  public event Action<bool>? FocusChanged;
  public event Action<double>? Render;

  event Action<Vector2D<int>>? Windowing.IView.Resize {
    add {
      if (value == null) return;
      void handler(object? s, EventArgs e) => value(FramebufferSize);
      handlerMap[value] = handler;
      Resize += handler;
    }
    remove {
      handlerMap.Remove(value!);
      Resize -= handlerMap[value!];
    }
  }

  event Action? Windowing.IView.Closing {
    add {
      if (value == null) return;
      // Wait for our own Closing event handlers to complete
      void handler(object? s, EventArgs e) => Invoke(() => {
        if (isClosing) value();
      });
      handlerMap[value] = handler;
      Closing += handler;
    }
    remove {
      handlerMap.Remove(value!);
      Closing -= handlerMap[value!].To<CancelEventHandler>();
    }
  }

  event Action? Windowing.IView.Load {
    add {
      if (value == null) return;
      void handler(object? s, EventArgs e) => value();
      handlerMap[value] = handler;
      Load += handler;
    }
    remove {
      handlerMap.Remove(value!);
      Load -= handlerMap[value!];
    }
  }

  event Action<double>? Windowing.IView.Update {
    add => UpdateView += value;
    remove => UpdateView -= value;
  }
  #endregion

  // FIXME: This method ought be extracted into a platform-idependent base-class.
  public void Start() {
    stopwatch.Start();
    Debug.Assert(renderer != null, "Renderer should be created before starting the game.");
    if (Game.Instance == null) {
      logger.Trace("Starting game...");
      Scene.IoC.Register<IInputContext>(
        Reuse.Scoped,
        Made.Of(r => ServiceInfo.Of<IWindow>(), window => window.CreateInput(Arg.Of<IWindow>())),
        // The input abstraction is kida heavy, so let services dispose it
        Setup.With(trackDisposableTransient: true, allowDisposableTransient: true)
      );
      var game = new Game(renderer);
      Task.Run(game.Run);
    } else {
      logger.Trace("Resuming game...");
      Game.Instance.Resume();
    }
  }

  #region IView Methods
  public void Initialize() {
    if (glSurface.IsHandleCreated) rendererCreated.Set();
    logger.Trace("Waiting for renderer instantiation...");
    rendererCreated.WaitOne();
    logger.Trace("Renderer created");
  }

  public void Reset() {
    Game.Instance?.Pause();
    Hide();
    Controls.Clear();
    glSurface.Dispose();
    glSurface = null;
  }

  public void Run(Action onFrame) {
    while (!isClosing) {
      DoEvents();
      onFrame();
    }
  }

  void Windowing.IView.Focus() => Focus();

  public void ContinueEvents() => Invoke(Application.DoEvents);
  public void DoEvents() => Application.DoEvents();
  public void DoRender() => Render?.Invoke(Game.Instance!.FrameTime.TotalMilliseconds);
  public void DoUpdate() => UpdateView?.Invoke(Game.Instance!.FrameTime.TotalMilliseconds);
  public Vector2D<int> PointToClient(Vector2D<int> point) {
    var pt = base.PointToClient(new(point.X, point.Y));
    return new(pt.X, pt.Y);
  }

  public Vector2D<int> PointToFramebuffer(Vector2D<int> point) {
    // FIXME: This does not take into account the screen's DPI
    var pt = glSurface.PointToClient(new(point.X, point.Y));
    return new(pt.X, pt.Y);
  }

  public Vector2D<int> PointToScreen(Vector2D<int> point) {
    var pt = base.PointToScreen(new(point.X, point.Y));
    return new(pt.X, pt.Y);
  }
  #endregion

  #region IInputPlatform Members
  public bool IsApplicable(Windowing.IView view) => view is GameWindow;
  public IInputContext CreateInput(Windowing.IView view) => new InputAdapter(this);
  #endregion

  private void GameWindow_GotFocus(object sender, EventArgs e) {
    var game = Game.Instance;
    if (game?.IsPaused ?? false) game.Resume();
    FocusChanged?.Invoke(true);
  }

  private void GameWindow_LostFocus(object sender, EventArgs e) {
    var game = Game.Instance;
    var isPaused = game?.IsPaused ?? false;
    if (!isPaused) game?.Resume();
    FocusChanged?.Invoke(false);
  }

  private void GameWindow_Resize(object sender, EventArgs e) =>
    FramebufferResize?.Invoke(FramebufferSize);

  private void GameWindow_FormClosing(object sender, FormClosingEventArgs e) {
    var wasGameStopped = Game.Instance?.Quit() ?? false;
    // Cancel the closure if the game is still running
    e.Cancel = wasGameStopped == false || Game.IsRunning;
    isClosing = e.Cancel == false;
  }

  private void GlSurface_RendererCreated(IGraphicsSurface _, IRenderer renderer) {
    this.renderer = renderer;
    rendererCreated.Set();
  }
}

// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Input;
using OpenCobra.GDK.Platform;
using OpenRCT3.Input;
using OpenRCT3.OpenGL;
using OpenRCT3.Platforms;
using Silk.NET.Input;
using System.Numerics;
using System.Threading;

#if WINDOWS
using System.Windows.Forms;
#elif OSX
using AppKit;
#endif

namespace OpenRCT3;

/// <summary>
/// The game world.
/// </summary>
public class Game : IGame {
  /// <summary>The maximum number of simulation ticks to process in one instant.</summary>
  private const int MaxSimulationTicks = 8;
  /// <summary>The minimum time between lag warning messages.</summary>
  /// <remarks>This prevents spamming the log with warnings about lag.</remarks>
  private readonly TimeSpan lagWarningDebounceInterval = TimeSpan.FromSeconds(10);

  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private bool isRunning = false;
  private bool isPaused = false;
  private readonly ManualResetEvent resumeSignal = new(true);
  private readonly Stopwatch stopwatch = new();
  private DateTime lastLagWarning = DateTime.Now;
  private readonly Renderer renderer = IoC.Resolve<IRenderer>() as Renderer ??
    throw new InvalidOperationException();

  public static Container IoC => IGame.IoC;
  public static Game? Instance { get; private set; }
  public static bool IsRunning => Instance?.isRunning ?? false;
  /// <summary>
  /// Default frame rate of the game loop, in frames per second.
  /// </summary>
  public readonly static int DefaultFrameRate = 60;

  /// <summary>
  /// Raised once the game has started and the game loop is running.
  /// </summary>
  /// <remarks>
  /// The game is started via <see cref="Run"/>.
  /// </remarks>
  public event Action? Started;
  /// <summary>
  /// <para>Raised when the game ends, i.e. when the user quits.</para>
  /// <para>See <see cref="Quit"/>.</para>
  /// </summary>
  public event Action? Exited;

  public AppConfig Config { get; } = AppConfig.Instance;

  public bool IsPaused => isPaused;

  /// <summary>
  /// <para>The time taken to render the last frame, or null if no frame has been rendered yet.</para>
  /// <para>Use <see cref="TargetFrameRate"/> to set the frame rate.</para>
  /// </summary>
  public TimeSpan FrameTime { get; private set; } = TimeSpan.Zero;

  /// <summary>
  /// Target frame rate of the game loop, in frames per second.
  /// </summary>
  public int TargetFrameRate {
    get => Convert.ToInt32(1.0 / TargetFrameTime.TotalSeconds);
    set => TargetFrameTime = TimeSpan.FromSeconds(1.0 / value);
  }

  /// <summary>
  /// Target frame time of the game loop.
  /// </summary>
  public TimeSpan TargetFrameTime { get; private set; } = TimeSpan.FromSeconds(1.0 / 60.0);

  /// <summary>
  /// Target simulation tick rate.
  /// </summary>
  public TimeSpan TargetUpdateRate { get; set; } = TimeSpan.FromSeconds(1.0 / 60.0);

  /// <summary>
  /// Whether the game should use vertical sync (VSync) to limit the frame rate.
  /// </summary>
  public bool VSync { get; set; } = false;

  public Simulation.World World { get; } = new();
  public Scene Scene { get; } = new();

  private readonly InputController inputController;
  /// <summary>
  /// Resolves the game's named, rebindable input actions (see <see cref="DefaultBindings"/>) against the
  /// window's live <see cref="IInputContext"/>.
  /// </summary>
  public InputActionMap InputActions => inputController.Actions;

  public Game() {
    Instance = this;
    IoC.RegisterInstance(this);

    inputController = new InputController(IoC.Resolve<IInputContext>(), Config, Scene.Camera, Quit);

    logger.Trace("Creating game world...");
    logger.Warn("Simulation features are unimplemented!");

    // Load the game world
    // TODO: Show a progress bar while loading
    World.Load();
    logger.Trace("Game world loaded");
  }

  /// <summary>
  /// Starts the game loop.
  /// </summary>
  /// <remarks>
  /// The game loop runs at a fixed frame rate, sleeping when ahead of schedule to reduce CPU usage.
  /// </remarks>
  /// <seealso cref="TargetFrameRate"/>
  /// <seealso cref="TargetFrameTime"/>
  /// <seealso href="https://gameprogrammingpatterns.com/game-loop.html"/>
  public void Run() {
    isRunning = true;

    // Run the game loop
    Started?.Invoke();
    stopwatch.Start();
    var previousTime = stopwatch.Elapsed;
    // Measures wall time that has elapsed since the last frame
    var lag = TimeSpan.Zero;

    // Implements the fixed-update-time-step, variable-rendering pattern to decouple
    // simulation stability (fixed step for physics/AI determinism) from visual
    // smoothness (variable render rate).
    //
    // See https://gameprogrammingpatterns.com/game-loop.html
    while (IsRunning) {
      // Wait for the resume signal if the game is paused
      if (isPaused) {
        resumeSignal.WaitOne();
        logger.Trace("Game resumed");
      }

      var currentTime = stopwatch.Elapsed;
      var elapsed = FrameTime = currentTime - previousTime;
      previousTime = currentTime;
      // FIXME: Ought the game NOT accumulate lag if the game was paused?
      lag += elapsed;

      // Process any pending window events, e.g. input events
#if WINDOWS
      Application.DoEvents();
#elif OSX
      // FIXME: Pump macOS windowing events
      // See https://duckduckgo.com/?q=osx+how+to+pump+windowing+events+in+a+game+loop&ia=web
      NSApplication.EnsureUIThread();
#endif

      // Simulation ticks are fixed steps to aid physics/AI determinism
      // For example, a 60Hz target frame-rate would process one tick 60 times per second
      LogLagWarning(lag);
      for (var tickCount = 0; tickCount < MaxSimulationTicks && lag >= TargetFrameTime; tickCount++) {
        Tick(
          delta: TargetFrameTime,
          // Normalize the lag to a percentage representing how far into the
          // simulation step we are (0.0 = just started, 1.0 = just finished)
          interpolation: lag.TotalMilliseconds / TargetFrameTime.TotalMilliseconds);
        lag -= TargetFrameTime;
      }

      // Poll held-key camera movement (WASD/arrows) once per rendered frame - unlike the
      // InputActionMap.Pressed/Scrolled-driven handlers, continuous movement has no discrete event to
      // hook and needs this frame's elapsed time to scale by.
      inputController.Update((float)elapsed.TotalSeconds);

      // Rendering can happen at arbitrary points between updates, and frames can
      // be dropped if the machine is slow.
      Scene.Update(delta: elapsed);

      // Proof-of-concept: an ImDraw.Axis marker at the rotation-marker cube's center, exercising
      // ImDraw end-to-end (see OpenCobra/GDK/ImDraw.cs). screenSpaceExtent keeps it a fixed 80px size
      // regardless of camera distance, unlike the marker cube it's attached to.
      Scene.ImDraw.Axis(World.MarkerCenter, Quaternion.Identity, size: 80f, screenSpaceExtent: true);

      renderer.Render(Scene);

      // Reduce CPU usage by sleeping when ahead of schedule
      var remaining = TargetFrameTime - lag;
      if (remaining > TimeSpan.Zero) {
        var sleepMs = remaining.TotalMilliseconds / 2.0;
        if (sleepMs > 2) Thread.Sleep((int)sleepMs / 2);
      }
    }

    Exited?.Invoke();
    logger.Info("Game exited");
  }

  public void Pause() {
    isPaused = true;
    resumeSignal.Reset();
  }

  public void Resume() {
    isPaused = false;
    resumeSignal.Set();
  }

  /// <summary>
  /// Try to quit the game.
  /// </summary>
  /// <returns>Whether the game stopped running.</returns>
  public bool Quit() {
    // TODO: Check for unsaved changes and prevent closure
    isRunning = false;

    if (!isRunning) logger.Info("Exiting game...");
    return !isRunning;
  }

  public void Dispose() {
    // TODO: World.Dispose();
    GC.SuppressFinalize(this);
    Instance = null;
  }

  /// <summary>
  /// Advances the simulation.
  /// </summary>
  /// <param name="delta">The time between ticks.</param>
  /// <param name="interpolation">The interpolation fraction.</param>
  private void Tick(TimeSpan delta, double interpolation) {
    // TODO: Advance the simulation logic by a fixed time step
    // TODO: Scheduler.Execute(delta);
  }

  [Conditional("DEBUG")]
  private void LogLagWarning(TimeSpan lag) {
    // TODO: Detect excessive lag and lower the user's target frame-rate
    // TODO: Maybe even show a modal to the user:
    // "You are experiencing excessive lag. Lowering frame-rate to prevent stuttering."
    // "Consider lowering your target frame-rate in the game settings."
    if (lag <= TargetFrameTime || DateTime.Now - lastLagWarning <= lagWarningDebounceInterval) return;

    var details = $"{lag.TotalMilliseconds}ms (target: {TargetFrameTime.TotalMilliseconds}ms)";
    logger.Warn($"Lag has exceeded target frame time budget: {details}");
    lastLagWarning = DateTime.Now;
  }
}

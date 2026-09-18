// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using DryIoc;
using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.Input;
using OpenCobra.GDK.Platform;
using OpenRCT3.Input;
using OpenRCT3.OpenGL;
using OpenRCT3.Platforms;
using OpenRCT3.Scenario;
using OpenRCT3.Simulation;
using System.Collections.Generic;
using System.Threading;
using Silk.NET.Input;

#if OSX
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
  private readonly GameRunLifecycle lifecycle = new();
  private bool isPaused = false;
  private readonly ManualResetEvent resumeSignal = new(true);
  private readonly Stopwatch stopwatch = new();
  private DateTime lastLagWarning = DateTime.Now;
  private IRenderer? renderer = ResolveRenderer(Game.IoC);
  private Scene? ownedScene;
  private Simulation.World? ownedWorld;
  private bool disposed;

  public static DryIoc.Container IoC => IGame.IoC;
  public static Game? Instance { get; private set; }
  public static bool IsRunning => Instance?.lifecycle.IsRunning ?? false;

  internal static Game? DetachInstance() {
    var instance = Instance;
    Instance = null;
    return instance;
  }

  internal static IRenderer ResolveRenderer(IResolverContext resolver) =>
    resolver.Resolve<IRenderer>();

  internal IRenderer? BoundRenderer => Volatile.Read(ref renderer);

  internal void BindRenderer(IRenderer replacement) =>
    Volatile.Write(ref renderer, replacement);

  internal void UnbindRenderer(IRenderer ownedRenderer) =>
    Interlocked.CompareExchange(ref renderer, null, ownedRenderer);

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

  private readonly object inputLock = new();
  private InputController? inputController;
  /// <summary>
  /// Resolves the game's named, rebindable input actions (see <see cref="DefaultBindings"/>) against the
  /// window's live <see cref="IInputContext"/>.
  /// </summary>
  public InputActionMap InputActions => Volatile.Read(ref inputController)?.Actions
    ?? throw new InvalidOperationException("Input services are unavailable.");

  internal void BindInput(IInputContext input) {
    ArgumentNullException.ThrowIfNull(input);
    var replacement = new InputController(input, Config, Scene.Camera, Quit);
    lock (inputLock) inputController = replacement;
  }

  internal void UnbindInput(IInputContext ownedInput) {
    ArgumentNullException.ThrowIfNull(ownedInput);
    lock (inputLock) {
      if (ReferenceEquals(inputController?.Context, ownedInput)) inputController = null;
    }
  }

  public Game() {
    ownedScene = Scene;
    ownedWorld = World;
    Instance = this;
    IoC.RegisterInstance(this);

    BindInput(IoC.Resolve<IInputContext>());

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
    if (!lifecycle.TryStart()) return;

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
    while (lifecycle.IsRunning) {
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
      if (!ProcessEventsAndCheckRunning(Application.DoEvents, static () => IsRunning)) break;
#elif OSX
      // FIXME: Pump macOS windowing events
      // See https://duckduckgo.com/?q=osx+how+to+pump+windowing+events+in+a+game+loop&ia=web
      if (!ProcessEventsAndCheckRunning(NSApplication.EnsureUIThread, static () => IsRunning)) break;
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
      UpdateInput((float)elapsed.TotalSeconds);

      // Rendering can happen at arbitrary points between updates, and frames can
      // be dropped if the machine is slow.
      Scene.Update(delta: elapsed);
      Volatile.Read(ref renderer)?.Render(Scene);

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

  internal static bool ProcessEventsAndCheckRunning(
    Action processEvents,
    Func<bool> isRunning
  ) {
    processEvents();
    return isRunning();
  }

  private void UpdateInput(float deltaSeconds) {
    lock (inputLock) inputController?.Update(deltaSeconds);
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
    lifecycle.Stop();
    resumeSignal.Set();

    if (!lifecycle.IsRunning) logger.Info("Exiting game...");
    return !lifecycle.IsRunning;
  }

  public void Dispose() {
    if (disposed) return;
    disposed = true;
    var scene = ownedScene;
    var world = ownedWorld;
    ownedScene = null;
    ownedWorld = null;

    // Dispose GPU-backed scene resources while the graphics context is still alive, then release
    // the world-owned texture catalog and simulation systems.
    DisposeOwnedResources(
      scene == null ? null : scene.Dispose,
      world == null ? null : world.Dispose,
      () => {
        lifecycle.Stop();
        resumeSignal.Set();
        Instance = null;
        GC.SuppressFinalize(this);
      });
  }

  internal static void DisposeOwnedResources(
    Action? disposeScene,
    Action? disposeWorld,
    Action clearState
  ) {
    try {
      var releases = new List<Action>();
      if (disposeScene != null) releases.Add(disposeScene);
      if (disposeWorld != null) releases.Add(disposeWorld);
      ResourceReleaser.Run(releases);
    } finally {
      clearState();
    }
  }

  /// <summary>
  /// Advances the simulation.
  /// </summary>
  /// <remarks>
  /// Called at a fixed timestep (potentially multiple times per frame if lagging, clamped by
  /// <see cref="MaxSimulationTicks"/>). Invokes <see cref="Simulation.World.Update"/> to execute all
  /// registered systems in phase order (Early → Update → Render → Late). Park load requests from
  /// <see cref="ParkChooser"/> are dequeued and executed in Early phase, deferred from the render loop
  /// to avoid UI-thread reentrancy.
  /// </remarks>
  /// <param name="delta">The time between ticks.</param>
  /// <param name="interpolation">The interpolation fraction.</param>
  private void Tick(TimeSpan delta, double interpolation) {
    World.Update(delta);
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

internal sealed class GameRunLifecycle {
  private const int Created = 0;
  private const int Running = 1;
  private const int Stopped = 2;
  private int state = Created;

  public bool IsRunning => Volatile.Read(ref state) == Running;

  public bool TryStart() =>
    Interlocked.CompareExchange(ref state, Running, Created) == Created;

  public void Stop() => Interlocked.Exchange(ref state, Stopped);
}

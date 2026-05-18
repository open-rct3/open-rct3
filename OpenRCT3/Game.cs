// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Diagnostics;
using OpenCobra.GDK;
using OpenRCT3.Simulation;
using OpenRCT3.Platforms;
using NLog;

#if WINDOWS
using System.Windows.Forms;
#elif OSX
using AppKit;
#endif

namespace OpenRCT3;

/// <summary>
/// The game world.
/// </summary>
public class Game : IDisposable {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static Game? Instance { get; private set; }
  public static bool IsRunning => Instance != null;
  /// <summary>
  /// Default frame rate of the game loop, in frames per second.
  /// </summary>
  public static readonly int DefaultFrameRate = 60;

  public AppConfig Config { get; } = AppConfig.Instance;
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
  [Unowned("The renderer is owned by the platform abstraction layer.")]
  public IRenderer Renderer { get; }
  public World World { get; }
  public Scene Scene { get; } = new();

  private readonly Stopwatch _stopwatch = new();

  /// <param name="renderer">The game renderer.</param>
  public Game(IRenderer renderer) {
    Instance = this;
    Renderer = renderer;
    World = new World();

    logger.Info("Simulation features are unimplemented");

    // Load the game world
    // TODO: Show a progress bar while loading
    World.Load();
    if (!string.IsNullOrEmpty(Config.InstallPath)) {
      var nullbmpPath = System.IO.Path.Combine(Config.InstallPath, "nullbmp.common.ovl");
      Scene.LoadTexture(nullbmpPath, "nullbmp");
    }
  }

  /// <summary>
  /// Starts the game loop.
  /// </summary>
  /// <remarks>
  /// The game loop runs at a fixed frame rate, sleeping when ahead of schedule to reduce CPU usage.
  /// </remarks>
  /// <seealso cref="TargetFrameRate"/>
  /// <seealso cref="TargetFrameTime"/>
  /// <see href="https://gameprogrammingpatterns.com/game-loop.html"/>
  public void Run() {
    _stopwatch.Start();

    // Run the game loop
    var previousTime = _stopwatch.Elapsed;
    var lag = TimeSpan.Zero;
    var msPerUpdate = TargetFrameTime;

    while (IsRunning) {
      var currentTime = _stopwatch.Elapsed;
      var elapsed = currentTime - previousTime;
      previousTime = currentTime;
      lag += elapsed;

      // Process any pending window events, e.g. input events
#if WINDOWS
      Application.DoEvents();
#elif OSX
      // FIXME: Pump macOS windowing events
      // See https://duckduckgo.com/?q=osx+how+to+pump+windowing+events+in+a+game+loop&ia=web
      NSApplication.EnsureUIThread();
#endif

      while (lag >= msPerUpdate) {
        // FIXME: Shouldn't the amount of time Tick takes affect the lag calculation?
        Tick(msPerUpdate);
        lag -= msPerUpdate;
      }

      Render((float)(lag.TotalMilliseconds / msPerUpdate.TotalMilliseconds));

      // Reduce CPU usage by sleeping when ahead of schedule
      var remaining = msPerUpdate - lag;
      if (remaining > TimeSpan.Zero) {
        var sleepMs = (int)(remaining.TotalMilliseconds / 2.0);
        if (sleepMs > 2)
          System.Threading.Thread.Sleep(sleepMs / 2);
      }
    }
  }

  /// <summary>
  /// Advances the simulation.
  /// </summary>
  /// <param name="delta">The time between ticks.</param>
  private void Tick(TimeSpan delta) {
    // TODO: Advance the simulation logic by a fixed time step
  }

  /// <summary>
  /// Renders the scene.
  /// </summary>
  /// <param name="interpolation">The interpolation fraction.</param>
  private void Render(float interpolation) {
    if (Renderer.TryGetTarget(out var renderer))
      renderer.Render(Scene);
  }

  public void Dispose() {
    // TODO: World.Dispose();
    GC.SuppressFinalize(this);
    Instance = null;
  }
}

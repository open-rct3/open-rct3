// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NLog;
using OpenCobra.GDK;
using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Meshes;
using OpenRCT3.OpenGL;
using OpenRCT3.Platforms;
using OpenRCT3.Simulation;
using Silk.NET.OpenGL;
using System.Drawing;
using System.Numerics;

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
  private bool isRunning = true;

  public static Game? Instance { get; private set; }
  public static bool IsRunning => Instance?.isRunning ?? false;
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
  public IRenderer Renderer { get; }
  public World World { get; } = new();
  public Scene Scene { get; } = new();

  private readonly Stopwatch _stopwatch = new();

  /// <param name="renderer">The game renderer.</param>
  public Game(IRenderer renderer) {
    Instance = this;
    Renderer = renderer;

    logger.Trace("Creating game world...");
    logger.Info("Simulation features are unimplemented");

    // Load the game world
    // TODO: Show a progress bar while loading
    World.Load();
    logger.Trace("Game world loaded");

    // Create a flat quad on the XY plane (Z-up)
    var grass = Color.FromArgb(79, 129, 14).ToGl();
    Scene.Models.Add(new(new Mesh([
      new Vertex { Position = new Vector3(-10, -10, 0), TexCoord = new Vector2(0, 0), Color = grass },
      new Vertex { Position = new Vector3( 10, -10, 0), TexCoord = new Vector2(1, 0), Color = grass },
      new Vertex { Position = new Vector3( 10,  10, 0), TexCoord = new Vector2(1, 1), Color = grass },
      new Vertex { Position = new Vector3(-10,  10, 0), TexCoord = new Vector2(0, 1), Color = grass }
    ], [0, 1, 2, 0, 2, 3]) { Name = "Ground" }) {
      Material = new Flat()
    });
    logger.Trace("Added ground plane");
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

      Render(lag.TotalMilliseconds / msPerUpdate.TotalMilliseconds);

      // Reduce CPU usage by sleeping when ahead of schedule
      var remaining = msPerUpdate - lag;
      if (remaining > TimeSpan.Zero) {
        var sleepMs = (int)(remaining.TotalMilliseconds / 2.0);
        if (sleepMs > 2)
          System.Threading.Thread.Sleep(sleepMs / 2);
      }
    }

    logger.Info("Exiting game...");
  }

  /// <summary>
  /// Try to quit the game.
  /// </summary>
  /// <returns>Whether the game stopped running.</returns>
  public bool Quit() {
    // TODO: Check for unsaved changes and prevent closure
    isRunning = false;

    return isRunning == false;
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
  private void Render(double interpolation) {
    // TODO: Supply the interpolation fraction to the scene for animations
    Renderer.Render(Scene);
  }

  public void Dispose() {
    // TODO: World.Dispose();
    GC.SuppressFinalize(this);
    Instance = null;
  }
}

// IGame
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;

namespace OpenCobra.GDK.Game;

public interface IGame : IDisposable {
  public readonly static Container IoC = new();

  /// <summary>
  /// Raised once the game has started and the game loop is running.
  /// </summary>
  /// <remarks>
  /// The game is started via <see cref="Run"/>.
  /// </remarks>
  event Action? Started;

  /// <summary>
  /// <para>Raised when the game ends, i.e. when the user quits.</para>
  /// <para>See <see cref="Quit"/>.</para>
  /// </summary>
  event Action? Exited;

  /// <summary>
  /// Whether this game is running, i.e. 
  /// </summary>
  static bool IsRunning { get; }

  /// <summary>
  /// Whether this game is paused.
  /// </summary>
  bool IsPaused { get; }

  /// <summary>
  /// Whether the game should use vertical sync (VSync) to limit the frame rate.
  /// </summary>
  bool VSync { get; }

  /// <summary>
  /// <para>The time taken to render the last frame, or null if no frame has been rendered yet.</para>
  /// <para>Use <see cref="TargetFrameRate"/> to set the frame rate.</para>
  /// </summary>
  TimeSpan FrameTime { get; }

  /// <summary>
  /// Target frame rate of the game loop, in frames per second.
  /// </summary>
  int TargetFrameRate { get; }

  /// <summary>
  /// Target frame time of the game loop, i.e. the budgeted time for a single frame in the game loop.
  /// </summary>
  TimeSpan TargetFrameTime { get; }

  /// <summary>
  /// Target simulation tick rate, i.e. how often to update this game's simulation.
  /// </summary>
  TimeSpan TargetUpdateRate { get; }

  Scene Scene { get; }

  void Run();
  void Pause();
  void Resume();
  /// <summary>
  /// Try to quit the game.
  /// </summary>
  /// <returns>Whether the game stopped running.</returns>
  bool Quit();
}

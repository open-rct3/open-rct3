// System Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;

namespace OpenCobra.GDK.Game;

/// <summary>
/// Represents a self-contained game system.
/// </summary>
public interface ISystem : IDisposable {
  [Category("Behavior")]
  PipelinePhase Order { get; }

  /// <summary>
  /// Whether this system may be executed in parallel with other systems in its phase.
  /// </summary>
  /// <remarks>
  /// Use a parallel system for CPU-bound tasks with large datasets (> ~10,000 items).
  /// </remarks>
  /// <seealso cref="Order"/>
  [Category("Behavior")]
  bool Parallelizable { get; }

  /// <summary>
  /// Attach this system to the given game <paramref name="world"/>.
  /// </summary>
  void Attach([Unowned("Systems may not retain references to the world.")] WeakReference<IWorld> world);

  /// <summary>
  /// Start updating this system.
  /// </summary>
  /// <remarks>
  /// Perform any necessary setup procedures here.
  /// </remarks>
  /// <seealso cref="Update(TimeSpan)"/>
  void Start();

  /// <summary>
  /// Update the state of this system.
  /// </summary>
  /// <param name="delta">Amount of time since the last iteration of the game's update cycle</param>
  void Update(TimeSpan delta);

  /// <summary>
  /// Stop updating this system.
  /// </summary>
  /// <remarks>
  /// Perform any tear-down operations here to conserve memory.
  /// </remarks>
  void Stop();
}

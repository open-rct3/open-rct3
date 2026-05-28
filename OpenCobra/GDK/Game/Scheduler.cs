// System Scheduler
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc.ImTools;

namespace OpenCobra.GDK.Game;

public static class Scheduler {
  private static TaskScheduler scheduler = Task.Factory.Scheduler;

  /// <summary>
  /// Execute the given collection of <paramref name="systems"/> in order.
  /// </summary>
  /// <param name="systems"></param>
  /// <param name="delta">Amount of time since the last iteration of the game's update cycle</param>
  /// <seealso cref="ISystem.Order"/>
  /// <seealso cref="ISystem.Update(TimeSpan)"/>
  /// <seealso cref="IGame.TargetUpdateRate"/>
  public static void Execute(IEnumerable<ISystem> systems, TimeSpan delta) {
    var buckets = systems.ToDictionary(s => s.Order, s => s);

    var orderedSystems =
      from pair in buckets
      orderby pair.Key.To<int>()
      select pair.Value;
    foreach (var system in orderedSystems) {
      if (system.Parallelizable) {
        // TODO: Use Task.Factory.StartNew to do slow async work in the background
        // Schedule on thread pool
        Task.Run(() => system.Update(delta));
      } else {
        system.Update(delta);
      }
    }
  }
}

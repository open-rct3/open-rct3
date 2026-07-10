// System Scheduler
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc.ImTools;
using NLog;

namespace OpenCobra.GDK.Game;

public static class Scheduler {
  private static readonly Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>
  /// Execute the given collection of <paramref name="systems"/> in order.
  /// </summary>
  /// <param name="systems"></param>
  /// <param name="delta">Amount of time since the last iteration of the game's update cycle</param>
  /// <exception cref="AggregateException">Raised when one or more parallel systems failed to update</exception>
  /// <seealso cref="ISystem.Order"/>
  /// <seealso cref="ISystem.Update(TimeSpan)"/>
  /// <seealso cref="IGame.TargetUpdateRate"/>
  public static void Execute(IEnumerable<ISystem> systems, TimeSpan delta) {
    var buckets = systems.GroupBy(s => s.Order).ToDictionary(g => g.Key, g => g.ToList());
    var phases = buckets.Keys.OrderBy(key => key.To<int>());

    try {
      foreach (var phase in phases) {
        var parallelSystems = buckets[phase].Where(s => s.Parallelizable)
          .AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount)
          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
          .WithMergeOptions(ParallelMergeOptions.NotBuffered);
        var linearSystems = buckets[phase].Except(parallelSystems);

        foreach (var system in parallelSystems) system.Update(delta);
        foreach (var system in linearSystems) system.Update(delta);
      }
    } catch (AggregateException ex) {
      logger.Error("Could not update one or more parallel systems:", ex);
      throw;
    } catch (OperationCanceledException) {
      logger.Trace("Parallel system execution was cancelled.");
    }
  }
}

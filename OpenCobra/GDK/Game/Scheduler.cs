// System Scheduler
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc.ImTools;
using NLog;
using System;
using System.Diagnostics;

namespace OpenCobra.GDK.Game;

public static class Scheduler {
  private static readonly Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>
  /// Execute the given collection of <paramref name="systems"/> in order.
  /// </summary>
  /// <remarks>
  /// Systems are ordered by <see cref="PipelinePhase"/> using <see cref="PipelinePhase.To{T}()"/> to convert
  /// to numeric order. If a new <see cref="PipelinePhase"/> value is added with a non-contiguous integer value,
  /// the execution order will silently change.
  /// </remarks>
  /// <param name="systems"></param>
  /// <param name="delta">Amount of time since the last iteration of the game's update cycle</param>
  /// <exception cref="AggregateException">Raised when one or more parallel systems failed to update</exception>
  /// <seealso cref="ISystem.Order"/>
  /// <seealso cref="ISystem.Update(TimeSpan)"/>
  /// <seealso cref="IGame.TargetUpdateRate"/>
  public static void Execute(IEnumerable<ISystem> systems, TimeSpan delta) {
    var buckets = systems.GroupBy(s => s.Order).ToDictionary(g => g.Key, g => g.ToList());
    var phases = buckets.Keys.OrderBy(key => key.To<int>()).ToList();

    Debug.Assert(ValidatePhaseOrdering(phases), "PipelinePhase values must be contiguous");


    try {
      foreach (var phase in phases) {
        var phaseSystems = buckets[phase];
        var parallelSet = new HashSet<ISystem>(phaseSystems.Where(s => s.Parallelizable));
        var linearSystems = phaseSystems.Where(s => !parallelSet.Contains(s));

        var parallelSystems = parallelSet.AsParallel().WithDegreeOfParallelism(Environment.ProcessorCount)
          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
          .WithMergeOptions(ParallelMergeOptions.NotBuffered);

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

  private static bool ValidatePhaseOrdering(List<PipelinePhase> phases) {
    if (phases.Count < 2) return true;
    for (int i = 0; i < phases.Count - 1; i++) {
      if ((int)phases[i + 1] - (int)phases[i] != 1)
        return false;
    }
    return true;
  }
}

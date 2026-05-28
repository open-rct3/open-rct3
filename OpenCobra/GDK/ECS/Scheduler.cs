// ECS System Scheduler
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc.ImTools;

namespace OpenCobra.GDK.ECS;

public static class Scheduler {
  public static void Execute(IEnumerable<System> systems, TimeSpan delta) {
    var buckets = systems.ToDictionary(s => s.SystemOrder, s => s);

    var orderedSystems =
      from pair in buckets
      orderby pair.Key.To<int>()
      select pair.Value;
    foreach (var system in orderedSystems) {
      if (system.Parallelizable) {
        // Schedule on thread pool
        Task.Run(() => system.Update(delta));
      } else {
        system.Update(delta);
      }
    }
  }
}

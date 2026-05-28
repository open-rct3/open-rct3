// Progress
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using Tasks = System.Threading.Tasks;

namespace OpenCobra.GDK.Streaming;

/// <summary>
/// Represents a task to be executed during a progress measurement.
/// </summary>
public record struct Task(Action Action, string? Description = null);
/// <summary>
/// Represents an ongoing process measurement, including the progress and the asynchronous task measured.
/// </summary>
public record struct Measurement(Progress Progress, Tasks.Task Task);

/// <summary>
/// Represents the progress of a long-running task.
/// </summary>
public record struct Progress(string Task, float Loaded = 0.0f) {
  public static readonly Progress COMPLETE = new(string.Empty, 1.0f);
  public static readonly string ELLIPSIS = "\u2026";
  private static readonly string DEFAULT_LOADING_TASK = $"Loading{ELLIPSIS}";

  public readonly float LoadedPercent => Loaded * 100.0f;
  public readonly float LoadedPercentRounded => Convert.ToSingle(Math.Round(LoadedPercent, 2));
  public readonly bool IsLoading => Loaded < 1;
  public readonly bool IsLoaded => Loaded == 1;

  public static Progress operator +(Progress a, Progress b) => new(a.Task, a.Loaded + b.Loaded);

  public override readonly string ToString() => $"{Task}: {LoadedPercentRounded}%";

  /// <summary>
  /// Measures the progress of a list of tasks.
  /// </summary>
  /// <remarks>
  /// Tasks are executed sequentially, one after another, updating the progress as they complete.
  /// </remarks>
  public static Measurement MeasureTasks(IReadOnlyList<Task> tasks) {
    // FIXME: The Progress type ought be immutable
    var progress = new Progress(DEFAULT_LOADING_TASK);
    var totalTasks = tasks.Count;

    return new(progress, Tasks.Task.Run(() => {
      for (var i = 0; i < totalTasks; i++) {
        // TODO: Measure the duration of each task
        var task = tasks[i];
        progress.Task = $"{task.Description}{ELLIPSIS}" ?? DEFAULT_LOADING_TASK;
        task.Action();
        progress.Loaded = Convert.ToSingle(i) / totalTasks;
      }
    }));
  }
}

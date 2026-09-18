// ProgressTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.GDK.Streaming;

namespace OpenCobra.Tests.GDK.Streaming;

[TestFixture]
public class ProgressTests {
  [Test]
  public void IsLoading_IsTrue_WhileLoadedIsBelowTheCompletionThreshold() {
    Assert.That(new Progress("task", 0.0f).IsLoading, Is.True);
    Assert.That(new Progress("task", 0.5f).IsLoading, Is.True);
    Assert.That(new Progress("task", 0.99f).IsLoading, Is.True);
  }

  [Test]
  public void IsLoading_IsFalse_OnceLoadedReachesTheCompletionThreshold() {
    Assert.That(new Progress("task", 1.0f).IsLoading, Is.False);
    Assert.That(new Progress("task", 1.5f).IsLoading, Is.False);
  }

  [Test]
  public void IsLoaded_IsFalse_WhileLoadedIsBelowTheCompletionThreshold() {
    Assert.That(new Progress("task", 0.0f).IsLoaded, Is.False);
    Assert.That(new Progress("task", 0.999f).IsLoaded, Is.False);
  }

  [Test]
  public void IsLoaded_IsTrue_OnceLoadedIsSufficientlyCloseToOne() {
    Assert.That(new Progress("task", 1.0f).IsLoaded, Is.True);
    Assert.That(new Progress("task", 2.0f).IsLoaded, Is.True);
  }

  [Test]
  public void IsLoaded_ToleratesFloatingPointAccumulationError_JustUnderOne() {
    // Summing ten tenths never lands exactly on 1.0f; the threshold must still treat this as loaded.
    var loaded = 0.0f;
    for (var i = 0; i < 10; i++) loaded += 0.1f;

    Assert.That(loaded, Is.Not.EqualTo(1.0f));
    Assert.That(new Progress("task", loaded).IsLoaded, Is.True);
    Assert.That(new Progress("task", loaded).IsLoading, Is.False);
  }

  [Test]
  public void Complete_IsLoadedAndNotLoading() {
    Assert.That(Progress.COMPLETE.IsLoaded, Is.True);
    Assert.That(Progress.COMPLETE.IsLoading, Is.False);
  }

  [Test]
  public void LoadedPercent_ScalesLoadedByOneHundred() {
    Assert.That(new Progress("task", 0.25f).LoadedPercent, Is.EqualTo(25.0f).Within(1e-4f));
  }

  [Test]
  public void LoadedPercentRounded_RoundsToTwoDecimalPlaces() {
    Assert.That(new Progress("task", 1.0f / 3.0f).LoadedPercentRounded, Is.EqualTo(33.33f).Within(1e-4f));
  }

  [Test]
  public void AdditionOperator_SumsLoadedAndKeepsTheLeftOperandTask() {
    var sum = new Progress("first", 0.3f) + new Progress("second", 0.4f);

    Assert.That(sum.Task, Is.EqualTo("first"));
    Assert.That(sum.Loaded, Is.EqualTo(0.7f).Within(1e-4f));
  }

  [Test]
  public void ToString_RendersTheTaskAndRoundedPercent() {
    Assert.That(new Progress("Textures", 0.5f).ToString(), Is.EqualTo("Textures: 50%"));
  }

  [Test]
  public void MeasureTasks_RunsEveryTaskAndCompletes() {
    var ran = 0;
    var tasks = new[] {
      new OpenCobra.GDK.Streaming.Task(() => ran++, "one"),
      new OpenCobra.GDK.Streaming.Task(() => ran++, "two"),
    };

    var measurement = Progress.MeasureTasks(tasks);
    measurement.Task.Wait();

    Assert.That(ran, Is.EqualTo(2));
  }
}

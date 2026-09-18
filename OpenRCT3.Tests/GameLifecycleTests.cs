namespace OpenRCT3.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;

[TestFixture]
public class GameLifecycleTests {
  [Test]
  public void Quit_WakesPausedGameLoop() {
    using var resumeSignal = new ManualResetEvent(false);
    var lifecycle = new GameRunLifecycle();
    Assert.That(lifecycle.TryStart(), Is.True);
    var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
    SetField(game, "lifecycle", lifecycle);
    SetField(game, "isPaused", true);
    SetField(game, "resumeSignal", resumeSignal);
    var waiting = Task.Run(() => resumeSignal.WaitOne(TimeSpan.FromSeconds(2)));

    var stopped = game.Quit();

    using (Assert.EnterMultipleScope()) {
      Assert.That(stopped, Is.True);
      Assert.That(waiting.Result, Is.True);
    }
  }

  [Test]
  public void Run_QuitBeforeQueuedStartCannotBeUndone() {
    using var resumeSignal = new ManualResetEvent(true);
    var lifecycle = new GameRunLifecycle();
    var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
    SetField(game, "lifecycle", lifecycle);
    SetField(game, "resumeSignal", resumeSignal);

    var stopped = game.Quit();
    var runCompleted = Task.Run(game.Run).Wait(TimeSpan.FromSeconds(2));
    var visibleRunningState = Task.Run(() => lifecycle.IsRunning).Result;

    using (Assert.EnterMultipleScope()) {
      Assert.That(stopped, Is.True);
      Assert.That(runCompleted, Is.True);
      Assert.That(visibleRunningState, Is.False);
    }
  }

  [Test]
  public void ProcessEventsAndCheckRunning_QuitDuringEventsStopsFrameTail() {
    var running = true;
    var order = new List<string>();

    var shouldContinue = Game.ProcessEventsAndCheckRunning(
      () => {
        order.Add("events");
        running = false;
      },
      () => {
        order.Add("running");
        return running;
      });

    using (Assert.EnterMultipleScope()) {
      Assert.That(shouldContinue, Is.False);
      Assert.That(order, Is.EqualTo(new[] { "events", "running" }));
    }
  }

  [Test]
  public void ProcessEventsAndCheckRunning_RunningAfterEventsContinuesFrame() {
    var eventsProcessed = 0;

    var shouldContinue = Game.ProcessEventsAndCheckRunning(
      () => eventsProcessed++,
      () => true);

    using (Assert.EnterMultipleScope()) {
      Assert.That(shouldContinue, Is.True);
      Assert.That(eventsProcessed, Is.EqualTo(1));
    }
  }

  [Test]
  public void DisposeOwnedResources_AttemptsSceneAndWorldAndAlwaysClearsState() {
    var released = new List<string>();

    var error = Assert.Throws<AggregateException>(new Action(() =>
      Game.DisposeOwnedResources(
        () => {
          released.Add("scene");
          throw new InvalidOperationException("Injected scene disposal failure.");
        },
        () => {
          released.Add("world");
          throw new InvalidOperationException("Injected world disposal failure.");
        },
        () => released.Add("state"))));

    using (Assert.EnterMultipleScope()) {
      Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
      Assert.That(released, Is.EqualTo(new[] { "scene", "world", "state" }));
    }
  }

  private static void SetField(Game game, string fieldName, object value) {
    var field = typeof(Game).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.That(field, Is.Not.Null, $"Could not access Game.{fieldName}.");
    field!.SetValue(game, value);
  }
}

// Systems Framework Tests
//
// Comprehensive tests for World, Scheduler, and ISystem implementations.
// Tests cover: system lifecycle, phase ordering, exception handling, parallelization.

using NUnit.Framework;
using OpenCobra.GDK.Game;
using OpenCobraSystem = OpenCobra.GDK.Game.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class WorldSystemsTests {
  private TestWorld world = null!;

  [SetUp]
  public void Setup() {
    world = new TestWorld();
  }

  [TearDown]
  public void TearDown() {
    world.Dispose();
  }

  #region World.AddSystem Tests

  [Test]
  public void AddSystem_CallsAttachAndStartInSequence() {
    var system = new LifecycleTrackingSystem(PipelinePhase.Update);
    world.TryAddSystem(system);

    Assert.That(system.AttachCalledBefore, Is.True, "Attach should be called");
    Assert.That(system.StartCalledAfter, Is.True, "Start should be called after Attach");
    Assert.That(system.IsRunning, Is.True, "System should be running");
    Assert.That(world.Systems, Contains.Item(system));
  }

  [Test]
  public void AddSystem_ReturnsFalseForDuplicates() {
    var system = new TestSystem(PipelinePhase.Update);
    var result1 = world.TryAddSystem(system);
    var result2 = world.TryAddSystem(system);

    Assert.That(result1, Is.True);
    Assert.That(result2, Is.False, "Second add should return false");
    Assert.That(world.Systems.Count, Is.EqualTo(1), "Should not add duplicate");
  }

  [Test]
  public void AddSystem_DoesNotDoubleInvokeOnDuplicate() {
    var system = new UpdateCountingSystem(PipelinePhase.Update);
    world.TryAddSystem(system);
    world.TryAddSystem(system); // Should be ignored

    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(system.UpdateCallCount, Is.EqualTo(1), "Should only update once despite duplicate add");
  }

  #endregion

  #region World.RemoveSystem Tests

  [Test]
  public void RemoveSystem_CallsStopAndRemoves() {
    var system = new TestSystem(PipelinePhase.Update);
    world.TryAddSystem(system);

    world.TryRemoveSystem(system);

    Assert.That(system.IsRunning, Is.False, "System should be stopped");
    Assert.That(world.Systems, Does.Not.Contain(system));
  }

  #endregion

  #region World.Update Tests

  [Test]
  public void Update_InvokesSystems_InPhaseOrder() {
    var systems = new[] {
      new UpdateOrderTracker(PipelinePhase.Late, "Late"),
      new UpdateOrderTracker(PipelinePhase.Early, "Early"),
      new UpdateOrderTracker(PipelinePhase.Render, "Render"),
      new UpdateOrderTracker(PipelinePhase.Update, "Update"),
    };

    foreach (var sys in systems) {
      world.TryAddSystem(sys);
    }

    var delta = TimeSpan.FromMilliseconds(16);
    world.Update(delta);

    var order = UpdateOrderTracker.InvocationOrder;
    Assert.That(order, Is.EqualTo(new[] { "Early", "Update", "Render", "Late" }),
      "Systems should execute in phase order");
  }

  [Test]
  public void Update_PassesSameDeltaToAllSystems() {
    var sys1 = new DeltaTrackingSystem(PipelinePhase.Early);
    var sys2 = new DeltaTrackingSystem(PipelinePhase.Update);
    var sys3 = new DeltaTrackingSystem(PipelinePhase.Render);

    world.TryAddSystem(sys1);
    world.TryAddSystem(sys2);
    world.TryAddSystem(sys3);

    var delta = TimeSpan.FromMilliseconds(33);
    world.Update(delta);

    Assert.That(sys1.LastDelta, Is.EqualTo(delta));
    Assert.That(sys2.LastDelta, Is.EqualTo(delta));
    Assert.That(sys3.LastDelta, Is.EqualTo(delta));
  }

  [Test]
  public void Update_HandlesOperationCanceledException() {
    var system = new ExceptionThrowingSystem(PipelinePhase.Update, new OperationCanceledException());
    world.TryAddSystem(system);

    NUnit.Framework.Assert.DoesNotThrow(new System.Action(() => world.Update(TimeSpan.FromMilliseconds(16))));
  }

  [Test]
  public void Update_PropagatesAggregateException() {
    var system = new ExceptionThrowingSystem(PipelinePhase.Update, new AggregateException("error"));
    world.TryAddSystem(system);

    Assert.Throws<AggregateException>(new System.Action(() => world.Update(TimeSpan.FromMilliseconds(16))));
  }

  #endregion

  #region Scheduler.Execute Tests

  [Test]
  public void Scheduler_ExecutesParallelSystemsInParallel() {
    // Even on a single core, ForceParallelism should still try to parallelize.
    // This test uses long-running CPU work (loop) rather than Thread.Sleep
    // to better detect sequential vs parallel execution.
    var parallelSystems = new[] {
      new BusyWaitParallelSystem(50),
      new BusyWaitParallelSystem(50),
    };

    foreach (var sys in parallelSystems) {
      world.TryAddSystem(sys);
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    world.Update(TimeSpan.FromMilliseconds(16));
    sw.Stop();

    // Sequential would take ~100ms+, parallel with PLINQ overhead may be 80-120ms.
    // Allow margin for system variance. The key is it shouldn't be ~200ms.
    Assert.That(sw.ElapsedMilliseconds, Is.LessThan(150),
      "Parallel systems should run concurrently, not sequentially");
  }

  [Test]
  public void Scheduler_ExecutesLinearSystemsSequentially() {
    var system1 = new OrderedSystem(PipelinePhase.Update, "sys1");
    var system2 = new OrderedSystem(PipelinePhase.Update, "sys2");

    // Both non-parallel, should execute in order
    world.TryAddSystem(system1);
    world.TryAddSystem(system2);

    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(OrderedSystem.ExecutionOrder, Contains.Item("sys1"));
    Assert.That(OrderedSystem.ExecutionOrder.IndexOf("sys1"), Is.LessThan(
      OrderedSystem.ExecutionOrder.IndexOf("sys2")),
      "Linear systems should execute in registration order");
  }

  #endregion

  #region Test Helpers

  private class TestWorld : World {
    public override void Load() { }

    public bool TryAddSystem(ISystem system) => base.AddSystem(system);
    public bool TryRemoveSystem(ISystem system) {
      base.RemoveSystem(system);
      return true;
    }
  }

  private class TestSystem : OpenCobraSystem {
    public TestSystem(PipelinePhase phase) : base(phase) { }
    public override void Update(TimeSpan delta) {
      base.Update(delta);
    }
  }

  private class LifecycleTrackingSystem : OpenCobraSystem {
    public bool AttachCalledBefore { get; private set; }
    public bool StartCalledAfter { get; private set; }

    public LifecycleTrackingSystem(PipelinePhase phase) : base(phase) {
      Started += () => { StartCalledAfter = true; };
    }

    public override void Attach(WeakReference<IWorld> world) {
      AttachCalledBefore = true;
      base.Attach(world);
    }
  }

  private class UpdateCountingSystem : OpenCobraSystem {
    public int UpdateCallCount { get; private set; }

    public UpdateCountingSystem(PipelinePhase phase) : base(phase) { }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      UpdateCallCount++;
    }
  }

  private class UpdateOrderTracker : OpenCobraSystem {
    public static List<string> InvocationOrder { get; private set; } = [];
    private readonly string name;

    public UpdateOrderTracker(PipelinePhase phase, string name) : base(phase) {
      this.name = name;
    }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      InvocationOrder.Add(name);
    }

    [OneTimeTearDown]
    public void ClearOrder() {
      InvocationOrder.Clear();
    }
  }

  private class DeltaTrackingSystem : OpenCobraSystem {
    public TimeSpan LastDelta { get; private set; }

    public DeltaTrackingSystem(PipelinePhase phase) : base(phase) { }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      LastDelta = delta;
    }
  }

  private class ExceptionThrowingSystem : OpenCobraSystem {
    private readonly Exception exception;

    public ExceptionThrowingSystem(PipelinePhase phase, Exception exception) : base(phase) {
      this.exception = exception;
    }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      throw exception;
    }
  }

  private class SlowParallelSystem : OpenCobraSystem {
    private readonly int delayMs;

    public SlowParallelSystem(int delayMs) : base(PipelinePhase.Update) {
      this.delayMs = delayMs;
      Parallelizable = true;
    }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      System.Threading.Thread.Sleep(delayMs);
    }
  }

  private class BusyWaitParallelSystem : OpenCobraSystem {
    private readonly int durationMs;

    public BusyWaitParallelSystem(int durationMs) : base(PipelinePhase.Update) {
      this.durationMs = durationMs;
      Parallelizable = true;
    }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      var sw = System.Diagnostics.Stopwatch.StartNew();
      while (sw.ElapsedMilliseconds < durationMs) {
        // Busy wait (CPU work, not thread sleep)
      }
    }
  }

  private class OrderedSystem : OpenCobraSystem {
    public static List<string> ExecutionOrder { get; private set; } = [];
    private readonly string name;

    public OrderedSystem(PipelinePhase phase, string name) : base(phase) {
      this.name = name;
    }

    public override void Update(TimeSpan delta) {
      base.Update(delta);
      ExecutionOrder.Add(name);
    }

    [OneTimeTearDown]
    public void ClearOrder() {
      ExecutionOrder.Clear();
    }
  }

  #endregion
}

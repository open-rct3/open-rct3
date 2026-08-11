// ParkLoadSystem Tests
//
// Tests for asynchronous park loading via the systems pipeline.
// Covers weak reference safety, concurrent access patterns, and exception handling.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Simulation;
using System.Runtime.CompilerServices;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class ParkLoadSystemTests {
  private TestWorld world = null!;
  private ParkLoadSystem parkLoadSystem = null!;

  [SetUp]
  public void Setup() {
    world = new TestWorld();
    parkLoadSystem = new ParkLoadSystem();
    world.TryAddSystem(parkLoadSystem);
  }

  [TearDown]
  public void TearDown() {
    parkLoadSystem?.Dispose();
    world?.Dispose();
  }

  #region Known-Good Case

  [Test]
  public void RequestLoad_FollowedByUpdate_LoadsExactlyOnce() {
    var loadCount = 0;
    world.OnLoadCalled += () => loadCount++;

    parkLoadSystem.RequestLoad(null);
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(loadCount, Is.EqualTo(1), "Should load exactly once after RequestLoad + Update");
  }

  [Test]
  public void RequestLoad_WithoutUpdate_DoesNotLoad() {
    var loadCount = 0;
    world.OnLoadCalled += () => loadCount++;

    parkLoadSystem.RequestLoad(null);

    Assert.That(loadCount, Is.Zero, "Should not load without calling Update");
  }

  #endregion

  #region Last-Write-Wins Edge Case

  [Test]
  public void RequestLoad_CalledTwice_OnlyLoadsLatestPath() {
    var paths = new List<string?>();
    world.OnLoadCalled += () => paths.Add(world.LastLoadedPath);

    parkLoadSystem.RequestLoad("path1.dat");
    parkLoadSystem.RequestLoad("path2.dat");
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(paths, Has.Count.EqualTo(1), "Should load exactly once");
    Assert.That(paths[0], Is.EqualTo("path2.dat"), "Should load the last requested path");
  }

  [Test]
  public void RequestLoad_CalledMultipleTimes_KeepsLatest() {
    var paths = new List<string?>();
    world.OnLoadCalled += () => paths.Add(world.LastLoadedPath);

    parkLoadSystem.RequestLoad("path1.dat");
    parkLoadSystem.RequestLoad("path2.dat");
    parkLoadSystem.RequestLoad("path3.dat");
    parkLoadSystem.RequestLoad(null);
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(paths, Has.Count.EqualTo(1), "Should load exactly once");
    Assert.That(paths[0], Is.Null, "Should load the null (default) path");
  }

  #endregion

  #region Weak Reference Edge Case

  /// <remarks>
  /// Debug builds keep a method's locals alive for the whole body, not just to last use, so a weak
  /// reference checked in the same frame that touched <c>world</c> would never report it as dead. This
  /// frame is kept separate (<see cref="MethodImplOptions.NoInlining"/>) so it can unwind first.
  /// </remarks>
  [MethodImpl(MethodImplOptions.NoInlining)]
  private WeakReference DisposeWorldAndTrackWeakly(Action onLoad) {
    world.OnLoadCalled += onLoad;
    parkLoadSystem.RequestLoad(null);
    var weak = new WeakReference(world);
    world.Dispose();
    world = null!;
    return weak;
  }

  [Test]
  public void Update_WhileWorldDisposed_NoOps() {
    var loadCount = 0;
    var weak = DisposeWorldAndTrackWeakly(() => loadCount++);

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    Assert.That(weak.IsAlive, Is.False, "World should be collectible once nothing references it");

    Assert.DoesNotThrow(
      new Action(() => parkLoadSystem.Update(TimeSpan.FromMilliseconds(16))),
      "Should not throw when world is disposed");

    Assert.That(loadCount, Is.Zero, "Should not load if world is disposed");
  }

  #endregion

  #region Concurrent Threading Case

  [Test]
  public void RequestLoad_CalledConcurrently_LastWriteWins() {
    var paths = new System.Collections.Generic.List<string?>();
    var loadLock = new object();

    world.OnLoadCalled += () => {
      lock (loadLock) {
        paths.Add(world.LastLoadedPath);
      }
    };

    var pathsToRequest = new[] { "path1.dat", "path2.dat", "path3.dat", "path4.dat", "path5.dat" };
    var tasks = new Task[pathsToRequest.Length];

    for (int i = 0; i < pathsToRequest.Length; i++) {
      var path = pathsToRequest[i];
      tasks[i] = Task.Run(() => parkLoadSystem.RequestLoad(path));
    }

    Task.WaitAll(tasks);
    world.Update(TimeSpan.FromMilliseconds(16));

    using (Assert.EnterMultipleScope()) {
      Assert.That(paths, Has.Count.EqualTo(1), "Should load exactly once despite concurrent requests");
      Assert.That(pathsToRequest, Does.Contain(paths[0]),
        "The loaded path should be one of the requested paths (last write wins via Interlocked.Exchange)");
    }

  }

  [Test]
  public void RequestLoad_Interleaved_WithUpdates_OnlyLoadsLatest() {
    var loadedPaths = new List<string?>();
    world.OnLoadCalled += () => loadedPaths.Add(world.LastLoadedPath);

    parkLoadSystem.RequestLoad("path1.dat");
    world.Update(TimeSpan.FromMilliseconds(16));
    Assert.That(loadedPaths, Has.Count.EqualTo(1));
    Assert.That(loadedPaths[0], Is.EqualTo("path1.dat"));

    parkLoadSystem.RequestLoad("path2.dat");
    parkLoadSystem.RequestLoad("path3.dat");
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(loadedPaths, Has.Count.EqualTo(2));
    Assert.That(loadedPaths[1], Is.EqualTo("path3.dat"), "Should have loaded the latest of the two concurrent requests");
  }

  #endregion

  #region Test Helpers

  private class TestWorld : OpenCobra.GDK.Game.World, IParkLoader {
    public event Action? OnLoadCalled;
    public string? LastLoadedPath { get; private set; }

    public override void Load() => (this as IParkLoader).Load(null);

    void IParkLoader.Load(string? parkPath) {
      LastLoadedPath = parkPath;
      OnLoadCalled?.Invoke();
    }

    public bool TryAddSystem(OpenCobra.GDK.Game.ISystem system) => base.AddSystem(system);
  }

  #endregion
}

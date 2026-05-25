// Scene
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;

namespace OpenCobra.GDK;

public class Scene : IResource, IDisposable {
  public readonly static Container IoC = new();
  public State State { get; private set; } = State.Uninitialized;
  public readonly Camera Camera = new();
  public List<Model> Models { get; } = [];
  public IEnumerable<Model> UninitializedModels =>
    from model in Models
    where model.Mesh.State == State.Uninitialized || model.Material is { State: State.Uninitialized }
    select model;

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="delta">The time since the last update.</param>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void Update(TimeSpan delta, float aspectRatio) {
    Camera.Update(aspectRatio);
  }

  public void Dispose() {
    if (State == State.Disposed) return;

    foreach (var model in Models) model.Dispose();
    Models.Clear();
    GC.SuppressFinalize(this);
    State = State.Disposed;
  }
}

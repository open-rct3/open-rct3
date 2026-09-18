// Scene
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Game;
using OpenCobra.GDK.GUI;

namespace OpenCobra.GDK;

public class Scene : IResource, IDisposable {
  private readonly Platform.IGraphicsSurface? surface;
  private Controller? gui;

  public Scene() : this(
    IGame.IoC.Resolve<Platform.IGraphicsSurface>(),
    IGame.IoC.Resolve<Controller>()) { }

  protected Scene(Platform.IGraphicsSurface? surface, Controller? gui) {
    this.surface = surface;
    this.gui = gui;
  }

  public State State { get; private set; } = State.Uninitialized;

  public readonly Camera Camera = new();
  public readonly ImDraw ImDraw = new();
  public List<Model> Models { get; } = [];
  public List<IWindow> Windows { get; } = [];

  public IEnumerable<Model> UninitializedModels =>
    from model in Models
    where model.Mesh.State == State.Uninitialized || model.Material is { State: State.Uninitialized }
    select model;

  /// <summary>Binds the GUI controller for the surface's current native handle.</summary>
  public void BindGui(Controller replacement) {
    ArgumentNullException.ThrowIfNull(replacement);
    Volatile.Write(ref gui, replacement);
  }

  /// <summary>Detaches a GUI controller before its owning surface disposes it.</summary>
  public void UnbindGui(Controller ownedController) {
    ArgumentNullException.ThrowIfNull(ownedController);
    Interlocked.CompareExchange(ref gui, null, ownedController);
  }

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="delta">The time since the last update.</param>
  ///
  public void Update(TimeSpan delta) {
    ObjectDisposedException.ThrowIf(State == State.Disposed, this);
    if (surface == null)
      throw new InvalidOperationException("Scene services are unavailable.");
    Camera.Update(surface.AspectRatio);
    Volatile.Read(ref gui)?.Update(delta.TotalSeconds);
  }

  public void Dispose() {
    if (State == State.Disposed) return;

    var models = Models.ToArray();
    Models.Clear();
    ImDraw.Dispose();
    GC.SuppressFinalize(this);
    try {
      ResourceDisposal.Run(models.Select<Model, Action>(model => model.Dispose));
    } finally {
      State = State.Disposed;
    }
  }
}

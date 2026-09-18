// Model
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Materials;
using OpenCobra.GDK.Meshes;

namespace OpenCobra.GDK;

public class Model(Mesh mesh) : IDisposable {
  private Mesh? ownedMesh = mesh;
  private Material? material;
  private bool disposed;

  public Mesh Mesh { get; init; } = mesh;
  public Material? Material {
    get => material;
    set {
      ObjectDisposedException.ThrowIf(disposed, this);
      material = value;
    }
  }
  public Transform Transform { get; set; } = new();

  public void Dispose() {
    if (disposed) return;
    var meshResource = ownedMesh;
    var materialResource = material;
    ownedMesh = null;
    material = null;
    disposed = true;
    GC.SuppressFinalize(this);

    var releases = new List<Action>();
    if (meshResource != null) releases.Add(() => DisposeMesh(meshResource));
    if (materialResource != null)
      releases.Add(() => DisposeMaterial(materialResource));
    ResourceDisposal.Run(releases);
  }

  protected virtual void DisposeMesh(Mesh resource) => resource.Dispose();
  protected virtual void DisposeMaterial(Material resource) => resource.Dispose();
}

internal static class ResourceDisposal {
  public static void Run(IEnumerable<Action> releases) {
    var errors = new List<Exception>();
    foreach (var release in releases) {
      try {
        release();
      } catch (Exception error) {
        errors.Add(error);
      }
    }
    if (errors.Count > 0) throw new AggregateException(errors);
  }
}

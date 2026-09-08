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
  public Mesh Mesh { get; init; } = mesh;
  public Material? Material { get; set; }
  public Transform Transform { get; set; } = new();

  public void Dispose() {
    Mesh.Dispose();
    Material?.Dispose();
    GC.SuppressFinalize(this);
  }
}

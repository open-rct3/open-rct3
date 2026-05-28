// ECS Component Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.ECS;

public interface IComponent : IDisposable { }

public readonly struct Component : IComponent {
  public void Dispose() => GC.SuppressFinalize(this);
}

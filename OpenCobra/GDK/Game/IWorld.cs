// IWorld
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Streaming;

namespace OpenCobra.GDK.Game;

public interface IWorld : IDisposable {
  /// <summary>
  /// The current progress of the world loading.
  /// </summary>
  Progress Progress { get; }

  /// <summary>
  /// Systems that operate on components of this world.
  /// </summary>
  IReadOnlyCollection<ISystem> Systems { get; }

  void Load();
}

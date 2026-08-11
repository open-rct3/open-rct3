// Park Loading IoC Interface
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Game;

namespace OpenRCT3.Simulation;

/// <summary>
/// Capability interface for worlds that support loading parks by path.
/// Systems use this focused interface instead of the full World contract.
/// </summary>
public interface IParkLoader : IWorld {
  /// <summary>
  /// Loads a park from the given path (or the default park if path is null).
  /// </summary>
  /// <param name="parkPath">Path to the park save file, or null to load the default park.</param>
  void Load(string? parkPath);
}

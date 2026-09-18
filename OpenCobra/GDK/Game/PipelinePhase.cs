// System Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Game;

public enum PipelinePhase : int {
  /// <summary>
  /// Beginning of the current frame.
  /// </summary>
  /// <remarks>
  /// Use this phase to process input, physics, etc.
  /// </remarks>
  Early = -1,
  /// <summary>
  /// Before rendering the current frame.
  /// </summary>
  /// <remarks>
  /// Use this phase to process game logic
  /// </remarks>
  Update = 0,
  /// <summary>
  /// While rendering the current frame.
  /// </summary>
  /// <remarks>
  /// Use this phase to process rendering, GUI, etc.
  /// </remarks>
  Render = 1,
  /// <summary>
  /// After rendering, but before the next frame.
  /// </summary>
  Late = 2,
}

// GraphicsAPI
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Platform;

public enum GraphicsAPI : uint {
  Unsupported = 0,
  OpenGL,
  /// <remarks>
  /// The macOS Metal backend is a pipe dream.
  /// </remarks>
  Metal,
  /// <remarks>
  /// The WebGPU backend is used in the web client, and is also a pipe dream.
  /// </remarks>
  WebGPU
}

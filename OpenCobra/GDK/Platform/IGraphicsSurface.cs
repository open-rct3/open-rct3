// IGraphicsSurface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;
using Silk.NET.OpenGL;
using System.ComponentModel;

namespace OpenCobra.GDK.Platform;

public delegate void SurfaceCreated(IGraphicsSurface surface, IRenderer renderer);
public delegate void SurfaceChanged(IGraphicsSurface surface);

public interface IGraphicsSurface {
  /// <summary>
  /// Raised when this window has finished creating its GPU surface.
  /// </summary>
  event SurfaceCreated? SurfaceCreated;

  /// <summary>
  /// Raised whenever the backing GPU surface changes, e.g. when the frame-buffer is resized.
  /// </summary>
  event SurfaceChanged? SurfaceChanged;

  [Category("GPU")]
  ISurfaceSettings Settings { get; }

  /// <summary>
  /// Whether this graphics surface is valid.
  /// </summary>
  /// <value>
  /// <c>true</c> when the surface has finished initializing its underlying GPU
  /// resources, <c>false</c> otherwise
  /// </value>
  [Category("GPU")]
  bool IsValid { get; }

  [Category("GPU")]
  Size FrameBufferSize { get; }

  [Category("Behavior")]
  float AspectRatio { get; }
}

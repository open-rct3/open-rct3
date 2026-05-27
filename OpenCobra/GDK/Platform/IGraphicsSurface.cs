// IGraphicsSurface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;
using System.ComponentModel;

namespace OpenCobra.GDK.Platform;

public delegate void SurfaceCreated(IGraphicsSurface surface, IRenderer renderer);
public delegate void SurfaceChanged(IGraphicsSurface surface);

public interface IGraphicsSurface {
  /// <summary>
  /// Raised when this window has finished creating its GPU surface.
  /// </summary>
  public event SurfaceCreated? SurfaceCreated;

  /// <summary>
  /// Raised whenever the backing GPU surface changes, e.g. when the framebuffer is resized.
  /// </summary>
  public event SurfaceChanged? SurfaceChanged;

  [Browsable(false)]
  IRenderer Renderer { get; }

  [Category("GPU")]
  ISurfaceSettings Settings { get; }

  /// <summary>
  /// Whether this graphics surface is valid.
  /// </summary>
  /// <remarks>
  /// Generally, this is <c>true</c> when the surface has finished initializing its underlying GPU resources.
  /// </remarks>
  [Category("GPU")]
  public bool IsValid { get; }

  /// <summary>
  /// A safe handle to the underlying native GPU surface.
  /// </summary>
  [Browsable(false)]
  public Handle<IGraphicsSurface> Surface { get; }

  [Category("GPU")]
  Size FrameBufferSize { get; }

  [Category("Behavior")]
  float AspectRatio { get; }
}

// IGraphicsSurface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

// ReSharper disable InconsistentNaming
using OpenRCT3.OpenGL;

namespace OpenRCT3.Platforms;

public delegate void SurfaceCreated(IGraphicsSurface surface, IRenderer renderer);
public delegate void SurfaceChanged(IGraphicsSurface surface);

public interface IGraphicsSurface {
  IRenderer Renderer { get; }
  SurfaceSettings Settings { get; }
  /// <summary>
  /// Whether this graphics surface is valid.
  /// </summary>
  /// <remarks>
  /// Generally, this is <c>true</c> when the surface has finished initializing its underlying GPU resources.
  /// </remarks>
  public bool IsValid { get; }
  /// <summary>
  /// A safe handle to the underlying native GPU surface.
  /// </summary>
  public Handle<nint> Surface { get; }
  /// <summary>
  /// Raised when this window has finished creating its GPU surface.
  /// </summary>
  /// <remarks>
  /// It is safe to start the game only <i>after</i> this event.
  /// </remarks>
  public event SurfaceCreated? SurfaceCreated;
  /// <summary>
  /// Raised whenever the backing GPU surface changes, e.g. when the framebuffer is resized.
  /// </summary>
  public event SurfaceChanged? SurfaceChanged;
}

public enum GraphicsAPI {
  Unsupported,
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

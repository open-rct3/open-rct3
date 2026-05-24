// IGraphicsSurface
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

// ReSharper disable InconsistentNaming
namespace OpenRCT3.Platforms;

public interface IGraphicsSurface {
  IRenderer Renderer { get; }
  SurfaceSettings Settings { get; }
  public OpenGLSurface Surface { get; }
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

public sealed class OpenGLSurface(
  nint handle, bool ownsHandle, Func<bool>? disposeHandle = null
) : Handle<nint>(handle, ownsHandle, disposeHandle) { }

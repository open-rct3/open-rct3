// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms;

public record struct Dpi(float X, float Y);

public delegate void SurfaceChanged(OpenGLSurface surface);

public interface IWindow {
  public string Title { get; set; }
  public Dpi Dpi { get; }
  public Size FrameBufferSize { get; }
  /// <summary>
  /// Raised whenever the backing GPU surface changes, e.g. when the framebuffer is resized.
  /// </summary>
  public event SurfaceChanged? SurfaceChanged;
}

public abstract class Handle<T>(
  nint handle, bool ownsHandle, Func<bool>? disposeHandle = null
) : SafeHandle(handle, ownsHandle) {
  private readonly Func<bool>? disposeHandle = disposeHandle;

  public override bool IsInvalid => this.handle == 0;

  protected override bool ReleaseHandle() {
    if (disposeHandle != null) return disposeHandle();
    else return true;
  }
}

// OpenGL Surface Handle
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public sealed class OpenGLSurface(
  nint handle, bool ownsHandle, Func<bool>? disposeHandle = null
) : Handle<nint>(handle, ownsHandle, disposeHandle) { }

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

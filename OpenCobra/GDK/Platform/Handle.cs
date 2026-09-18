// Handle
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Runtime.InteropServices;

namespace OpenCobra.GDK.Platform;

/// <summary>
/// Managed handle to an unmanaged pointer.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="handle"></param>
/// <param name="ownsHandle">Whether the caller owns the underlying reference</param>
/// <param name="disposeHandle">Delegate called when this handle is released</param>
public sealed class Handle<T>(
  nint handle, bool ownsHandle, Func<bool>? disposeHandle = null
) : SafeHandle(handle, ownsHandle) {
  private readonly Func<bool>? disposeHandle = disposeHandle;

  public override bool IsInvalid => this.handle == 0;

  protected override bool ReleaseHandle() {
    if (disposeHandle != null) return disposeHandle();
    else return true;
  }
}

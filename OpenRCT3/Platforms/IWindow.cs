// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms;

public struct Dpi(float x, float y) {
  public float X = x;
  public float Y = y;
}

public interface IWindow : IObservable<OpenGLSurface> {
  public string Title { get; set; }
  public Dpi Dpi { get; }
  public Size FrameBufferSize { get; }
}

public abstract class Handle<T> : SafeHandle {
  private readonly Func<bool>? _disposeHandle;

  public Handle(nint handle, bool ownsHandle, Func<bool>? disposeHandle = null) : base((nint)handle, ownsHandle) {
    _disposeHandle = disposeHandle;
  }

  public override bool IsInvalid => this.handle == 0;

  protected override bool ReleaseHandle() {
    if (_disposeHandle != null) return _disposeHandle();
    else return true;
  }
}

public sealed class OpenGLSurface : Handle<nint> {
  public OpenGLSurface(nint handle, bool ownsHandle, Func<bool>? disposeHandle = null) : base(handle, ownsHandle, disposeHandle) { }
}

public delegate void SurfaceChanged(OpenGLSurface surface);

internal class SurfaceSubscription : IDisposable {
  private readonly List<IObserver<OpenGLSurface>> _observers;
  private readonly IObserver<OpenGLSurface> observer;

  public SurfaceSubscription(List<IObserver<OpenGLSurface>> observers, IObserver<OpenGLSurface> observer) {
    this._observers = observers;
    this.observer = observer;
  }

  public void Dispose() {
    if (observer != null && _observers.Contains(observer))
      _observers.Remove(observer);
  }
}

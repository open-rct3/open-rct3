// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using Silk.NET.WebGPU;
using System;
using System.Runtime.InteropServices;

namespace OpenRCT3.Platforms;

public interface IWindow : IObservable<Surface> {
  public string Title { get; set; }
  public uint FrameBufferWidth { get; }
  public uint FrameBufferHeight { get; }
}

public sealed class Surface : SafeHandle {
  public Surface(nint handle, bool ownsHandle) : base(handle, ownsHandle) { }

  public SurfaceDescriptor Descriptor { get; set; }
  public override bool IsInvalid => this.handle == 0;

  protected override bool ReleaseHandle() {
    // QUESTION: WebGPU releases the surface handle for us?
    return true;
  }
}

public delegate void SurfaceChanged(Surface surface);

internal class SurfaceSubscription : IDisposable {
  private readonly List<IObserver<Surface>> _observers;
  private readonly IObserver<Surface> observer;

  public SurfaceSubscription(List<IObserver<Surface>> observers, IObserver<Surface> observer) {
    this._observers = observers;
    this.observer = observer;
  }

  public void Dispose() {
    if (observer != null && _observers.Contains(observer))
      _observers.Remove(observer);
  }
}

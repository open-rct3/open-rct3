// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using AppKit;
using CoreAnimation;
using ObjCRuntime;

namespace OpenRCT3.Platforms.macOS {
  public partial class MainWindow : NSWindow, IWindow {
    private List<IObserver<Surface>> observers = new();

    public MainWindow(NativeHandle handle) : base(handle) {
      this.MakeKeyAndOrderFront(this);
    }

    public uint FrameBufferWidth => (uint) Math.Round(
      Controller?.Game.Bounds.Width.Value * BackingScaleFactor.Value ?? 640
    );
    public uint FrameBufferHeight => (uint) Math.Round(
      Controller?.Game.Bounds.Height.Value * BackingScaleFactor.Value ?? 420
    );

    private GameViewController? Controller => ContentViewController as GameViewController;

    public override void AwakeFromNib() {
      base.AwakeFromNib();

      if (Controller != null) Controller.SurfaceChanged += SurfaceChanged;
    }

    private void SurfaceChanged(Surface surface) {
      foreach (var observer in observers) observer.OnNext(surface);
    }

    public IDisposable Subscribe(IObserver<Surface> observer) {
      if (!observers.Contains(observer)) observers.Add(observer);
      return new SurfaceSubscription(observers, observer);
    }
  }
}

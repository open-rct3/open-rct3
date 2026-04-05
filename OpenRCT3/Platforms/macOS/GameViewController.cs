// GameViewController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System;
using Foundation;
using AppKit;
using ObjCRuntime;
using CoreAnimation;

using OpenRCT3.ViewModels;

namespace OpenRCT3.Platforms.macOS;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class GameViewController(NativeHandle handle) : NSViewController(handle), IPlatformInspector {
  public event SurfaceChanged? SurfaceChanged;

  public NSView Game => this.game;

  public override void AwakeFromNib() {
    base.AwakeFromNib();

    this.inspector.LoadRequest(new NSUrlRequest(new NSUrl("https://google.com")));

    this.game.WantsLayer = true;
    this.game.Layer = new OpenGlLayer();

    var surface = new OpenGLSurface(this.game.Layer.Handle, true);
    SurfaceChanged?.Invoke(surface);

    // TODO: Update framebuffer on WillResize/DidResize, DidChangeScreen, and DidChangeScreenProfile
    // See also DidEndLiveResize
  }

  public IDisposable Subscribe(IObserver<Inspector> observer) {
    throw new NotImplementedException();
  }
}

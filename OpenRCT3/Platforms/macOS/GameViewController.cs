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

using OpenRCT3.ViewModels;

namespace OpenRCT3.Platforms.macOS;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class GameViewController(NativeHandle handle) : NSViewController(handle), IPlatformInspector {
  public event SurfaceChanged? SurfaceChanged;

  public NSView Game => this.game;

  public override void AwakeFromNib() {
    base.AwakeFromNib();

    this.inspector.LoadRequest(new NSUrlRequest(new NSUrl("https://google.com")));

    var attrs = new[] {
      NSOpenGLPixelFormatAttribute.Accelerated,
      NSOpenGLPixelFormatAttribute.DoubleBuffer,
      NSOpenGLPixelFormatAttribute.ColorSize, (NSOpenGLPixelFormatAttribute)24,
      NSOpenGLPixelFormatAttribute.DepthSize, (NSOpenGLPixelFormatAttribute)24,
      (NSOpenGLPixelFormatAttribute)0
    };
    var pixelFormat = new NSOpenGLPixelFormat(attrs);
    var gameView = new GameView(this.game.Bounds, pixelFormat);
    gameView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
    this.game.AddSubview(gameView);

    if (gameView.OpenGLContext != null) {
      var surface = new OpenGLSurface(gameView.OpenGLContext.CGLContext.Handle, true);
      SurfaceChanged?.Invoke(surface);
    }

    // TODO: Update framebuffer on WillResize/DidResize, DidChangeScreen, and DidChangeScreenProfile
    // See also DidEndLiveResize
  }

  public IDisposable Subscribe(IObserver<Inspector> observer) {
    throw new NotImplementedException();
  }
}

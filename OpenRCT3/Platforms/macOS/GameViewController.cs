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
using Silk.NET.WebGPU;

namespace OpenRCT3.Platforms.macOS;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class GameViewController(NativeHandle handle) : NSViewController(handle), IPlatformInspector {
  public event SurfaceChanged? SurfaceChanged;

  public NSView Game => this.game;

  public override unsafe void AwakeFromNib() {
    base.AwakeFromNib();

    this.inspector.LoadRequest(new NSUrlRequest(new NSUrl("https://google.com")));

    this.game.WantsLayer = true;
    // Do NOT simplify this to `CALayer`. WebGPU requires a Metal layer.
    // ReSharper disable once AccessToStaticMemberViaDerivedType
    this.game.Layer = CAMetalLayer.Create();

    var metalDesc = new SurfaceDescriptorFromMetalLayer(
      new ChainedStruct { Next = null, SType = SType.SurfaceDescriptorFromMetalLayer },
      (void*) (this.game.Layer as INativeObject).Handle
    );
    var surface = new Surface((nint) (&metalDesc), true) {
      Descriptor = new SurfaceDescriptor(&metalDesc.Chain)
    };

    SurfaceChanged?.Invoke(surface);

    // TODO: Update framebuffer on WillResize/DidResize, DidChangeScreen, and DidChangeScreenProfile
    // See also DidEndLiveResize
  }

  public IDisposable Subscribe(IObserver<Inspector> observer) {
    throw new NotImplementedException();
  }
}

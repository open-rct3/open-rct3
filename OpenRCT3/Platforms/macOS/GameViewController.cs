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
using Silk.NET.WebGPU;

namespace OpenRCT3.Platforms.macOS;

public partial class GameViewController : NSViewController {
  public GameViewController(NativeHandle handle) : base(handle) { }

  public event SurfaceChanged? SurfaceChanged;

  public NSView Game => this.game;

  public override unsafe void AwakeFromNib() {
    base.AwakeFromNib();

    // TODO: https://github.com/dagronf/DSFInspectorPanes in `this.inspector`.

    // Do NOT simplify this to `CALayer`. WebGPU requires a Metal layer.
    this.game.WantsLayer = true;
    this.game.Layer = CAMetalLayer.Create();

    var metalDesc = new SurfaceDescriptorFromMetalLayer(
      new ChainedStruct { Next = null, SType = SType.SurfaceDescriptorFromMetalLayer},
      (void*) (this.game.Layer as INativeObject).Handle
    );
    var surface = new Surface((nint) (&metalDesc), true) {
      Descriptor = new SurfaceDescriptor(&metalDesc.Chain)
    };

    SurfaceChanged?.Invoke(surface);

    // TODO: Update framebuffer on WillResize/DidResize, DidChangeScreen, and DidChangeScreenProfile
    // See also DidEndLiveResize
  }
}

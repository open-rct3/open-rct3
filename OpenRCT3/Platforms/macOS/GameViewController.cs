// GameViewController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System;
using Foundation;
using AppKit;
using ObjCRuntime;
using CoreAnimation;
using OpenRCT3.OpenGL;
using OpenRCT3.ViewModels;

namespace OpenRCT3.Platforms.macOS;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class GameViewController(NativeHandle handle) : NSViewController(handle) {

  public NSView Game => game;
  public IGraphicsSurface Surface => game.Layer as OpenGLLayer
    ?? throw new InvalidOperationException("Surface is not an OpenGLLayer!");

  public override void AwakeFromNib() {
    base.AwakeFromNib();

    inspector.LoadRequest(new NSUrlRequest(new NSUrl("https://google.com")));

    game.WantsLayer = true;
    game.Layer = new OpenGLLayer();
  }
}

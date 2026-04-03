// GameView
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using AppKit;
using CoreGraphics;
using OpenTK.Graphics.OpenGL;

namespace OpenRCT3.Platforms.macOS;

public class GameView : NSOpenGLView {
  public GameView(CGRect frame, NSOpenGLPixelFormat format) : base(frame, format) { }

  public override void DrawRect(CGRect dirtyRect) {
    var ctx = OpenGLContext;
    if (ctx == null) return;
    ctx.MakeCurrentContext();
    GL.ClearColor(0.392f, 0.584f, 0.929f, 1f); // CornflowerBlue
    GL.Clear(ClearBufferMask.ColorBufferBit);
    ctx.FlushBuffer();
  }
}

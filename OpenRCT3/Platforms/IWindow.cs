// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using System.Drawing;

namespace OpenRCT3.Platforms;

public record struct Dpi(float X, float Y);

public interface IWindow {
  [Category("Behavior")]
  public string Title { get; set; }
  [Category("Behavior")]
  public Dpi Dpi { get; }
  [Category("GPU")]
  public Size FrameBufferSize { get; }
}

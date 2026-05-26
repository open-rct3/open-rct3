// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using Silk.NET.Windowing;

namespace OpenRCT3.Platforms;

public record struct Dpi(float X, float Y);

public interface IWindow : IView {
  [Category("Behavior")]
  public string Title { get; set; }
  [Category("Behavior")]
  public Dpi Dpi { get; }

  /// <summary>
  /// Start the game.
  /// </summary>
  public void Start();
}

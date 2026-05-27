// IWindow
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace OpenCobra.GDK.Game;

public record struct Dpi(float X, float Y);

/// <summary>
/// Represents a generic window, an abstraction over the host window used by the game runtime.
/// </summary>
/// <remarks>
/// <para>Implementations are expected to:</para>
/// - Expose window/view state via <see cref="IView"/> members,
/// - Expose input devices/events via <see cref="IInputPlatform"/> members, and
/// - Create and manage the application's <c>Game</c> instance when <see cref="Start"/> is called.
/// </remarks>
public interface IWindow : IView, IInputPlatform {
  [Category("Behavior")]
  public string Title { get; set; }
  [Category("Behavior")]
  // FIXME: The intent here is to supply the window's display's pixel density, not neccisarily the dots-per-inch
  public Dpi Dpi { get; }

  /// <summary>
  /// Start the game.
  /// </summary>
  /// <remarks>
  /// Implementations are expected to create and manage the <c>Game</c> instance.
  /// </remarks>
  // FIXME: Extract this into a platform-idependent base-class.
  public void Start();
}

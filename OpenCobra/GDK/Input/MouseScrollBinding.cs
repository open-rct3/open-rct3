// Mouse Scroll Binding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>Which way a scroll wheel tick moved.</summary>
public enum ScrollDirection {
  Up,
  Down
}

/// <summary>
/// Binds an <see cref="InputActionMap"/> action to a mouse scroll wheel tick in <see cref="Direction"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="KeyboardBinding"/>/<see cref="MouseBinding"/>, a scroll tick is a discrete event
/// with no "held" state, so <see cref="IsActive"/> always reports not-active - <see cref="InputActionMap"/>
/// raises <see cref="InputActionMap.Scrolled"/> (never <see cref="InputActionMap.Pressed"/>/
/// <see cref="InputActionMap.Released"/>) for scroll-bound actions, carrying the wheel's raw tick
/// magnitude alongside the action name.
/// </remarks>
public sealed record MouseScrollBinding(ScrollDirection Direction) : IInputBinding {
  public bool IsActive(IInputContext context) => false;
}

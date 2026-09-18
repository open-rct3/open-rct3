// Mouse Chord Binding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>
/// Binds an <see cref="InputActionMap"/> action to two mouse buttons held down simultaneously (e.g. RMB
/// + LMB together for an alternate camera-rotate gesture).
/// </summary>
/// <remarks>
/// Only <see cref="IsActive"/> polling is meaningful for a chord - <see cref="InputActionMap"/>'s
/// <c>Pressed</c>/<c>Released</c> dispatch only looks at <see cref="MouseBinding"/>, so a chord-only
/// binding never raises those events, same as <see cref="MouseScrollBinding"/>.
/// </remarks>
public sealed record MouseChordBinding(MouseButton Primary, MouseButton Secondary) : IInputBinding {
  public bool IsActive(IInputContext context) {
    foreach (var mouse in context.Mice) {
      if (mouse.IsButtonPressed(Primary) && mouse.IsButtonPressed(Secondary)) return true;
    }
    return false;
  }
}

// IInputBinding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>
/// A single device-level binding (one keyboard key, one mouse button, etc.) that an
/// <see cref="InputActionMap"/> action can resolve against live <see cref="IInputContext"/> state.
/// </summary>
public interface IInputBinding {
  /// <summary>Whether this binding is currently held down on any device in <paramref name="context"/>.</summary>
  bool IsActive(IInputContext context);
}

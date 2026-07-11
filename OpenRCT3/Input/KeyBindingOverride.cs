// Key Binding Override
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Input;
using Silk.NET.Input;

namespace OpenRCT3.Input;

/// <summary>
/// JSON-serializable form of an <see cref="IInputBinding"/>, for persisting user key-rebinds in
/// <see cref="Platforms.AppConfig"/> (which cannot serialize the polymorphic <see cref="IInputBinding"/>
/// hierarchy directly).
/// </summary>
public readonly record struct KeyBindingOverride(string Kind, string Value, KeyModifiers Modifiers = KeyModifiers.None) {
  private const string KeyboardKind = "Keyboard";
  private const string MouseKind = "Mouse";

  public static KeyBindingOverride From(IInputBinding binding) => binding switch {
    KeyboardBinding keyboardBinding => new(KeyboardKind, keyboardBinding.Key.ToString(), keyboardBinding.Modifiers),
    MouseBinding mouseBinding => new(MouseKind, mouseBinding.Button.ToString()),
    _ => throw new NotSupportedException($"Unsupported binding type: {binding.GetType()}"),
  };

  public IInputBinding ToBinding() => Kind switch {
    KeyboardKind => new KeyboardBinding(Enum.Parse<Key>(Value), Modifiers),
    MouseKind => new MouseBinding(Enum.Parse<MouseButton>(Value)),
    _ => throw new NotSupportedException($"Unsupported binding kind: {Kind}"),
  };
}

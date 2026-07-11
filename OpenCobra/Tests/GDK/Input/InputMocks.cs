// Input Test Doubles
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using Silk.NET.Input;

namespace OpenCobra.Tests.GDK.Input;

/// <summary>
/// A minimal hand-rolled <see cref="IKeyboard"/> for driving <see cref="OpenCobra.GDK.Input.InputActionMap"/>
/// and <see cref="OpenCobra.GDK.Input.KeyboardBinding"/> tests without a real windowing/HID backend.
/// </summary>
internal sealed class FakeKeyboard : IKeyboard {
  private readonly HashSet<Key> pressed = [];

  public event Action<IKeyboard, Key, int>? KeyDown;
  public event Action<IKeyboard, Key, int>? KeyUp;
  public event Action<IKeyboard, char>? KeyChar;

  public string Name => "Fake Keyboard";
  public int Index => 0;
  public bool IsConnected => true;
  public IReadOnlyList<Key> SupportedKeys => [];
  public string ClipboardText { get; set; } = "";

  public void BeginInput() {}
  public void EndInput() {}
  public bool IsKeyPressed(Key key) => pressed.Contains(key);
  public bool IsScancodePressed(int scancode) => false;

  public void Press(Key key) {
    pressed.Add(key);
    KeyDown?.Invoke(this, key, 0);
  }

  public void Release(Key key) {
    pressed.Remove(key);
    KeyUp?.Invoke(this, key, 0);
  }
}

/// <summary>
/// A minimal hand-rolled <see cref="IMouse"/> for driving <see cref="OpenCobra.GDK.Input.InputActionMap"/>,
/// <see cref="OpenCobra.GDK.Input.MouseBinding"/>, and <see cref="OpenCobra.GDK.Input.MouseScrollBinding"/>
/// tests without a real windowing/HID backend.
/// </summary>
internal sealed class FakeMouse : IMouse {
  private readonly HashSet<MouseButton> pressed = [];

  public event Action<IMouse, MouseButton>? MouseDown;
  public event Action<IMouse, MouseButton>? MouseUp;
  public event Action<IMouse, MouseButton, Vector2>? Click;
  public event Action<IMouse, MouseButton, Vector2>? DoubleClick;
  public event Action<IMouse, Vector2>? MouseMove;
  public event Action<IMouse, ScrollWheel>? Scroll;

  public string Name => "Fake Mouse";
  public int Index => 0;
  public bool IsConnected => true;
  public IReadOnlyList<MouseButton> SupportedButtons => [];
  public IReadOnlyList<ScrollWheel> ScrollWheels => [];
  public Vector2 Position { get; set; }
  public ICursor Cursor => throw new NotImplementedException();
  public int DoubleClickTime { get; set; }
  public int DoubleClickRange { get; set; }

  public bool IsButtonPressed(MouseButton button) => pressed.Contains(button);

  public void Press(MouseButton button) {
    pressed.Add(button);
    MouseDown?.Invoke(this, button);
  }

  public void Release(MouseButton button) {
    pressed.Remove(button);
    MouseUp?.Invoke(this, button);
  }

  public void ScrollBy(float y) => Scroll?.Invoke(this, new ScrollWheel(0, y));
}

/// <summary>
/// A minimal hand-rolled <see cref="IInputContext"/> exposing exactly one <see cref="FakeKeyboard"/> and
/// one <see cref="FakeMouse"/>, matching how a real single-keyboard/single-mouse desktop session looks to
/// <see cref="OpenCobra.GDK.Input.InputActionMap"/>.
/// </summary>
internal sealed class FakeInputContext : IInputContext {
  public FakeKeyboard Keyboard { get; } = new();
  public FakeMouse Mouse { get; } = new();

  public nint Handle => 0;
  public IReadOnlyList<IGamepad> Gamepads => [];
  public IReadOnlyList<IJoystick> Joysticks => [];
  public IReadOnlyList<IKeyboard> Keyboards => [Keyboard];
  public IReadOnlyList<IMouse> Mice => [Mouse];
  public IReadOnlyList<IInputDevice> OtherDevices => [];

  public event Action<IInputDevice, bool>? ConnectionChanged;

  public void Dispose() {}
}

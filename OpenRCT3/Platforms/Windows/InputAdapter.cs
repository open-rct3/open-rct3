// Windows Forms Input Adapter
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using HidSharp;
using HidSharp.Reports;
using NLog;
using OpenRCT3.Platforms.Input;
using Silk.NET.Input;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using System.Xml.Linq;

namespace OpenRCT3.Platforms.Windows;

internal readonly record struct Device(
  string DeviceId,
  int VendorId,
  int ProductId,
  string Name,
  string Manufacturer,
  uint[] Usages
);

internal interface IHidDevice : IInputDevice {
  Device Device { get; }
}

internal class HidDevice(int index, Device device) : IHidDevice {
  public Device Device { get; } = device;

  public string Name => Device.Name;

  public int Index { get; } = index;

  // FIXME: Detect disconnections from HidSharp
  public bool IsConnected { get; } = true;
}

public class InputAdapter : InputContext {
  private readonly Device[] devices;

  [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "Explicitness for clarity")]
  public InputAdapter(Control control) : base(control.Handle) {
    devices = GetDevices().ToArray();

    // Detect connected input devices
    var keyboards =
      from device in this.devices
      where device.Usages.Contains((uint)Usage.GenericDesktopKeyboard)
      select new KeyboardAdapter(this.devices.IndexOf(device), device, control);
    var mice =
      from device in this.devices
      where device.Usages.Contains((uint)Usage.GenericDesktopMouse)
      select new MouseAdapter(this.devices.IndexOf(device), device, control);
    var gamepads =
      from device in this.devices
      where device.Usages.Contains((uint)Usage.GenericDesktopGamepad)
      select new GamepadAdapter(this.devices.IndexOf(device), device);

    this.keyboards.AddRange(keyboards);
    this.mice.AddRange(mice);
    this.gamepads.AddRange(gamepads);
  }

  //private IKeyboard? PrimaryKeyboard => devices.FirstOrDefault(dev => dev.Usages.Contains((uint)Usage.GenericDesktopKeyboard));
  //private IMouse? PrimaryMouse => devices.FirstOrDefault(dev => dev.Usages.Contains((uint)Usage.GenericDesktopMouse));
  //private IGamepad? PrimaryGamepad => devices.FirstOrDefault(dev => dev.Usages.Contains((uint)Usage.GenericDesktopGamepad));

  private static IEnumerable<Device> GetDevices() => DeviceList.Local.GetHidDevices()
    .Select(dev => dev.ToDevice(dev.GetDevices()))
    .Where(dev => dev != null).Cast<Device>();

  private class KeyboardAdapter : HidDevice, IKeyboard {
    private PressedKey[] pressedKeys = [];

    public event Action<IKeyboard, Key, int>? KeyDown;
    public event Action<IKeyboard, Key, int>? KeyUp;
    public event Action<IKeyboard, char>? KeyChar;

    public KeyboardAdapter(int index, Device device, Control control) : base(index, device) {
      control.KeyDown += OnKeyDown;
      control.KeyUp += OnKeyUp;
      control.KeyPress += KeyPress;
    }

    public IReadOnlyList<Key> SupportedKeys => [.. Enum.GetValues<Keys>().Cast<Key>()];
    public string ClipboardText {
      get => Clipboard.GetText();
      set => Clipboard.SetText(value);
    }

    private void OnKeyDown(object? _, KeyEventArgs e) {
      pressedKeys = [.. pressedKeys, new PressedKey((Key)e.KeyCode, e.KeyValue)];
      KeyDown?.Invoke(this, (Key)e.KeyCode, e.KeyValue);
    }

    private void OnKeyUp(object? _, KeyEventArgs e) {
      pressedKeys = [.. pressedKeys.Where(k => k.Key != (Key)e.KeyCode)];
      KeyUp?.Invoke(this, (Key)e.KeyCode, e.KeyValue);
    }

    private void KeyPress(object? _, KeyPressEventArgs e) => KeyChar?.Invoke(this, e.KeyChar);

    public void BeginInput() {}
    public void EndInput() {}
    public bool IsKeyPressed(Key key) => pressedKeys.Select(k => k.Key).Contains(key);
    public bool IsScancodePressed(int scancode) => pressedKeys.Select(k => k.ScanCode).Contains(scancode);
  }

  private class MouseAdapter : HidDevice, IMouse {
    private MouseState mouse = new();
    private MouseButton? lastClickedButton = null;

    public event Action<IMouse, MouseButton> MouseDown;
    public event Action<IMouse, MouseButton> MouseUp;
    public event Action<IMouse, MouseButton, Vector2> Click;
    public event Action<IMouse, MouseButton, Vector2> DoubleClick;
    public event Action<IMouse, Vector2> MouseMove;
    public event Action<IMouse, ScrollWheel> Scroll;

#pragma warning disable CS8618 // Silk.Net Bug: Mouse events are not nullable
    public MouseAdapter(int index, Device device, Control control) : base(index, device) {
      control.MouseDown += (_, e) => {
        mouse = mouse.WithButton(e.Button.ToMouseButton(), true);
        lastClickedButton = e.Button.ToMouseButton();
        MouseDown?.Invoke(this, e.Button.ToMouseButton());
      };
      control.MouseUp += (_, e) => {
        mouse = mouse.WithButton(e.Button.ToMouseButton(), false);
        lastClickedButton = null;
        MouseUp?.Invoke(this, e.Button.ToMouseButton());
      };
      control.Click += (_, e) => Click?.Invoke(this, lastClickedButton!.Value, mouse.Position);
      control.DoubleClick += (_, e) => DoubleClick?.Invoke(this, lastClickedButton!.Value, mouse.Position);
      control.MouseMove += (_, e) => {
        mouse = mouse.WithPosition(new(e.Location.X, e.Location.Y));
        MouseMove?.Invoke(this, mouse.Position);
      };
      // FIXME: Get the real scroll deltas from the Win32 API
      control.MouseWheel += (_, e) => Scroll?.Invoke(this, new ScrollWheel(0, e.Delta));
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor.

    // FIXME: Get the supported buttons via the Win32 API
    public IReadOnlyList<MouseButton> SupportedButtons => [
      MouseButton.Left,
      MouseButton.Middle,
      MouseButton.Right
    ];
    public IReadOnlyList<ScrollWheel> ScrollWheels => [new ScrollWheel(0, 0)];

    public Vector2 Position {
      get => mouse.Position;
      // FIXME: Set the position via the Win32 API
      set {}
    }
    public ICursor Cursor => throw new NotImplementedException();

    public int DoubleClickTime {
      get => SystemInformation.DoubleClickTime;
      set {}
    }
    public int DoubleClickRange {
      get {
        var size = SystemInformation.DoubleClickSize;
        return Math.Max(size.Width, size.Height);
      }
      set {}
    }

    public bool IsButtonPressed(MouseButton btn) => mouse.PressedButtons.Contains(btn);
  }

  private class GamepadAdapter : HidDevice, IGamepad {
    public event Action<IGamepad, Silk.NET.Input.Button>? ButtonDown;
    public event Action<IGamepad, Silk.NET.Input.Button>? ButtonUp;
    public event Action<IGamepad, Thumbstick>? ThumbstickMoved;
    public event Action<IGamepad, Trigger>? TriggerMoved;

    public GamepadAdapter(int index, Device device) : base(index, device) { }

    public IReadOnlyList<Silk.NET.Input.Button> Buttons => throw new NotImplementedException();
    public IReadOnlyList<Thumbstick> Thumbsticks => throw new NotImplementedException();

    public IReadOnlyList<Trigger> Triggers => throw new NotImplementedException();

    public IReadOnlyList<IMotor> VibrationMotors => throw new NotImplementedException();

    public Deadzone Deadzone { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
  }
}

internal static class HidDeviceExtensions {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  internal static IList<DeviceItem> GetDevices(this HidSharp.HidDevice device) {
    try {
      return device.GetReportDescriptor().DeviceItems;
    } catch (NotSupportedException) {
      return Array.Empty<DeviceItem>();
    }
  }

  /// <summary>
  /// Try to get the details of a USB HID <paramref name="device"/>.
  /// </summary>
  /// <param name="device"></param>
  /// <param name="items">The HID reports for the given <paramref name="device"/></param>
  [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "Explicitness for clarity")]
  internal static Device? ToDevice(this HidSharp.HidDevice device, IList<DeviceItem> items) {
    var deviceName = device.GetFriendlyName();

    try {
      return new(
        device.DevicePath,
        device.VendorID,
        device.ProductID,
        deviceName,
        device.GetManufacturer(),
        items.SelectMany(item => item.Usages.GetAllValues()).ToArray()
      );
    } catch (Exception e) {
      logger.Warn($"Could not determine USB HID class for {{device}}: {e.Message}", deviceName.Trim());
      return null;
    }
  }
}

internal static class MouseButtonsExtensions {
  internal static MouseButton ToMouseButton(this MouseButtons btn) => btn switch {
    MouseButtons.Left => MouseButton.Left,
    MouseButtons.Right => MouseButton.Right,
    MouseButtons.Middle => MouseButton.Middle,
    _ => throw new NotImplementedException(),
  };
}

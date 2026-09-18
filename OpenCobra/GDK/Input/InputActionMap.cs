// Input Action Map
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Input;

namespace OpenCobra.GDK.Input;

/// <summary>
/// Resolves string-named, game-defined input actions (e.g. "rotate-map-left") against rebindable
/// <see cref="IInputBinding"/>s, polling or eventing off a live <see cref="IInputContext"/>.
/// </summary>
/// <remarks>
/// <para>GDK has no notion of what actions exist.</para>
/// <para>The action name set, default bindings, and what each action
/// does are entirely up to the consuming game. This class only knows how to resolve a name to a binding
/// and that binding to device state.
/// </para>
/// </remarks>
public sealed class InputActionMap {
  private readonly IInputContext context;
  private readonly Dictionary<string, List<IInputBinding>> bindings = [];

  /// <summary>Raised when any binding for <c>action</c> transitions from up to down.</summary>
  public event Action<string>? Pressed;
  /// <summary>Raised when any binding for <c>action</c> transitions from down to up.</summary>
  public event Action<string>? Released;
  /// <summary>
  /// Raised for a <see cref="MouseScrollBinding"/>-bound <c>action</c> with the wheel's raw signed tick
  /// magnitude (<see cref="ScrollWheel.Y"/>), not just its direction - consumers that want scroll speed
  /// to scale with how hard/fast the wheel moved (e.g. zoom) should use this instead of <see cref="Pressed"/>.
  /// </summary>
  public event Action<string, float>? Scrolled;

  public InputActionMap(IInputContext context, IEnumerable<KeyValuePair<string, IInputBinding>> defaultBindings) {
    this.context = context;
    foreach (var (action, binding) in defaultBindings) AddBinding(action, binding);

    foreach (var keyboard in context.Keyboards) {
      keyboard.KeyDown += (_, key, _) => OnKeyChanged(key, pressed: true);
      keyboard.KeyUp += (_, key, _) => OnKeyChanged(key, pressed: false);
    }
    foreach (var mouse in context.Mice) {
      mouse.MouseDown += (_, button) => OnButtonChanged(button, pressed: true);
      mouse.MouseUp += (_, button) => OnButtonChanged(button, pressed: false);
      mouse.Scroll += (_, wheel) => OnScroll(wheel.Y);
    }
  }

  /// <summary>The bindings currently assigned to <paramref name="action"/>, or empty if unbound.</summary>
  public IReadOnlyList<IInputBinding> GetBindings(string action) =>
    bindings.TryGetValue(action, out var list) ? list : [];

  /// <summary>Replaces every binding for <paramref name="action"/> with just <paramref name="binding"/>.</summary>
  public void Bind(string action, IInputBinding binding) => bindings[action] = [binding];

  /// <summary>Adds an additional binding for <paramref name="action"/>, keeping any existing ones.</summary>
  public void AddBinding(string action, IInputBinding binding) {
    if (!bindings.TryGetValue(action, out var list)) bindings[action] = list = [];
    list.Add(binding);
  }

  /// <summary>Whether any binding for <paramref name="action"/> is currently held down.</summary>
  public bool IsActive(string action) =>
    bindings.TryGetValue(action, out var list) && list.Exists(binding => binding.IsActive(context));

  private void OnKeyChanged(Key key, bool pressed) {
    foreach (var (action, list) in bindings) {
      if (list.Exists(binding => binding is KeyboardBinding keyboardBinding && keyboardBinding.Key == key)) Raise(action, pressed);
    }
  }

  private void OnButtonChanged(MouseButton button, bool pressed) {
    foreach (var (action, list) in bindings) {
      if (list.Exists(binding => binding is MouseBinding mouseBinding && mouseBinding.Button == button)) Raise(action, pressed);
    }
  }

  private void OnScroll(float deltaY) {
    if (deltaY == 0f) return;
    var direction = deltaY > 0f ? ScrollDirection.Up : ScrollDirection.Down;
    foreach (var (action, list) in bindings) {
      if (list.Exists(binding => binding is MouseScrollBinding scroll && scroll.Direction == direction)) Scrolled?.Invoke(action, deltaY);
    }
  }

  private void Raise(string action, bool pressed) {
    if (pressed) Pressed?.Invoke(action);
    else Released?.Invoke(action);
  }
}

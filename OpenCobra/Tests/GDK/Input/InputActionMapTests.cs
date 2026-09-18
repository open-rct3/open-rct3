// InputActionMapTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.GDK.Input;
using Silk.NET.Input;

namespace OpenCobra.Tests.GDK.Input;

[TestFixture]
public class InputActionMapTests {
  private const string RotateLeft = "rotate-map-left";
  private const string RotateRight = "rotate-map-right";
  private const string ZoomIn = "zoom-in";

  private static InputActionMap CreateMap(FakeInputContext context, params (string Action, IInputBinding Binding)[] defaults) =>
    new(context, defaults.Select(d => new KeyValuePair<string, IInputBinding>(d.Action, d.Binding)));

  [Test]
  public void GetBindings_ReturnsEmpty_ForAnUnboundAction() {
    var context = new FakeInputContext();
    var map = CreateMap(context);

    Assert.That(map.GetBindings("does-not-exist"), Is.Empty);
  }

  [Test]
  public void GetBindings_ReturnsTheDefaultBinding() {
    var context = new FakeInputContext();
    var binding = new KeyboardBinding(Key.Q);
    var map = CreateMap(context, (RotateLeft, binding));

    Assert.That(map.GetBindings(RotateLeft), Is.EqualTo(new IInputBinding[] { binding }));
  }

  [Test]
  public void IsActive_ReflectsLiveDeviceState() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));

    Assert.That(map.IsActive(RotateLeft), Is.False);

    context.Keyboard.Press(Key.Q);
    Assert.That(map.IsActive(RotateLeft), Is.True);

    context.Keyboard.Release(Key.Q);
    Assert.That(map.IsActive(RotateLeft), Is.False);
  }

  [Test]
  public void Pressed_Fires_WhenABoundKeyGoesDown() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));
    string? firedAction = null;
    map.Pressed += action => firedAction = action;

    context.Keyboard.Press(Key.Q);

    Assert.That(firedAction, Is.EqualTo(RotateLeft));
  }

  [Test]
  public void Released_Fires_WhenABoundKeyGoesUp() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));
    string? firedAction = null;
    map.Released += action => firedAction = action;

    context.Keyboard.Press(Key.Q);
    context.Keyboard.Release(Key.Q);

    Assert.That(firedAction, Is.EqualTo(RotateLeft));
  }

  [Test]
  public void Pressed_DoesNotFire_ForAnUnrelatedKey() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));
    var fired = false;
    map.Pressed += _ => fired = true;

    context.Keyboard.Press(Key.Space);

    Assert.That(fired, Is.False);
  }

  [Test]
  public void Pressed_Fires_WhenABoundMouseButtonGoesDown() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new MouseBinding(MouseButton.Right)));
    string? firedAction = null;
    map.Pressed += action => firedAction = action;

    context.Mouse.Press(MouseButton.Right);

    Assert.That(firedAction, Is.EqualTo(RotateLeft));
  }

  [Test]
  public void Bind_ReplacesAnyExistingBindingsForTheAction() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));

    map.Bind(RotateLeft, new KeyboardBinding(Key.A));

    Assert.That(map.GetBindings(RotateLeft), Is.EqualTo(new IInputBinding[] { new KeyboardBinding(Key.A) }));
    // The old binding no longer resolves to the action.
    context.Keyboard.Press(Key.Q);
    Assert.That(map.IsActive(RotateLeft), Is.False);
  }

  [Test]
  public void AddBinding_KeepsExistingBindingsAndAddsAnAlternate() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (RotateLeft, new KeyboardBinding(Key.Q)));

    map.AddBinding(RotateLeft, new KeyboardBinding(Key.A));

    Assert.That(map.GetBindings(RotateLeft), Has.Count.EqualTo(2));
    context.Keyboard.Press(Key.A);
    Assert.That(map.IsActive(RotateLeft), Is.True);
  }

  [Test]
  public void KeyEvents_OnlyRaiseTheActionsBoundToThatKey() {
    var context = new FakeInputContext();
    var map = CreateMap(context,
      (RotateLeft, new KeyboardBinding(Key.Q)),
      (RotateRight, new KeyboardBinding(Key.E)));
    var firedActions = new List<string>();
    map.Pressed += firedActions.Add;

    context.Keyboard.Press(Key.Q);

    Assert.That(firedActions, Is.EqualTo(new[] { RotateLeft }));
  }

  [Test]
  public void Scrolled_Fires_ForTheActionBoundToTheMatchingDirection() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (ZoomIn, new MouseScrollBinding(ScrollDirection.Up)));
    string? firedAction = null;
    float firedDelta = 0f;
    map.Scrolled += (action, delta) => (firedAction, firedDelta) = (action, delta);

    context.Mouse.ScrollBy(1.5f);

    Assert.That(firedAction, Is.EqualTo(ZoomIn));
    Assert.That(firedDelta, Is.EqualTo(1.5f));
  }

  [Test]
  public void Scrolled_DoesNotFire_ForTheOppositeDirection() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (ZoomIn, new MouseScrollBinding(ScrollDirection.Up)));
    var fired = false;
    map.Scrolled += (_, _) => fired = true;

    context.Mouse.ScrollBy(-1f);

    Assert.That(fired, Is.False);
  }

  [Test]
  public void Scrolled_DoesNotFire_WhenDeltaIsZero() {
    var context = new FakeInputContext();
    var map = CreateMap(context, (ZoomIn, new MouseScrollBinding(ScrollDirection.Up)));
    var fired = false;
    map.Scrolled += (_, _) => fired = true;

    context.Mouse.ScrollBy(0f);

    Assert.That(fired, Is.False);
  }

  [Test]
  public void Scrolled_CarriesTheNegativeMagnitude_ForDownDirection() {
    var context = new FakeInputContext();
    var map = CreateMap(context, ("zoom-out", new MouseScrollBinding(ScrollDirection.Down)));
    float firedDelta = 0f;
    map.Scrolled += (_, delta) => firedDelta = delta;

    context.Mouse.ScrollBy(-2f);

    Assert.That(firedDelta, Is.EqualTo(-2f));
  }

  [Test]
  public void Pressed_DoesNotFire_ForAScrollBoundAction() {
    // Scroll ticks have no held state - only Scrolled should fire for MouseScrollBinding, never Pressed.
    var context = new FakeInputContext();
    var map = CreateMap(context, (ZoomIn, new MouseScrollBinding(ScrollDirection.Up)));
    var fired = false;
    map.Pressed += _ => fired = true;

    context.Mouse.ScrollBy(1f);

    Assert.That(fired, Is.False);
  }
}

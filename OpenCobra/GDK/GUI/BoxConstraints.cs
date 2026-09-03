// BoxConstraints layout primitives following Flutter's constraint model.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>Represents immutable layout constraints (min/max width and height) in whole pixels for 2D widget layout.</summary>
public readonly record struct BoxConstraints(
  int MinWidth = 0,
  int MaxWidth = int.MaxValue,
  int MinHeight = 0,
  int MaxHeight = int.MaxValue
) {
  /// <summary>Creates tight constraints where the width and height must match the given size exactly.</summary>
  public static BoxConstraints Tight(Size<int> size) =>
    new(size.Width, size.Width, size.Height, size.Height);

  /// <summary>Creates tight constraints where the width and height must match the given dimensions.</summary>
  public static BoxConstraints TightFor(int? width = null, int? height = null) =>
    new(
      width ?? 0,
      width ?? int.MaxValue,
      height ?? 0,
      height ?? int.MaxValue
    );

  /// <summary>Creates loose constraints that forbid the widget from being larger than the given size, but allow it to be smaller.</summary>
  public static BoxConstraints Loose(Size<int> size) =>
    new(0, size.Width, 0, size.Height);

  /// <summary>Creates constraints that expand to fill all available space up to the given max width and height.</summary>
  public static BoxConstraints Expand(int? width = null, int? height = null) =>
    new(
      width ?? int.MaxValue,
      width ?? int.MaxValue,
      height ?? int.MaxValue,
      height ?? int.MaxValue
    );

  /// <summary>Clamps the given size to fit within these constraints.</summary>
  public Size<int> Constrain(Size<int> size) =>
    new(
      Math.Clamp(size.Width, MinWidth, MaxWidth),
      Math.Clamp(size.Height, MinHeight, MaxHeight)
    );

  /// <summary>Gets whether width has a tight constraint (min equals max).</summary>
  public bool HasTightWidth => MinWidth >= MaxWidth;

  /// <summary>Gets whether height has a tight constraint (min equals max).</summary>
  public bool HasTightHeight => MinHeight >= MaxHeight;

  /// <summary>Gets whether both width and height are tightly constrained.</summary>
  public bool IsTight => HasTightWidth && HasTightHeight;
}

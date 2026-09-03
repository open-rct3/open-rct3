// Widget interface for immediate GUI layout and rendering.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;

namespace OpenCobra.GDK.GUI;

/// <summary>Represents an immediate GUI component following the constraints-down sizing protocol in whole pixels.</summary>
public interface IWidget {
  /// <summary>Renders the widget within the given layout constraints, returning its resolved size.</summary>
  /// <param name="constraints">The layout boundaries passed down by the parent container.</param>
  /// <returns>The actual size in whole pixels chosen and occupied by the widget.</returns>
  Size<int> Render(BoxConstraints constraints);
}

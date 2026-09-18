// Game Presentation Options
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3;

/// <summary>Resolves process-level presentation choices used by native visual verification.</summary>
internal static class GamePresentationOptions {
  internal const string HideUiEnvironmentVariable = "OPENRCT3_HIDE_UI";

  public static bool ShowUserInterface => ShouldShowUserInterface(
    Environment.GetEnvironmentVariable(HideUiEnvironmentVariable));

  internal static bool ShouldShowUserInterface(string? hideUi)
    => !string.Equals(hideUi, "1", StringComparison.Ordinal);
}

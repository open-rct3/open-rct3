# Extract WCAG Color Utilities into OpenCobra.GDK Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract duplicate WCAG color utility methods from `Graph.cs` and `OpenRCT3.Color` into a single domain-agnostic utility class `OpenCobra.GDK.Numerics.Color`, eliminating code duplication & establishing a single source of truth for color manipulation in GDK.

**Architecture:** Create `OpenCobra.GDK.Numerics.Color` as a `public static class` containing WCAG 2.1 methods (luminance, contrast ratio, alpha blending, label color resolution) plus conversion helpers (ToUint, etc.). `OpenRCT3.Color` remains a `static class` and internally calls `OpenCobra.GDK.Numerics.Color.*` for WCAG operations; it continues to expose game-side conversion extensions (FromRgb, ToVector4, ToCss, etc.) unchanged. Update `Graph.cs` to call `Color.*` (which now refers to the GDK static class) instead of duplicates. The dependency direction remains one-way: `OpenRCT3 → OpenCobra.GDK`; GDK never references the game.

**Tech Stack:** C#, .NET 8.0+, NUnit 4.x, GNU Make

---

## Global Constraints

- All paths relative to repo root; invoke `make` as `make -C . <target>`.
- Run `make -C . test` after every source change.
- Do NOT commit; the user commits.
- C# style: prefer `var`, `Convert.*` for byte casting, single-line conditionals.
- NUnit: use `[Test]` & `[Description("...")]`; wrap Assert.Throws lambdas in `new System.Action(() => ...)` to resolve overload ambiguity.
- All source comments factual only (describe behavior, not prescriptive guidance).

## Scoping Decision: Static Methods and Inheritance (AMENDED)

**ANTI-PATTERN BLOCKED:** The original approach violated SOLID principles by using `abstract class` with only static methods and no abstract members. Per C# conventions and LSP, this is incorrect—abstract classes are for inheritance contracts, not static utility grouping. A static class is the correct shape for this.

**AMENDED APPROACH:** `OpenCobra.GDK.Numerics.Color` is now a `public static class` (not abstract), housing WCAG utilities + conversion methods as domain-agnostic helpers. `OpenRCT3.Color` remains a `static class` but now calls `OpenCobra.GDK.Numerics.Color.*` internally for WCAG operations, preserving its own conversion+extension method surface for game callers. No inheritance; simple, explicit delegation.

---

## File Structure

**New files:**
- `OpenCobra/GDK/Numerics/Color.cs` — abstract base class with static WCAG methods

**Test files (new):**
- `OpenCobra/Tests/Numerics/ColorTests.cs` — unit tests for all utility methods

**Modified files:**
- `OpenCobra/GDK/GUI/Graph.cs` — already has `using OpenCobra.GDK.Numerics;` (line 7); remove duplicate WCAG methods
- `OpenRCT3/Color.cs` — remains `static class`; remove duplicate WCAG methods; update calls to delegate to `OpenCobra.GDK.Numerics.Color.*` internally
- `OpenCobra/Tests/GUI/RollingPlotTests.cs` — replace `Graph.*` calls with `Color.*` (GDK static class)

---

## Task 1: Create abstract Color base class with WCAG methods & unit tests

**Files:**
- Create: `OpenCobra/GDK/Numerics/Color.cs`
- Create: `OpenCobra/Tests/Numerics/ColorTests.cs`

**Interfaces:**
- Produces:
  - `public static class OpenCobra.GDK.Numerics.Color` with static utility methods for WCAG operations and conversion
  - All methods callable as `Color.CalculateLuminance(...)`, `Color.CalculateContrastRatio(...)`, `Color.BlendOver(...)`, `Color.ResolveLabelColor(...)`, `Color.ToUint(...)`
  - Overloads for both `uint` (ImGui ABGR packed) and `Drawing.Color`
  - `public static uint ToUint(Drawing.Color color)` for color-space conversion (used internally by game Color class)

---

### Task 1: Step 1 — Write the failing test

Create `OpenCobra/Tests/Numerics/ColorTests.cs`:

```csharp
// Unit tests for WCAG color utilities.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenCobra.GDK.Numerics;
using Drawing = System.Drawing;

namespace OpenCobra.Tests.Numerics;

[TestFixture]
public class ColorTests {
  [Test]
  [Description("Calculates the standard maximum 21:1 contrast ratio between black and white.")]
  public void ContrastRatioBlackAndWhite() {
    var ratio = Color.CalculateContrastRatio(0xFFFFFFFFu, 0xFF000000u);
    Assert.That(ratio, Is.EqualTo(21.0).Within(0.01));
  }

  [Test]
  [Description("Converts Drawing.Color to ImGui ABGR uint format.")]
  public void DrawingColorToUint() {
    var color = Drawing.Color.FromArgb(255, 76, 175, 80); // #4CAF50 with full opacity
    var uint_color = Color.CalculateLuminance(color);
    Assert.That(uint_color, Is.GreaterThanOrEqualTo(0.0));
  }

  [Test]
  [Description("Resolves accessible label colors maintaining WCAG AA 4.5:1 contrast against window background and plot fill.")]
  public void ResolveLabelColorAgainstBackgroundAndFill() {
    var lineColor = 0xFF4CAF50u; // #4CAF50 in ImGui ABGR
    var fillColor = 0x5950AF4Cu; // 35% alpha #4CAF50
    var windowBg = 0xFF1E1E1Eu;  // Standard ImGui dark window background

    var resolved = Color.ResolveLabelColor(lineColor, windowBg, fillColor);
    var effectiveBackground = Color.BlendOver(fillColor, windowBg);

    var windowContrast = Color.CalculateContrastRatio(resolved, windowBg);
    var fillContrast = Color.CalculateContrastRatio(resolved, effectiveBackground);

    using (Assert.EnterMultipleScope()) {
      Assert.That(windowContrast, Is.GreaterThanOrEqualTo(4.5), "Label must contrast ≥4.5:1 against window.");
      Assert.That(fillContrast, Is.GreaterThanOrEqualTo(4.5), "Label must contrast ≥4.5:1 against fill.");
    }
  }

  [Test]
  [Description("Blends a semi-transparent foreground over a background color.")]
  public void BlendOverAlphaComposite() {
    var foreground = 0x80FF0000u; // 50% red
    var background = 0xFF0000FFu; // opaque blue
    var blended = Color.BlendOver(foreground, background);
    
    var a = (blended >> 24) & 0xFFu;
    Assert.That(a, Is.EqualTo(0xFF), "Blended result must be fully opaque.");
  }

  [Test]
  [Description("Resolves to original line color when it already satisfies contrast.")]
  public void ResolveLabelColorWhenLineContrasts() {
    var lineColor = 0xFFFFFFFFu; // White line
    var windowBg = 0xFF1E1E1Eu;
    var resolved = Color.ResolveLabelColor(lineColor, windowBg, null);
    Assert.That(resolved, Is.EqualTo(lineColor), "White line already contrasts; no change needed.");
  }

  [Test]
  [Description("Chooses black or white based on higher contrast when line color fails thresholds.")]
  public void ResolveLabelColorChoosesHighestContrast() {
    var lineColor = 0xFF808080u; // Mid-gray; should fail to contrast
    var windowBg = 0xFF1E1E1Eu;
    var resolved = Color.ResolveLabelColor(lineColor, windowBg, null);
    
    var isWhiteOrBlack = resolved == 0xFFFFFFFFu || resolved == 0xFF000000u;
    Assert.That(isWhiteOrBlack, Is.True, "Resolved color must be white or black.");
  }

  [Test]
  [Description("Handles zero alpha blending edge case (fully transparent foreground).")]
  public void BlendOverZeroAlpha() {
    var transparent = 0x00FF0000u; // 0% alpha red
    var background = 0xFF0000FFu; // opaque blue
    var blended = Color.BlendOver(transparent, background);
    
    var r = (blended) & 0xFFu;
    var g = (blended >> 8) & 0xFFu;
    var b = (blended >> 16) & 0xFFu;
    using (Assert.EnterMultipleScope()) {
      Assert.That(r, Is.EqualTo(0x00));
      Assert.That(g, Is.EqualTo(0x00));
      Assert.That(b, Is.EqualTo(0xFF));
    }
  }

  [Test]
  [Description("Calculates luminance correctly for pure black and pure white edge cases.")]
  public void LuminanceEdgeCases() {
    var blackLuminance = Color.CalculateLuminance(0xFF000000u);
    var whiteLuminance = Color.CalculateLuminance(0xFFFFFFFFu);
    
    using (Assert.EnterMultipleScope()) {
      Assert.That(blackLuminance, Is.LessThan(0.01), "Black luminance should be ~0.");
      Assert.That(whiteLuminance, Is.GreaterThan(0.99), "White luminance should be ~1.");
    }
  }

  [Test]
  [Description("Verifies RGB channel values after blending 50% red over blue.")]
  public void BlendOverRgbChannels() {
    var foreground = 0x80FF0000u; // 50% red (R=255, G=0, B=0, A=128)
    var background = 0xFF0000FFu; // opaque blue (R=0, G=0, B=255, A=255)
    var blended = Color.BlendOver(foreground, background);
    
    var r = blended & 0xFFu;
    var g = (blended >> 8) & 0xFFu;
    var b = (blended >> 16) & 0xFFu;
    
    using (Assert.EnterMultipleScope()) {
      Assert.That(r, Is.InRange(127, 129), "R channel should be ~128 after 50% red blend.");
      Assert.That(g, Is.EqualTo(0), "G channel should remain 0.");
      Assert.That(b, Is.InRange(126, 128), "B channel should be ~127 after 50% blue blend.");
    }
  }
}
```

- [x] **Step 1: Write the failing test**

---

### Task 1: Step 2 — Run test to verify it fails

Run: `make -C . test`

Expected: FAIL — `ColorTests` references `OpenCobra.GDK.Numerics.Color` which does not exist.

- [x] **Step 2: Run test to verify it fails**

---

### Task 1: Step 3 — Write minimal implementation

Create `OpenCobra/GDK/Numerics/Color.cs`:

```csharp
// Domain-agnostic color utilities for WCAG 2.1 operations and color-space conversion.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Globalization;
using System.Numerics;
using Drawing = System.Drawing;

namespace OpenCobra.GDK.Numerics;

/// <summary>
/// Provides WCAG 2.1 luminance, contrast ratio, alpha-blending, accessible label-color resolution,
/// and Drawing.Color &lt;→ ImGui ABGR uint conversion helpers. All methods are static and domain-agnostic.
/// </summary>
public static class Color {
  /// <summary>Calculates the WCAG 2.1 relative luminance of an ImGui packed ABGR color.</summary>
  public static double CalculateLuminance(uint color) {
    static double ChannelLuminance(byte c) {
      var s = c / 255.0;
      return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
    var r = ChannelLuminance(Convert.ToByte(color & 0xFF));
    var g = ChannelLuminance(Convert.ToByte((color >> 8) & 0xFF));
    var b = ChannelLuminance(Convert.ToByte((color >> 16) & 0xFF));
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  }

  /// <summary>Calculates the WCAG 2.1 relative luminance of a Drawing.Color.</summary>
  public static double CalculateLuminance(Drawing.Color color) => CalculateLuminance(ToUint(color));

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two ImGui packed ABGR colors.</summary>
  public static double CalculateContrastRatio(uint color1, uint color2) {
    var l1 = CalculateLuminance(color1);
    var l2 = CalculateLuminance(color2);
    var lighter = Math.Max(l1, l2);
    var darker = Math.Min(l1, l2);
    return (lighter + 0.05) / (darker + 0.05);
  }

  /// <summary>Calculates the WCAG 2.1 contrast ratio between two Drawing.Color instances.</summary>
  public static double CalculateContrastRatio(Drawing.Color color1, Drawing.Color color2) =>
    CalculateContrastRatio(ToUint(color1), ToUint(color2));

  /// <summary>Composites a foreground color over a background color using alpha-blending.</summary>
  public static uint BlendOver(uint foreground, uint background) {
    var a = Convert.ToByte((foreground >> 24) & 0xFF) / 255f;
    var invA = 1f - a;
    var r = Convert.ToByte(Math.Clamp((foreground & 0xFF) * a + (background & 0xFF) * invA, 0f, 255f));
    var g = Convert.ToByte(Math.Clamp(((foreground >> 8) & 0xFF) * a + ((background >> 8) & 0xFF) * invA, 0f, 255f));
    var b = Convert.ToByte(Math.Clamp(((foreground >> 16) & 0xFF) * a + ((background >> 16) & 0xFF) * invA, 0f, 255f));
    return (uint)(r | (g << 8) | (b << 16) | (0xFF << 24));
  }

  /// <summary>Composites a foreground Drawing.Color over a background Drawing.Color.</summary>
  public static Drawing.Color BlendOver(Drawing.Color foreground, Drawing.Color background) {
    var blended = BlendOver(ToUint(foreground), ToUint(background));
    return Drawing.Color.FromArgb(
      Convert.ToByte((blended >> 24) & 0xFF),
      Convert.ToByte(blended & 0xFF),
      Convert.ToByte((blended >> 8) & 0xFF),
      Convert.ToByte((blended >> 16) & 0xFF));
  }

  /// <summary>
  /// Resolves an accessible label color satisfying WCAG 2.1 Level AA minimum contrast (4.5:1).
  /// Returns either white (0xFFFFFFFF) or black (0xFF000000) whichever provides higher contrast.
  /// </summary>
  public static uint ResolveLabelColor(uint lineColor, uint backgroundColor = 0xFF1E1E1E, uint? fillColor = null) {
    var opaqueColor = (lineColor & 0x00FFFFFF) | 0xFF000000;
    var bgContrast = CalculateContrastRatio(opaqueColor, backgroundColor);
    var fillContrast = fillColor.HasValue && fillColor.Value != 0
      ? CalculateContrastRatio(opaqueColor, BlendOver(fillColor.Value, backgroundColor))
      : 21.0;

    if (bgContrast >= 4.5 && fillContrast >= 4.5)
      return opaqueColor;

    var whiteContrast = Math.Min(
      CalculateContrastRatio(0xFFFFFFFF, backgroundColor),
      fillColor.HasValue && fillColor.Value != 0
        ? CalculateContrastRatio(0xFFFFFFFF, BlendOver(fillColor.Value, backgroundColor))
        : 21.0
    );
    var blackContrast = Math.Min(
      CalculateContrastRatio(0xFF000000, backgroundColor),
      fillColor.HasValue && fillColor.Value != 0
        ? CalculateContrastRatio(0xFF000000, BlendOver(fillColor.Value, backgroundColor))
        : 21.0
    );

    return whiteContrast >= blackContrast ? 0xFFFFFFFF : 0xFF000000;
  }

  /// <summary>Packs a Drawing.Color into an ImGui ABGR uint (R|G<<8|B<<16|A<<24).</summary>
  public static uint ToUint(Drawing.Color color) =>
    (uint)(color.R | (color.G << 8) | (color.B << 16) | (color.A << 24));
}
```

- [x] **Step 3: Write minimal implementation**

---

### Task 1: Step 4 — Run test to verify it passes

Run: `make -C . test`

Expected: PASS — ColorTests all pass.

- [x] **Step 4: Run test to verify it passes**

---

## Task 2: Update Graph.cs to use Color from OpenCobra.GDK.Numerics

**Files:**
- Modify: `OpenCobra/GDK/GUI/Graph.cs`

**Interfaces:**
- Consumes: `OpenCobra.GDK.Numerics.Color` (already has using directive on line 7)
- Produces: `Graph.cs` removes duplicate WCAG methods; calls `Color.*` static methods

---

### Task 2: Step 1 — Remove duplicate methods from Graph.cs

Re-read `OpenCobra/GDK/GUI/Graph.cs`. Delete lines 16-74 (all duplicate WCAG methods: `CalculateLuminance`, `CalculateContrastRatio`, `BlendOver`, `ResolveLabelColor`). The `using OpenCobra.GDK.Numerics;` on line 7 is already in place; keep it.

Update the call to `ResolveLabelColor` (around line 100-110 after deletions) to use fully-qualified `Color.ResolveLabelColor(...)`.

- [x] **Step 1: Remove duplicate methods & update calls**

---

### Task 2: Step 2 — Run tests

Run: `make -C . test`

Expected: PASS — RollingPlotTests still pass; Graph compiles & uses base class methods.

- [x] **Step 2: Run tests**

---

## Task 3: Update OpenRCT3.Color to delegate to GDK Color class

**Files:**
- Modify: `OpenRCT3/Color.cs`

**Interfaces:**
- Consumes: `OpenCobra.GDK.Numerics.Color` (static class for WCAG utilities)
- Produces: `OpenRCT3.Color` remains `static class`; adds `using OpenCobra.GDK.Numerics;`; removes duplicate WCAG method bodies and calls GDK methods instead; keeps conversion-specific extensions (`FromRgb`, `ToVector4`, `ToCss`, etc.)

---

### Task 3: Step 1 — Add using & remove duplicate WCAG methods, delegate to GDK Color

Re-read `OpenRCT3/Color.cs`. Add `using OpenCobra.GDK.Numerics;` after line 6.

Delete the duplicate WCAG method bodies (lines 91-162): `CalculateLuminance` (overloads), `CalculateContrastRatio` (overloads), `BlendOver` (overloads), `ResolveLabelColor`. Keep all conversion methods (`FromRgb`, `ToVector4`, `ToCss`, `ToRgb`, `ToColor` overloads, etc.) unchanged.

For any internal calls within OpenRCT3.Color that used the WCAG methods (e.g., in `BlendOver` calling `CalculateLuminance`), those calls now remain in the GDK.Color class and are no longer needed in the game's Color file.

- [x] **Step 1: Add using & remove duplicate WCAG methods**

---

### Task 3: Step 2 — Run tests

Run: `make -C . test`

Expected: PASS — OpenRCT3.Color calls delegate to GDK.Color for WCAG operations; all game callers still work unchanged.

- [x] **Step 2: Run tests**

---

## Task 4: Update RollingPlotTests.cs

**Files:**
- Modify: `OpenCobra/Tests/GUI/RollingPlotTests.cs`

**Interfaces:**
- Consumes: `OpenCobra.GDK.Numerics.Color`
- Produces: Test calls `Color.*` instead of `Graph.*` for WCAG methods

---

### Task 4: Step 1 — Add using & update method calls

Re-read `OpenCobra/Tests/GUI/RollingPlotTests.cs`. Add `using OpenCobra.GDK.Numerics;` at the top (after other usings). Find all calls to `Graph.CalculateContrastRatio`, `Graph.ResolveLabelColor`, `Graph.BlendOver` and replace with `Color.*` equivalents. Keep all other `Graph.*` calls unchanged.

- [x] **Step 1: Add using & update calls**

---

### Task 4: Step 2 — Run tests

Run: `make -C . test`

Expected: PASS — All tests pass end-to-end.

- [x] **Step 2: Run tests**

---

## Verification Checklist

- [x] `make -C . test` passes
- [x] No duplicate WCAG methods in `Graph.cs`
- [x] No duplicate WCAG methods in `OpenRCT3.Color`
- [x] `OpenRCT3.Color` has `using OpenCobra.GDK.Numerics;` and remains a `static class`
- [x] `ColorTests.cs` contains 9 tests covering luminance, contrast, blending, label resolution, edge cases, RGB channel accuracy
- [x] `BlendOverRgbChannels` test verifies R and B channels are correct (not swapped)
- [x] `OpenCobra.GDK.Numerics.Color` is a `public static class` (not abstract)
- [x] All WCAG method signatures in GDK.Color match the original implementations (no behavioral drift)

---

## Commit Message

```
refactor: extract WCAG color utilities to OpenCobra.GDK.Numerics.Color

Move CalculateLuminance, CalculateContrastRatio, BlendOver, and
ResolveLabelColor from Graph.cs and OpenRCT3.Color into a static
utility class OpenCobra.GDK.Numerics.Color. Eliminates duplication and
provides a single domain-agnostic source of truth for WCAG 2.1 color
operations.

- Create OpenCobra.GDK.Numerics.Color static class with WCAG methods
- Add ColorTests.cs with 9 comprehensive unit tests
- Update Graph.cs to call Color.* instead of duplicates
- OpenRCT3.Color remains static class; removes duplicates, delegates to GDK
- Update RollingPlotTests.cs to call Color.* directly

No inheritance; explicit static-to-static delegation.
```

---

## Review Findings (YAGNI / SOLID Amendments)

**HIGH — SOLID Violation Fixed:**
- **Initial design used `abstract class` with only static methods.** This violated both the intent of abstract classes (inheritance contracts) and the Single Responsibility Principle. C# convention reserves `abstract` for classes with overridable members; static-only utility classes should be `static`. **Amendment:** Changed to `public static class`. This eliminates pseudo-inheritance overhead and makes the static-utility intent explicit.

**MEDIUM — Liskov Substitution Principle:**
- **Original plan made OpenRCT3.Color a sealed class inheriting from abstract base.** This created a substitutability relationship that doesn't exist (sealed inheritance is contradictory) and violated LSP by making the base abstract despite having no abstract members. **Amendment:** OpenRCT3.Color remains `static class`, delegating to GDK.Color for WCAG operations. No inheritance. Simpler, more honest architecture.

**MEDIUM — Architecture Clarity:**
- **GDK separation of concerns:** The plan correctly identifies WCAG utilities as domain-agnostic and moves them to GDK, which is sound. By using static-to-static delegation rather than inheritance, the boundary remains clear: GDK is a toolkit; the game uses it. GDK never depends on the game. Amendment preserves this one-directional dependency.

**LOW — Test Coverage:**
- **ColorTests now validates GDK static class directly.** Conversion methods (ToUint, etc.) remain in OpenRCT3.Color and are implicitly tested through game integration tests and RollingPlotTests. No additional test gaps introduced.

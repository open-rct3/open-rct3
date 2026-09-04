# Plan: Make ImGui Render Greek Glyphs

## Problem

`OpenRCT3/UI/Debug.cs:93` formats a stats line with the Greek lowercase sigma:

```csharp
ImGui.TextDisabled($"min: {min:0.0}ms  max: {max:0.0}ms  avg: {avg:0.0}ms  σ: {stddev:0.0}ms");
```

In the running app it shows as `?`. In ImGui, a glyph that isn't baked into the active font atlas renders as a `?` replacement, which is exactly what's happening here.

Why it's missing: `OpenCobra/GDK/GUI/Controller.cs` never calls any `AddFont*` API on the atlas. With no explicit font loaded, ImGui uses the embedded default font (ProggyClean.ttf), whose glyph range is only `U+0020` to `U+00FF` (Basic Latin and Latin-1 Supplement, see `cimgui`'s `GetGlyphRangesDefault`). Greek starts at `U+0370`, so `σ` (`U+03C3`) has no glyph and renders as `?`.

The fix is to embed Montserrat into the GDK assembly's resources and load it as the atlas default. Montserrat ships in two flavors in [JulietaUla/Montserrat](https://github.com/JulietaUla/Montserrat):

- **Static instances** under `fonts/ttf/`: `Thin`, `ExtraLight`, `Light`, `Regular`, `Medium`, `SemiBold`, `Bold`, `ExtraBold`, `Black`, each with an italic variant. Eighteen files total. SIL Open Font License.
- **Variable-weight fonts** under `fonts/variable/`: `Montserrat[wght].ttf` and `Montserrat-Italic[wght].ttf`, each carrying the OpenType `wght` axis from 100 (Thin) to 900 (Black).

This plan embeds ten static instances (Light, Regular, Medium, Bold, Black plus italics) and the variable-weight pair. The variable files are dormant: stock ImGui's `stb_truetype` can't read OpenType variation axes ([FONTS.md](https://github.com/ocornut/imgui/blob/master/docs/FONTS.md)), so they wait on [PR #9199](https://github.com/ocornut/imgui/pull/9199) or a FreeType switch. Glyph range: Basic Latin, Latin-1 Supplement, Latin Extended-A (`U+0100`-`U+017F`), and Greek and Coptic (`U+0370`-`U+03FF`).

## Solution

Embed twelve Montserrat font files in the GDK assembly's resources and load them into the ImGui font atlas during `Controller`'s construction. Register the static `Regular` as `io.FontDefault`; register the other static weights so debug widgets can `PushFont` to switch weight; load the two variable files into memory but do not register them (ImGui's `stb_truetype` can't drive the `wght` axis). Pin each embedded byte array via `GC.AllocateUninitializedArray<T>(length, pinned: true)` because `AddFont` with `FontDataOwnedByAtlas = false` keeps the pointer across the next `NewFrame()`.

### File map

- **Modify** `OpenCobra/GDK/GDK.csproj` to register twelve Montserrat files in `assets/fonts/` as `EmbeddedResource` entries with stable logical names under the prefix `OpenCobra.GDK.Fonts.Montserrat.`.
- **Create** `OpenCobra/GDK/GUI/GlyphRanges.cs`: the `uint[]` range table (Basic Latin, Latin-1, Latin Extended-A, Greek and Coptic), next to `Controller.cs`.
- **Create** `OpenCobra/GDK/GUI/EmbeddedFonts.cs`: static class holding the pinned byte arrays and `ImFontPtr` handles per weight, with a `LoadAll(Assembly)` entry point.
- **Modify** `OpenCobra/GDK/GUI/Controller.cs`: call `EmbeddedFonts.LoadAll` in the constructor, after the `ImGuiConfigFlags` block and before `ImGui.StyleColorsDark()`.
- **Create** `OpenCobra/Tests/GUI/EmbeddedFontTests.cs`: one test that `Montserrat-Regular.ttf`'s `cmap` maps `σ` (`U+03C3`) to a glyph. Regression guard against a Greek-less default font.

### Out of scope

- No other GUI widgets, no font-size or DPI math changes (`style.FontScaleDpi = mainScale` still scales the registered fonts).
- No new NuGet packages: `Hexa.NET.ImGui` 2.2.9 already exposes `AddFont(ImFontConfig*)`, `ImFontConfig`, `ImFontGlyphRangesBuilder`.
- Variable-axis weight selection: the two variable files are embedded and loaded but not registered with the atlas; `stb_truetype` can't drive the `wght` axis.
- Italic registration: italic files are loaded into memory but not `AddFont`ed (no debug widget renders italic text). Add later per weight as needed.
- `ExtraLight`, `Thin`, `SemiBold`, `ExtraBold` are not bundled.
- No copy of the font files to the bin directory; embedded resources suffice.

## Tasks

> Each task ends with `make test` passing (per `AGENTS.md`'s "Run Unit Tests" rule). Commits are the user's job; this plan does not instruct `git` commands.

### Task 1: Download the Montserrat files and embed them in the GDK assembly

**Files:**
- Add files: 12 files in `assets/fonts/` (5 static weights × upright + italic, plus 2 variable files)
- Modify: `OpenCobra/GDK/GDK.csproj:33-37` (add an `<ItemGroup>` with twelve `<EmbeddedResource>` entries)

**Why first:** `Controller` reads the fonts from `OpenCobra.GDK`'s own assembly. Until the files are on disk and wired into the csproj, no other task can run.

- [x] **Step 1: Download the Montserrat files (do NOT delete anything)**

Montserrat is SIL OFL v1.1, redistributable in-repo. Also save the `OFL.txt` notice alongside the fonts. Do **not** delete or overwrite any existing file (`assets/fonts/Proxima Nova Semibold.otf` stays; retiring it is a separate commit).

Download from [`JulietaUla/Montserrat`](https://github.com/JulietaUla/Montserrat/tree/master/fonts). The variable filenames contain `[` `]`, URL-encoded as `%5B`/`%5D`; keep the literal brackets locally. From repo root:

```bash
cd assets/fonts

# SIL Open Font License notice
curl -fL -o OFL.txt https://raw.githubusercontent.com/JulietaUla/Montserrat/master/OFL.txt

# Static instances (10 files, in fonts/ttf/ on the upstream repo)
curl -fL -o Montserrat-Light.ttf        https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Light.ttf
curl -fL -o Montserrat-Regular.ttf      https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Regular.ttf
curl -fL -o Montserrat-Medium.ttf       https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Medium.ttf
curl -fL -o Montserrat-Bold.ttf         https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Bold.ttf
curl -fL -o Montserrat-Black.ttf        https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Black.ttf
curl -fL -o Montserrat-LightItalic.ttf https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-LightItalic.ttf
curl -fL -o Montserrat-Italic.ttf       https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-Italic.ttf
curl -fL -o Montserrat-MediumItalic.ttf https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-MediumItalic.ttf
curl -fL -o Montserrat-BoldItalic.ttf   https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-BoldItalic.ttf
curl -fL -o Montserrat-BlackItalic.ttf https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/ttf/Montserrat-BlackItalic.ttf

# Variable-weight files (2 files, in fonts/variable/ on the upstream repo)
curl -fL -o 'Montserrat[wght].ttf'           https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/variable/Montserrat%5Bwght%5D.ttf
curl -fL -o 'Montserrat-Italic[wght].ttf'   https://raw.githubusercontent.com/JulietaUla/Montserrat/master/fonts/variable/Montserrat-Italic%5Bwght%5D.ttf
```

After the downloads complete, verify the byte counts and OpenType magic headers are reasonable (the regular static files are ~445 KB each; the variable files are ~745 KB each; the OFL is ~4 KB). Run:

```bash
ls -l assets/fonts/
for f in assets/fonts/*.ttf; do
  printf "%-40s %s\n" "$f" "$(xxd -l 4 -p "$f")"
done
```

Every `.ttf` must start with `00 01 00 00` (TrueType `sfnt` version). Retry any missing or malformed file before Step 2.

- [ ] **Step 2: Add the EmbeddedResource items**

Insert this immediately after the existing GLFW-exclusion `<ItemGroup>` in `OpenCobra/GDK/GDK.csproj`:

```xml
  <ItemGroup>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Light.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Light.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Regular.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Regular.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Medium.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Medium.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Bold.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Bold.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Black.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Black.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Italic.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Italic.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-LightItalic.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.LightItalic.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-MediumItalic.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.MediumItalic.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-BoldItalic.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.BoldItalic.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-BlackItalic.ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.BlackItalic.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat[wght].ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.Variable.ttf</LogicalName>
    </EmbeddedResource>
    <EmbeddedResource Include="..\..\assets\fonts\Montserrat-Italic[wght].ttf">
      <LogicalName>OpenCobra.GDK.Fonts.Montserrat.VariableItalic.ttf</LogicalName>
    </EmbeddedResource>
  </ItemGroup>
```

`<LogicalName>` sets the name `Assembly.GetManifestResourceStream` looks up (needed because resource names can't contain `[` `]`, and to override MSBuild's default path-derived name). The `..\..\assets\...` include keeps one canonical copy of the fonts at the repo root.

- [ ] **Step 3: Build `GDK.csproj` and `OpenRCT3.csproj`** -- expect success.

### Task 2: Add the glyph-range helper

**Files:**
- Create: `OpenCobra/GDK/GUI/GlyphRanges.cs`

Task 3's `EmbeddedFonts.LoadAll` passes this table to every `AddFont` call. Its own file keeps `Controller.cs` focused on wiring.

- [ ] **Step 1: Write the helper**

```csharp
// Unicode range table for the GUI font.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.GDK.GUI;

/// <summary>Unicode range tables for fonts registered with the ImGui atlas.</summary>
/// <remarks>
/// <see cref="DefaultLatinAndGreek"/> covers Basic Latin, Latin-1 Supplement, Latin Extended-A,
/// and the Greek and Coptic block. The backing array is allocated with
/// <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/><c>(..., pinned: true)</c> so the
/// runtime guarantees its address remains stable for the process lifetime; ImGui only stores the
/// pointer handed to <c>ImFontConfig.GlyphRanges</c> and reads it later during <c>Build()</c>.
/// </remarks>
internal static class GlyphRanges {
  /// <summary>Basic Latin, Latin-1 Supplement, Latin Extended-A, and Greek and Coptic (U+0370 to U+03FF).</summary>
  /// <remarks>
  /// The first range is copied verbatim from Dear ImGui's <c>GetGlyphRangesDefault()</c>
  /// so existing ASCII and Latin-1 text keeps its current glyphs. Latin Extended-A
  /// (<c>0x0100</c> to <c>0x017F</c>) covers accented Latin characters (e.g. Á, Ç, ñ) that appear
  /// in user-visible strings. The trailing <c>0</c> is the range-array terminator required by ImGui.
  /// </remarks>
  public static ReadOnlySpan<uint> DefaultLatinAndGreek => LatinAndGreek;

  // Pinned: see class remarks. Use `CollectionExpression` for the source values, then copy into
  // the pinned backing array at type-init time. `GC.AllocateUninitializedArray<uint>(..., pinned: true)`
  // returns an array the runtime guarantees not to relocate, so a raw pointer obtained via
  // `fixed (uint* p = LatinAndGreek)` remains valid after the `fixed` block exits.
  private static readonly uint[] LatinAndGreek = CreatePinnedRangeTable();

  private static uint[] CreatePinnedRangeTable() {
    ReadOnlySpan<uint> source = [
      0x0020, 0x00FF, // Basic Latin and Latin-1 Supplement
      0x0100, 0x017F, // Latin Extended-A
      0x0370, 0x03FF, // Greek and Coptic
      0,             // ImGui range-array terminator
    ];
    var pinned = GC.AllocateUninitializedArray<uint>(source.Length, pinned: true);
    source.CopyTo(pinned);
    return pinned;
  }
}
```

Notes:
- First range `0x0020`-`0x00FF` matches Dear ImGui's `GetGlyphRangesDefault()`.
- `GC.AllocateUninitializedArray<uint>(..., pinned: true)` gives a non-relocating array; ImGui stores the `ImFontConfig.GlyphRanges` pointer and reads it later at `Build()`. No `GCHandle`, no `Dispose`. If Task 3 shows the pin is unnecessary, drop `CreatePinnedRangeTable` for a plain collection expression.

- [ ] **Step 2: Build**

Run: `dotnet build OpenCobra/GDK/GDK.csproj` -- expect success.

### Task 3: Load the embedded Montserrat fonts in Controller

**Files:**
- Create: `OpenCobra/GDK/GUI/EmbeddedFonts.cs`
- Modify: `OpenCobra/GDK/GUI/Controller.cs`

- [ ] **Step 1: Strip the `Authors:` block from the `Controller.cs` file header**

Per `docs/Style Guide.md`: significant changes to a file with an `Authors:` line means deleting that line. Remove the four-line block; keep the one-line description and the copyright line.

- [ ] **Step 2: Create the `EmbeddedFonts` registry class**

`OpenCobra/GDK/GUI/EmbeddedFonts.cs` holds the pinned byte arrays and `ImFontPtr` handles per weight and exposes `LoadAll(Assembly)`.

```csharp
// Runtime-pinned byte arrays and ImFont handles for the embedded Montserrat family.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.IO;
using System.Reflection;
using Hexa.NET.ImGui;

namespace OpenCobra.GDK.GUI;

/// <summary>
/// Pinned byte arrays and atlas handles for the embedded Montserrat family. Arrays are allocated
/// with <see cref="GC.AllocateUninitializedArray{T}(int, bool)"/><c>(..., pinned: true)</c> so the
/// pointers handed to <c>AddFont</c> stay valid for the process lifetime.
/// </summary>
internal static class EmbeddedFonts {
  /// <summary>Logical name of each Montserrat resource registered in <c>GDK.csproj</c>.</summary>
  private static class ResourceNames {
    public const string Light = "OpenCobra.GDK.Fonts.Montserrat.Light.ttf";
    public const string Regular = "OpenCobra.GDK.Fonts.Montserrat.Regular.ttf";
    public const string Medium = "OpenCobra.GDK.Fonts.Montserrat.Medium.ttf";
    public const string Bold = "OpenCobra.GDK.Fonts.Montserrat.Bold.ttf";
    public const string Black = "OpenCobra.GDK.Fonts.Montserrat.Black.ttf";
    public const string Italic = "OpenCobra.GDK.Fonts.Montserrat.Italic.ttf";
    public const string LightItalic = "OpenCobra.GDK.Fonts.Montserrat.LightItalic.ttf";
    public const string MediumItalic = "OpenCobra.GDK.Fonts.Montserrat.MediumItalic.ttf";
    public const string BoldItalic = "OpenCobra.GDK.Fonts.Montserrat.BoldItalic.ttf";
    public const string BlackItalic = "OpenCobra.GDK.Fonts.Montserrat.BlackItalic.ttf";
    public const string Variable = "OpenCobra.GDK.Fonts.Montserrat.Variable.ttf";
    public const string VariableItalic = "OpenCobra.GDK.Fonts.Montserrat.VariableItalic.ttf";
  }

  // Pinned bytes per file (rooted for the assembly lifetime; Variable* loaded but unregistered).
  public static byte[] RegularBytes { get; private set; } = [];
  public static byte[] LightBytes { get; private set; } = [];
  public static byte[] MediumBytes { get; private set; } = [];
  public static byte[] BoldBytes { get; private set; } = [];
  public static byte[] BlackBytes { get; private set; } = [];
  public static byte[] ItalicBytes { get; private set; } = [];
  public static byte[] LightItalicBytes { get; private set; } = [];
  public static byte[] MediumItalicBytes { get; private set; } = [];
  public static byte[] BoldItalicBytes { get; private set; } = [];
  public static byte[] BlackItalicBytes { get; private set; } = [];
  public static byte[] VariableBytes { get; private set; } = [];
  public static byte[] VariableItalicBytes { get; private set; } = [];

  // Atlas handles; Regular is io.FontDefault. Push/Pop these to switch weight.
  public static ImFontPtr Regular { get; private set; }
  public static ImFontPtr Light { get; private set; }
  public static ImFontPtr Medium { get; private set; }
  public static ImFontPtr Bold { get; private set; }
  public static ImFontPtr Black { get; private set; }
  public static ImFontPtr Italic { get; private set; }
  public static ImFontPtr LightItalic { get; private set; }
  public static ImFontPtr MediumItalic { get; private set; }
  public static ImFontPtr BoldItalic { get; private set; }
  public static ImFontPtr BlackItalic { get; private set; }

  /// <summary>
  /// Loads the embedded resources from <paramref name="assembly"/> into runtime-pinned byte
  /// arrays, registers each Montserrat weight with the active ImGui atlas, and assigns the
  /// resulting <see cref="ImFontPtr"/> handles to the matching properties.
  /// </summary>
  public static unsafe void LoadAll(Assembly assembly) {
    RegularBytes = LoadPinned(assembly, ResourceNames.Regular);
    LightBytes = LoadPinned(assembly, ResourceNames.Light);
    MediumBytes = LoadPinned(assembly, ResourceNames.Medium);
    BoldBytes = LoadPinned(assembly, ResourceNames.Bold);
    BlackBytes = LoadPinned(assembly, ResourceNames.Black);
    ItalicBytes = LoadPinned(assembly, ResourceNames.Italic);
    LightItalicBytes = LoadPinned(assembly, ResourceNames.LightItalic);
    MediumItalicBytes = LoadPinned(assembly, ResourceNames.MediumItalic);
    BoldItalicBytes = LoadPinned(assembly, ResourceNames.BoldItalic);
    BlackItalicBytes = LoadPinned(assembly, ResourceNames.BlackItalic);
    VariableBytes = LoadPinned(assembly, ResourceNames.Variable);
    VariableItalicBytes = LoadPinned(assembly, ResourceNames.VariableItalic);

    var io = ImGui.GetIO();
    var ranges = GlyphRanges.DefaultLatinAndGreek;

    fixed (byte* regularPtr = RegularBytes)
    fixed (byte* lightPtr = LightBytes)
    fixed (byte* mediumPtr = MediumBytes)
    fixed (byte* boldPtr = BoldBytes)
    fixed (byte* blackPtr = BlackBytes)
    fixed (byte* italicPtr = ItalicBytes)
    fixed (byte* lightItalicPtr = LightItalicBytes)
    fixed (byte* mediumItalicPtr = MediumItalicBytes)
    fixed (byte* boldItalicPtr = BoldItalicBytes)
    fixed (byte* blackItalicPtr = BlackItalicBytes)
    fixed (uint* rangesPtr = ranges) {
      Regular = io.Fonts.AddFont(NewConfig(regularPtr, RegularBytes.Length, rangesPtr));
      Light = io.Fonts.AddFont(NewConfig(lightPtr, LightBytes.Length, rangesPtr));
      Medium = io.Fonts.AddFont(NewConfig(mediumPtr, MediumBytes.Length, rangesPtr));
      Bold = io.Fonts.AddFont(NewConfig(boldPtr, BoldBytes.Length, rangesPtr));
      Black = io.Fonts.AddFont(NewConfig(blackPtr, BlackBytes.Length, rangesPtr));
      Italic = io.Fonts.AddFont(NewConfig(italicPtr, ItalicBytes.Length, rangesPtr));
      LightItalic = io.Fonts.AddFont(NewConfig(lightItalicPtr, LightItalicBytes.Length, rangesPtr));
      MediumItalic = io.Fonts.AddFont(NewConfig(mediumItalicPtr, MediumItalicBytes.Length, rangesPtr));
      BoldItalic = io.Fonts.AddFont(NewConfig(boldItalicPtr, BoldItalicBytes.Length, rangesPtr));
      BlackItalic = io.Fonts.AddFont(NewConfig(blackItalicPtr, BlackItalicBytes.Length, rangesPtr));
      io.FontDefault = Regular;
    }
    // Variable files stay unregistered: stb_truetype can't drive the wght axis.
  }

  /// <summary>Reads a resource into a runtime-pinned byte array.</summary>
  private static byte[] LoadPinned(Assembly assembly, string resourceName) {
    using var stream = assembly.GetManifestResourceStream(resourceName)
      ?? throw new InvalidOperationException(
        $"Embedded font '{resourceName}' not found in {assembly.GetName().Name}. "
        + "Ensure the file is declared as <EmbeddedResource> in OpenCobra/GDK/GDK.csproj.");
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    if (ms.Length < 4)
      throw new InvalidOperationException(
        $"Embedded font '{resourceName}' is empty or corrupt.");
    var bytes = GC.AllocateUninitializedArray<byte>((int)ms.Length, pinned: true);
    ms.Position = 0;
    ms.ReadExactly(bytes);
    return bytes;
  }

  /// <summary>The <see cref="ImFontConfig"/> used for every Montserrat weight. Named args match the
  /// v2.2.9 binding's `ImFontConfig(byte* name, void* fontData, ...)` constructor.</summary>
  private static unsafe ImFontConfig NewConfig(byte* fontData, int fontDataSize, uint* glyphRanges) => new(
    name: null,
    fontData: fontData,
    fontDataSize: fontDataSize,
    fontDataOwnedByAtlas: false,
    mergeMode: false,
    pixelSnapH: false,
    pixelSnapV: false,
    oversampleH: 2,
    oversampleV: 1,
    ellipsisChar: 0x2026, // U+2026 HORIZONTAL ELLIPSIS, matches Dear ImGui defaults
    sizePixels: 13f,
    glyphRanges: glyphRanges,
    glyphExcludeRanges: null,
    glyphOffset: default,
    glyphMinAdvanceX: 0f,
    glyphMaxAdvanceX: float.MaxValue,
    glyphExtraAdvanceX: 0f,
    fontNo: 0,
    fontLoaderFlags: 0,
    rasterizerMultiply: 1f,
    rasterizerDensity: 1f,
    flags: 0,
    dstFont: default,
    fontLoader: null,
    fontLoaderData: null);
}
```

`{ get; private set; } = [];` satisfies the non-nullable-field rule; only `LoadAll` (same class) mutates the fields. Byte arrays stay rooted by these static fields for the assembly's lifetime, so `AddFont`'s pointers stay valid (`FontDataOwnedByAtlas = false`); no `Dispose`, no `GCHandle`.

- [ ] **Step 3: Add the call site in `Controller`'s constructor**

Add `using System.IO;` and `using System.Reflection;`. In the constructor, after `io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;` and before `ImGui.StyleColorsDark()` / `style.FontScaleDpi = mainScale` (so DPI scaling applies to the new fonts):

```csharp
    EmbeddedFonts.LoadAll(typeof(Controller).Assembly);
```

The constructor currently loads no font (ImGui falls back to its embedded default), so there is nothing to remove.

- [ ] **Step 4: Build and test**

Run: `dotnet build OpenCobra/GDK/GDK.csproj`, then `make test` -- expect all green.

### Task 4: Assert the default font covers `σ`

**Files:**
- Create: `OpenCobra/Tests/GUI/EmbeddedFontTests.cs`

**Why:** the regression that would silently reintroduce the bug is a future swap of the atlas-default font (`Montserrat-Regular.ttf`) for one without the Greek block. One test guards it: the embedded `Regular` file's `cmap` Format 4 subtable must map `U+03C3` to a non-zero glyph. Other files aren't tested (a `<LogicalName>` typo shows up as a load failure in Task 3).

- [ ] **Step 1: Add the test file**

```csharp
// Verifies the atlas-default font (Montserrat Regular) covers the Greek block.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.IO;
using NUnit.Framework;
using OpenCobra.GDK.GUI;

namespace OVL.Tests.GUI;

[TestFixture]
public class EmbeddedFontTests {
  private const string RegularResource = "OpenCobra.GDK.Fonts.Montserrat.Regular.ttf";

  [Test]
  public void DefaultFont_CoverageIncludesSigma() {
    // Montserrat Regular is registered as io.FontDefault (Task 3). If it stops covering
    // U+03C3 the ImGui atlas renders '?' for it, which is the bug this plan fixes.
    var bytes = LoadResourceBytes(RegularResource);
    Assert.That(CmapContainsCodepoint(bytes, 0x03C3), Is.True,
      $"'{RegularResource}' does not cover U+03C3 (σ). "
      + "Replace it with a font that includes the Greek block.");
  }

  private static byte[] LoadResourceBytes(string resourceName) {
    var assembly = typeof(Controller).Assembly;
    using var stream = assembly.GetManifestResourceStream(resourceName)
      ?? throw new AssertionException(
        $"Embedded resource '{resourceName}' not found. "
        + "Check the <EmbeddedResource> / <LogicalName> entry in OpenCobra/GDK/GDK.csproj.");
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    return ms.ToArray();
  }

  private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
    // Cast each byte to `uint` before shifting: `byte << 24` returns a signed `int` and corrupts the
    // high bit for byte values >= 0x80. The cast keeps the shift unsigned.
    => (uint)bytes[offset] << 24
     | (uint)bytes[offset + 1] << 16
     | (uint)bytes[offset + 2] << 8
     | (uint)bytes[offset + 3];

  private static ushort ReadUInt16BigEndian(byte[] bytes, int offset)
    => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

  /// <summary>
  /// Returns true if the OpenType font's cmap table maps <paramref name="codepoint"/> to a glyph
  /// via a Format 4 subtable. Prefers the Windows Unicode BMP subtable (pid=3, eid=1) so it does
  /// not depend on subtable record order. The bundled Montserrat TTFs ship a Format 4 subtable;
  /// every other format returns false (out of scope for this regression test).
  /// </summary>
  private static bool CmapContainsCodepoint(byte[] font, uint codepoint) {
    var numTables = ReadUInt16BigEndian(font, ReadUInt16BigEndian(font, 4));
    var tableOffset = 12;
    uint? cmapOffset = null;
    for (var i = 0; i < numTables; i++, tableOffset += 16) {
      var tag = System.Text.Encoding.ASCII.GetString(font, tableOffset, 4);
      if (tag != "cmap") continue;
      cmapOffset = ReadUInt32BigEndian(font, tableOffset + 8);
      break;
    }
    if (cmapOffset is not { } baseOffset) return false;

    var numSubtables = ReadUInt16BigEndian(font, (int)baseOffset + 2);
    // Prefer the Windows Unicode BMP subtable (pid=3, eid=1) when present; otherwise fall back
    // to the first subtable we can parse. This avoids depending on the order of subtable records.
    var firstParseable = -1;
    for (var i = 0; i < numSubtables; i++) {
      var rec = (int)baseOffset + 4 + i * 8;
      var pid = ReadUInt16BigEndian(font, rec);
      var eid = ReadUInt16BigEndian(font, rec + 2);
      var subOff = ReadUInt16BigEndian(font, rec + 4) | (ReadUInt16BigEndian(font, rec + 6) << 16);
      var absOff = baseOffset + subOff;
      if (pid == 3 && eid == 1) {
        return SubtableContains(font, absOff, codepoint);
      }
      if (firstParseable < 0) firstParseable = (int)absOff;
    }
    if (firstParseable < 0) return false;
    return SubtableContains(font, (uint)firstParseable, codepoint);
  }

  private static bool SubtableContains(byte[] font, uint offset, uint codepoint) {
    var format = ReadUInt16BigEndian(font, (int)offset);
    return format == 4 && Format4Contains(font, offset, codepoint);
  }

  private static bool Format4Contains(byte[] font, uint offset, uint codepoint) {
    if (codepoint > 0xFFFFu) return false;
    var segCountX2 = ReadUInt16BigEndian(font, (int)offset + 6);
    var segCount = segCountX2 / 2;
    var endCodesOffset = (int)offset + 14;
    var startCodesOffset = endCodesOffset + segCountX2 + 2; // +2 for reservedPad
    var idDeltasOffset = startCodesOffset + segCountX2;
    var idRangeOffsetsOffset = idDeltasOffset + segCountX2;
    for (var s = 0; s < segCount; s++) {
      var endCode = ReadUInt16BigEndian(font, endCodesOffset + 2 * s);
      if (endCode == 0xFFFF && s == segCount - 1) return false; // terminator segment with no glyphs
      if (endCode < codepoint) continue;
      var startCode = ReadUInt16BigEndian(font, startCodesOffset + 2 * s);
      if (startCode > codepoint) continue;
      // We are in this segment. Compute the glyph ID per Format 4 rules:
      //   idRangeOffset[s] == 0 => glyph = codepoint + idDelta[s]
      //   else                  => glyph = *(idRangeOffset[s] + 2*(codepoint - startCode) + &idRangeOffset[s])
      var idDelta = (short)ReadUInt16BigEndian(font, idDeltasOffset + 2 * s);
      var idRangeOffset = ReadUInt16BigEndian(font, idRangeOffsetsOffset + 2 * s);
      if (idRangeOffset == 0) {
        return ((codepoint + (uint)idDelta) & 0xFFFFu) != 0;
      }
      var glyphOffset = idRangeOffsetsOffset + 2 * s + idRangeOffset + 2 * (codepoint - startCode);
      var glyphId = ReadUInt16BigEndian(font, (int)glyphOffset);
      return glyphId != 0;
    }
    return false;
  }
}
```

Format 4 only (what the bundled Montserrat TTFs ship); every other cmap format returns `false`, so a font that needs another format fails the test loudly.

- [ ] **Step 2: Run tests**

Run: `dotnet test OpenCobra/Tests/Tests.csproj --filter FullyQualifiedName~EmbeddedFontTests`, then `make test` -- expect all green.

## Open items

- **Pinning the glyph-ranges array may be unneeded.** A `static readonly` array is already GC-rooted; pinning only stops relocation. If Task 3 can force `io.Fonts.Build()` (or the OpenGL3 backend builds the atlas before the constructor returns), a `fixed` block over `AddFont`/`Build` is enough and `GlyphRanges` drops to a plain collection expression. The font *bytes* still need rooting because `FontDataOwnedByAtlas = false`.

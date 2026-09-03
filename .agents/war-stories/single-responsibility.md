# Case Study: Single-Responsibility Violations & Parameter Accumulation in Immediate UI

## The Core Failures

1. **Monolithic Method Signatures Accumulating Unrelated Concerns:** Instead of encapsulating graph rendering parameters into dedicated data carriers, `Graph.PlotLines` accumulated an ever-expanding argument list (10 separate parameters including values, capacity, dimensions, colors, threshold scale, stroke thickness, and axis flags).
2. **Conflating Window Overlays with Stateful Component Rendering:** `OpenRCT3.UI.Debug` was originally tasked with accumulating telemetry history, managing rolling buffers, rendering the ImGui frame, computing dynamic graph coordinates, drawing filled polygons, and formatting text readouts simultaneously.
3. **Delayed Widget Abstraction Extraction:** Rather than recognizing that a rolling plot is an independent, stateful UI widget (`RollingPlot`) that can manage its own samples and constraints, multiple implementation iterations left raw collections and drawing primitives directly in the debug window.
4. **Scattered Conversions for Common Data Formats:** Color conversions were performed ad-hoc using `ImGui.ColorConvertFloat4ToU32` across rendering calls instead of housing a domain-level `ToUint()` conversion on `OpenRCT3.Color`.
5. **Ignoring Layout Protocol Boundaries:** Sizing constraints were initially hard-coded in pixels and float vectors before formally separating parent constraints from child sizing decisions via `BoxConstraints` and `IWidget`.

## Sequence of Events

### 1. Inlining Graph Math and Rendering into Debug Window
During initial telemetry graph work, `OpenRCT3.UI.Debug` directly handled frame timing, maintained a local `RingBuffer<float>`, calculated canvas positions, and drew filled concave polygons using `ImDrawList.AddConvexPolyFilled`. This caused both geometric bowtie rendering bugs and heavy single-responsibility violations.

### 2. Extracting Static `Graph.PlotLines` With Parameter Bloat
When extracting graph rendering into `OpenCobra.GDK.GUI.Graph`, all parameters were passed as bare arguments to a static method:
```csharp
public static void PlotLines(
  IReadOnlyList<float> values,
  int capacity,
  Vector2 size,
  uint lineColor,
  uint fillColor,
  float targetScale,
  float thickness,
  bool showXAxis,
  bool showYAxis
)
```
Each additional visual feature (such as Y-axis toggles and reference threshold lines) further inflated the method signature and leaked parameter coupling across every overload.

### 3. Missing Domain Extensions on `Color`
At call sites, RGBA integer packing was handled by calling `ImGui.ColorConvertFloat4ToU32(Color.FromRgb(...).ToVector4())` rather than providing an idiomatic `color.ToUint()` extension directly on `OpenRCT3.Color`.

### 4. Remediation: Separation of Responsibilities

The user directed multiple refactorings to cleanly isolate responsibilities:

1. **`Graph.PlotLinesParameters` (`readonly record struct`):**
   Encapsulated rendering parameters into a dedicated data carrier object in [OpenCobra/GDK/GUI/Graph.cs](file:///OpenCobra/GDK/GUI/Graph.cs), supporting flexible sizing formats (`Size<int>`, `Size`, `Size<float>`, `Vector2`) and isolating parameter defaults.
2. **`IWidget` & `BoxConstraints`:**
   Adopted Flutter's constraint-down layout protocol in [OpenCobra/GDK/GUI/IWidget.cs](file:///OpenCobra/GDK/GUI/IWidget.cs) and [OpenCobra/GDK/GUI/BoxConstraints.cs](file:///OpenCobra/GDK/GUI/BoxConstraints.cs). Parents dictate boundaries; widgets resolve and return their occupied `Size<int>`.
3. **`RollingPlot` Stateful Widget:**
   Created [OpenCobra/GDK/GUI/RollingPlot.cs](file:///OpenCobra/GDK/GUI/RollingPlot.cs) to encapsulate sample history management, smoothed exponential scale transitions, and layout resolution, leaving `Debug.cs` purely responsible for window composition.
4. **`Color.ToUint()` Extension:**
   Added `ToUint()` in [OpenRCT3/Color.cs](file:///OpenRCT3/Color.cs) to eliminate conversion gymnastics in UI code.

## Key Takeaways

1. **Extract Parameter Objects Early:**
   When a method accumulates more than 3-4 configuration parameters, immediately extract a `record struct` parameter object (`PlotLinesParameters`) instead of continuously expanding the parameter list.
2. **Separate State Management from Visual Primitives:**
   A drawing helper (`Graph.PlotLines`) should only concern itself with immediate draw commands. Stateful data buffering, sample history eviction, and scale smoothing belong in a dedicated widget component (`RollingPlot`).
3. **Delegate Layout Responsibilities Explicitly:**
   Use formal constraint protocols (`BoxConstraints`) so container windows never micromanage inner component geometry or layout calculations.

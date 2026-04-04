# Cost Estimate: Render Flat, Empty Park

**Referenced Plan**: [.opencode/plans/render-flat-empty-park.md](../plans/render-flat-empty-park.md)

## Overview

This document estimates the development time for implementing the [Render Flat, Empty Park plan](../plans/render-flat-empty-park.md), which implements rendering of a flat, empty park in the native C# OpenRCT3 client using Silk.NET.

---

## Task Breakdown

| # | Task | Plan Phase | AI (MiniMax 2.5 / Claude) | Human (Experienced) |
|---|------|------------|---------------------------|---------------------|
| 1 | Scaffold GDK project | [Phase 1: Scaffold GDK Project](../plans/render-flat-empty-park.md#phase-1-scaffold-gdk-project) | 15-30 min | 30-60 min |
| 2 | GDK primitives (Material, Mesh, ShaderProgram) | [Phase 2: GDK Primitives](../plans/render-flat-empty-park.md#phase-2-gdk-primitives-backend-agnostic) | 30-60 min | 1-2 hrs |
| 3 | RCT3 install detection (Rct3InstallFinder, AppConfig) | [Phase 3: Detect RCT3 Installation](../plans/render-flat-empty-park.md#phase-3-detect-rct3-installation) | 30-45 min | 1 hr |
| 4 | Render solid color plane (Phase 4 prototype) | [Phase 4: Render Solid Color Plane (Prototype)](../plans/render-flat-empty-park.md#phase-4-render-solid-color-plane-prototype) | 1-2 hrs | 2-4 hrs |
| 5 | ftx-viewer plugin (flexi-texture decoder) | [Phase 5: Render Textured Plane with nullbmp](../plans/render-flat-empty-park.md#phase-5-render-textured-plane-with-nullbmp) | 1-2 hrs | 3-5 hrs |
| 6 | Prototype palette conversion | [Phase 5b: Prototype Palette Conversion](../plans/render-flat-empty-park.md#phase-5b-prototype-palette-conversion-decision-prototype) | 1-2 hrs | 2-3 hrs |
| 7 | Render nullbmp texture | [Phase 5: Render Textured Plane with nullbmp](../plans/render-flat-empty-park.md#phase-5-render-textured-plane-with-nullbmp) | 1-2 hrs | 2-3 hrs |
| 8 | Render grass from terrain OVL | [Phase 6: Render Grass from Terrain OVL](../plans/render-flat-empty-park.md#phase-6-render-grass-from-terrain-ovl) | 1-2 hrs | 2-4 hrs |
| 9 | NUnit tests for GDK primitives | [Testing](../plans/render-flat-empty-park.md#testing) | 30-60 min | 1-2 hrs |

---

## Totals

| Approach | Estimated Time | Notes |
|----------|----------------|-------|
| **AI-assisted** (MiniMax 2.5 + Claude) | **7-11 hours** | Draft generation with human review cycles |
| **Human only** | **14-24 hours** | Experienced C#/OpenGL developer |

---

## Assumptions

1. **Developer Experience**: The human estimates assume an experienced C# developer familiar with OpenGL/Silk.NET and the existing codebase
2. **Existing Infrastructure**: The solution already has Silk.NET.OpenGL (v2.22.0) and OVL library dependencies in place
3. **Test Assets Available**: Developer has access to RCT3 installation with required OVL files (nullbmp.common.ovl, Terrain_RCT3.common.ovl)
4. **No Major Blockers**: Platform-specific GL context handling works as expected

---

## AI Correction Overhead

Based on historical data from [Porting libOVL to C#](../../docs/ai/Porting%20libOVL.md), AI-generated code requires manual correction for:

| Issue Type | Example from libOVL |
|------------|---------------------|
| Integer width mismatches | 32-bit vs 64-bit fields in binary format |
| Incorrect field mappings | `"bmptbl"` vs actual `"btbl"` tag |
| Logic direction errors | Reverse mapping direction in tree-view grouping |
| Missing assignments | `symbolCountOrder` never assigned |

> **⚠️ Warning**: Budget 30-50% extra time for review and correction cycles when using AI assistance. This estimate includes that overhead.

---

## References

- **Plan**: [.opencode/plans/render-flat-empty-park.md](../plans/render-flat-empty-park.md)
- **AI Usage Reference**: [Porting libOVL to C#](../../docs/ai/Porting%20libOVL.md) — Documents AI tools used (Claude, OpenCode/MiniMax) and limitations encountered
- **Existing Codebase**:
  - [OpenRCT3/Platforms/IGraphicsSurface.cs](../../OpenRCT3/Platforms/IGraphicsSurface.cs)
  - [OpenRCT3/Platforms/macOS/GameOpenGLLayer.cs](../../OpenRCT3/Platforms/macOS/GameOpenGLLayer.cs)
  - [OpenCobra/OVL/OVL.cs](../../OpenCobra/OVL/OVL.cs) — `FlexiTextureData` struct at line 252

# Dumper: macOS Maintenance Costs

## Context

The user is deciding whether to continue maintaining the macOS platform for Dumper (an OVL file dumper tool). Currently the project supports both Windows (WinForms) and macOS (AppKit) via conditional compilation.

## Key Findings

### Current State
- Dual platform support via conditional compilation (`*.macOS.cs` vs `*.windows.cs`)
- Windows uses WinForms + WebView2 for content rendering
- macOS uses AppKit with separate UI code (~50+ files differ)
- Last macOS commits: `8b1b8b4` (fix compilation), `e44961e` (test targeting)

### Option 1: Keep Dual Platforms (Status Quo)
- **Pros**: Native look and feel on each platform
- **Cons**: High maintenance burden, two separate UI codebases, feature parity challenges

### Option 2: Mark macOS as Lower Tier
- Deprecate but keep building
- Focus development on Windows
- Accept slower macOS bug fixes

### Option 3: Port to Cross-Platform Framework

| Framework | Pros | Cons |
|-----------|------|------|
| **Avalonia** | Most mature, WPF-like, actively maintained, used by JetBrains/Unity | Full rewrite required |
| **Eto.Forms** | Lightweight, binds to native toolkits | Layout performance issues (2018), platform inconsistencies, learning curve |
| **Mono WinForms** | Minimal code changes | Deprecated, limited macOS support |
| **.NET MAUI** | Microsoft's official solution | Desktop secondary to mobile |

### Eto.Forms Developer Feedback (Researched)
- **Criticisms**: Slow dynamic layouts, GTK/WinForms backend inconsistencies, API confusing for WinForms devs
- **Positive**: 4k stars, active maintenance, used in production (Rhino CAD plugins)
- **Core issue**: Abstraction leaks—platform-specific quirks still surface

## Recommendation

If cross-platform is the goal:
- **Avalonia** is the best long-term choice (mature, consistent, well-maintained)
- **Eto.Forms** only if minimal rewrite is critical (but accepts platform quirks)

If keeping native platforms:
- Mark macOS as lower-tier support
- Accept slower macOS releases

## Decision Factors
1. Do you have active macOS users?
2. How important is native macOS look vs "works on macOS"?
3. Are you willing to do a full rewrite for cross-platform consistency?
# Dumper: macOS Client Removal Summary

## Overview

The macOS client has been completely removed from the Dumper project, making it a Windows-only application. This eliminates the maintenance burden of dual-platform support while focusing development on the WinForms-based Windows client.

## Changes Made

### Phase 1: Deleted macOS-Specific Files (23 files removed)

| Category | Files Removed |
|----------|--------------|
| **Entry Point** | `Main.macOS.cs` |
| **App Delegate** | `AppDelegate.cs`, `AppDelegate.designer.cs` |
| **Windows** | `MainWindow.cs`, `MainWindow.designer.cs`, `ProjectWindow.cs`, `ProjectWindow.designer.cs` |
| **Editor** | `Editor.cs`, `Editor.designer.cs`, `EditorContainer.cs`, `EditorContainer.designer.cs`, `EditorController.cs`, `EditorController.designer.cs`, `EditorSplitView.cs` |
| **Sidebar** | `Sidebar.cs`, `Sidebar.designer.cs` |
| **Error Handling** | `Error.cs`, `NSErrorExtensions.cs` |
| **Documents** | Entire `Documents/` folder (7 files): `DocumentController.cs`, `OvlDocument.cs`, `OvlViewController.cs`, `OvlViewController.designer.cs`, `OvlWindowController.cs`, `OvlWindowController.designer.cs`, `ProjectDocument.cs` |
| **macOS Config** | `Main.storyboard`, `Entitlements.plist`, `Dumper.entitlements`, `Info.plist`, `Assets.xcassets/` directory |

### Phase 2: Simplified Dumper.csproj

- Removed all macOS-specific `<Choose>` blocks and conditionals
- Removed macOS-specific PropertyGroup entries (codesigning, mono runtime, etc.)
- Removed `<ItemGroup>` conditions for `*.macOS.cs` and `*.windows.cs`
- Removed conditional Windows Forms exclusions (no longer needed)
- Consolidated to a single Windows-only configuration
- Removed `Entitlements.plist` reference

### Phase 3: Renamed Windows Entry Point

- Renamed `Main.windows.cs` → `Main.cs` for cleaner code
- This is now the single entry point for the application

### Phase 4: Verification

- Build succeeded with **0 warnings** and **0 errors**
- Project targets `net8.0-windows` with WinForms

## Impact

### Removed Capabilities
- macOS native UI (AppKit-based)
- Document-based architecture (NSDocument)
- macOS storyboards and UI designer
- macOS-specific error handling

### Retained Capabilities
- Windows WinForms UI (MainForm)
- OVL file viewing and dumping
- All core functionality

## Benefits

1. **Reduced Complexity**: Single platform eliminates conditional compilation
2. **Faster Development**: No need to maintain two separate UI codebases
3. **Clearer Codebase**: ~30% fewer files, all platform-agnostic
4. **Simplified Build**: Single configuration for Windows only

## Recommendations

If cross-platform support is desired in the future, consider:
- **Avalonia UI** (recommended): Most mature, WPF-like, actively maintained
- **Eto.Forms**: Lightweight but has known platform quirks
- **.NET MAUI**: Microsoft's official solution (desktop secondary to mobile)

## Files Modified

| File | Action |
|------|--------|
| `Dumper.csproj` | Simplified to Windows-only configuration |
| `Main.windows.cs` | Renamed to `Main.cs` |

## Build Output

```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.37
```

## Conclusion

The macOS client removal was successful. The Dumper project is now a clean, Windows-only WinForms application with no macOS dependencies. The build compiles without errors or warnings.
# Extism Plugin System for OVL Resource Viewers Results

## Summary

Built an Extism-powered plugin system that lets `.wasm` plugins render OVL file resources in the Dumper's content
panel when a tree node is selected. The content panel was previously empty; it now hosts a WebView2 (Windows) /
WKWebView (macOS) view that displays plugin-rendered HTML, with a header bar for the plugin name and a viewer-switcher
dropdown.

## Plugin Discovery

Plugins are `.wasm` files loaded from, in order:

- Bundled: `{app_dir}/plugins/*.wasm`
- Windows: `%APPDATA%/OpenRCT3/Plugins/*.wasm`, then `%USERDOCUMENTS%/OpenRCT3/Plugins/*.wasm`
- macOS: `~/Library/Application Support/OpenRCT3/Plugins/*.wasm`, then `~/Documents/OpenRCT3/Plugins/*.wasm`

Each is loaded via `CompiledPlugin`. `PluginManager` builds a `FileType` tag → ordered `IViewerPlugin` list; the first
entry per type is the default viewer. Plugins run sandboxed with a 50-unit fuel limit.

## Files Added

```
Dumper/
├── Plugins/
│   ├── IViewerPlugin.cs            # Name, Version, SupportedFileTypes, Render(byte[])
│   ├── ViewerPlugin.cs             # Extism wrapper around a loaded Plugin
│   ├── PluginManager.cs            # Discovery, loading, routing by tag
│   ├── PluginInfo.cs               # DTO: name, version, supported types, source path
│   └── DefaultViewerChooser.cs     # Dialog for picking the default viewer per file type
├── ContentPanel.cs / .Designer.cs        # WinForms: header bar + WebView2
├── ContentPanelHeader.cs / .Designer.cs  # WinForms: plugin name label + viewer dropdown
```

macOS: `EditorController` gained a header `NSView` (name label + `NSPopUpButton`) and an embedded `WKWebView`.

## Host-Side Wiring

- `OVL.csproj` grants `InternalsVisibleTo` for Dumper so it can resolve virtual addresses to raw byte slices via
  `CommonData`, `UniqueData`, `OvlFileData`, and `FileBlockEntry`.
- `MainForm.cs` handles `treeView.AfterSelect`: reads the node's `FileType` tag, looks up matching plugins, extracts
  raw bytes, and renders via the default plugin into the content panel.
- Tree leaf nodes got a context menu: "Open With" (submenu of matching viewers, default bolded, plus "Choose a
  default viewer..." opening `DefaultViewerChooser`), "Export" (stubbed as `NotImplementedException`), and
  "Properties" (file type, relocation info, sizes, viewer availability).

## Plugin Contract

Every viewer plugin exports `name() → string`, `version() → string`, `file_types() → string` (JSON array), and
`render(bytes) → string` (HTML fragment).

## Dependencies Added

`Extism.Sdk`, `Extism.runtime.all`, and `Microsoft.Web.WebView2` (Windows) to `Dumper`.

## Notes

This plan is the foundation the [AssemblyScript int/txt viewer plugins](assemblyscript-plugins.md) were built against.

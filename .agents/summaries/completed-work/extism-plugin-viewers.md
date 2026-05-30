# Extism Plugin System for OVL Resource Viewers

Dumper gains an Extism-powered plugin system so `.wasm` plugins can render OVL file resources in the content panel. Plugins are discovered from bundled and user directories, wrapped via `PluginManager`, and rendered in a `ContentPanel` using WebView2.

Key components:
- `IViewerPlugin` interface with `Name`, `Version`, `SupportedFileTypes`, `Render(byte[])`
- `ViewerPlugin` wraps Extism `Plugin` instance
- `PluginManager` handles discovery, loading, and routing by file type
- `ContentPanel` is a `UserControl` with header bar + WebView2
- Requires `InternalsVisibleTo` from OVL for virtual address resolution

Implementation order: IViewerPlugin → ViewerPlugin → PluginManager → ContentPanel → wire in MainForm → context menu → macOS.

# Extism Plugin System for OVL Resource Viewers

## Context

Dumper currently only displays the OVL resource tree in the left panel. The right content panel is empty. This plan adds
an Extism-powered plugin system so that `.wasm` plugins can render OVL file resources in the content panel when a tree
node is selected.

## Plugin Discovery

Plugins are `.wasm` files discovered at startup from these locations (in order):

**Bundled:**

- `{app_dir}/plugins/*.wasm`

**User data (OS-specific):**

- **Windows:** `%APPDATA%/OpenRCT3/Plugins/*.wasm` then `%USERDOCUMENTS%/OpenRCT3/Plugins/*.wasm`
- **macOS:** `~/Library/Application Support/OpenRCT3/Plugins/*.wasm` then `~/Documents/OpenRCT3/Plugins/*.wasm`

Each `.wasm` file is loaded via `CompiledPlugin` (pre-compiled for performance). A `PluginManager` builds a dictionary
mapping `FileType` tag → ordered list of `IViewerPlugin` instances. The first plugin for each type is the default
viewer.

## WASM Plugin Contract

Every viewer plugin must export:

| Export       | Signature          | Purpose                               |
| ------------ | ------------------ | ------------------------------------- |
| `name`       | `() → string`      | Display name, e.g. `"Texture Viewer"` |
| `version`    | `() → string`      | Semantic version                      |
| `file_types` | `() → string`      | JSON array: `["tex","ftx"]`           |
| `render`     | `(bytes) → string` | Raw resource bytes → HTML fragment    |

Plugins are sandboxed with a default fuel limit of **50 units**.

## Host-Side Class Hierarchy

```
Dumper/
├── Plugins/
│   ├── IViewerPlugin.cs            # Interface: Name, Version, SupportedFileTypes, Render(byte[])
│   ├── ViewerPlugin.cs             # Extism wrapper: calls WASM exports, manages Plugin lifecycle
│   ├── PluginManager.cs            # Discovery, loading, routing by tag, conflict list
│   └── PluginInfo.cs               # DTO: name, version, supported types, source path
├── ContentPanel.cs                 # WinForms UserControl Code-Behind: header bar + WebView2
├── ContentPanel.Designer.cs        # WinForms designer code
├── ContentPanelHeader.cs           # Header bar code-behind: centered plugin name + right-aligned viewer dropdown
├── ContentPanelHeader.Designer.cs  # WinForms designer code
├── MainForm.cs                     # Wire treeView.AfterSelect → PluginManager → ContentPanel
```

### `IViewerPlugin`

```csharp
interface IViewerPlugin : IDisposable {
    PluginInfo Info { get; }
    IReadOnlyList<string> SupportedFileTypes { get; }
    string Render(byte[] data);
}
```

### `ViewerPlugin`

Wraps an Extism `Plugin` instance. On construction, calls `name`, `version`, `file_types` exports. The `Render` method
calls the `render` export with `FuelLimit = 50`.

### `PluginManager`

```csharp
class PluginManager {
    void LoadAll();                    // scans discovery paths, builds registry
    IReadOnlyList<IViewerPlugin> GetViewers(string fileTypeTag);  // all matching plugins
    IViewerPlugin? GetDefaultViewer(string fileTypeTag);          // first match
}
```

## Content Panel (WinForms — Windows)

`ContentPanel` is a new `UserControl` with this layout:

```
┌─────────────────────────────────────────────┐
│ [ HeaderPanel - Dock=Top, Height=32 ]       │
│   ┌──────────────────────┐  ┌────────────┐  │
│   │  PluginNameLabel     │  │ ▼ ComboBox  │  │
│   │  (Centered/Anchored) │  │ (Right)     │  │
│   └──────────────────────┘  └────────────┘  │
├─────────────────────────────────────────────┤
│  WebView2 (Dock=Fill)                       │
│  - Renders HTML from plugin Render()        │
│  - Fallback message for no-viewer state     │
└─────────────────────────────────────────────┘
```

## Content Panel (macOS — AppKit)

Integrates into the existing `EditorController` / `EditorContainer`:

- Add a header `NSView` to `EditorController`
- Header contains: `NSTextField` (centered plugin name) + `NSPopUpButton` (viewer dropdown)
- Embed a `WKWebView` below the header

## OVL Data Access — Required Changes to OpenCobra/OVL

Add `InternalsVisibleTo` for Dumper in `OVL.csproj` so Dumper can access `CommonData`, `UniqueData`, `OvlFileData`, and
`FileBlockEntry` to resolve virtual addresses to raw byte slices.

## Tree View Integration (MainForm.cs)

Add `treeView.AfterSelect` handler that:

1. Reads `node.Tag` as `FileType`
2. Looks up matching plugins via `PluginManager.GetViewers(tag)`
3. Extracts raw bytes from OVL via virtual address resolution
4. Passes bytes to default plugin's `Render()`
5. Shows result in content panel

## Context Menu in Tree View

On leaf tree nodes, assign a context menu with these items:

- "Open With" item:

  - Sub-menu listing all matching `FileType` viewer plugins

    - Default viewer is bolded

    - Divider at the end of the list, followed by

    - "Choose a default viewer..." that opens a `Plugins/DefaultViewerChooser.cs` dialog that responds with a
      DialogResult of the selected `FileType`. The form contains a dropdown of file types and these buttons:

      - "Set Default": The dialog's default action, i.e. keyboard accessible in the normal WinForms way

      - Cancel: Cancels the dialog

- Export: Throws `NotImplementedException`

- Divider

- Properties: Opens a simple dialog detailing the file type, relocation info, compressed and/or uncompressed size, and
  whether a matching viewer exists

## Dependencies

| Package                  | Target           | Purpose             |
| ------------------------ | ---------------- | ------------------- |
| `Extism.Sdk`             | Dumper           | Plugin host SDK     |
| `Extism.runtime.all`     | Dumper           | Native WASM runtime |
| `Microsoft.Web.WebView2` | Dumper (Windows) | Content rendering   |

## Implementation Order

1. Add `InternalsVisibleTo` to OVL.csproj for Dumper
2. Create `Plugins/IViewerPlugin.cs` and `Plugins/PluginInfo.cs`
3. Create `Plugins/ViewerPlugin.cs` — Extism SDK wrapper
4. Create `Plugins/PluginManager.cs` — discovery, loading, routing
5. Create `ContentPanel.cs` and `ContentPanelHeader.cs` — WinForms content panel with WebView2
6. Wire `MainForm.cs` — add `AfterSelect`, populate content panel
7. Add right-click context menu on tree nodes for viewer selection
8. macOS: modify `EditorController` — add WKWebView + header bar

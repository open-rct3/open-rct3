// PluginManager
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.OVL;

namespace Dumper.Plugins;

/// <summary>Discovers, loads, and routes Extism viewer plugins by OVL file type tag.</summary>
#pragma warning disable IDE0305 // Simplify collection initialization
sealed class PluginManager : IDisposable {
  private readonly Dictionary<string, List<IViewerPlugin>> registry = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>All loaded plugins, keyed by their source path.</summary>
  public IReadOnlyList<IViewerPlugin> AllPlugins { get; private set; }

  /// <summary>
  /// The archive currently open in the host UI, if any. Read by every loaded plugin's
  /// "ovl" host functions (see <see cref="ViewerPlugin"/>) at call time - plugins are loaded
  /// once at startup, before any archive is open, so this can't be baked in at load time and
  /// must be a live reference the host updates on <c>MainForm.LoadOvl</c>.
  /// </summary>
  public Ovl? CurrentOvl { get; set; }

  /// <summary>
  /// The symbol currently being rendered, if any - lets the "ovl" host functions answer
  /// "current_resource_address" (a plugin's own resource address, needed to compute e.g.
  /// <c>shapeAddress + 40</c> for a StaticShape's <c>sh[]</c> field) without changing
  /// <see cref="IViewerPlugin.Render"/>'s signature. Set by <c>MainForm</c> alongside
  /// <c>ContentPanel.ShowContent</c>.
  /// </summary>
  public OvlFile? CurrentFile { get; set; }

  public PluginManager() =>
    AllPlugins = Load().Cast<IViewerPlugin>().ToList();

  /// <summary>Discover and load all plugins from standard search paths.</summary>
  public IEnumerable<IPlugin> Load() {
    var wasmFiles = new List<string>();

    foreach (var dir in GetSearchPaths()) {
      if (!Directory.Exists(dir)) continue;
      try {
        wasmFiles.AddRange(Directory.EnumerateFiles(dir, "*.wasm", SearchOption.TopDirectoryOnly));
      } catch (DirectoryNotFoundException) {
        // Silently ignore this, the user may have deleted or misconfigured something
      }
    }

    return wasmFiles.Select(wasmPath => {
      try {
        var plugin = ViewerPlugin.Load(wasmPath, () => CurrentOvl, () => CurrentFile);

        // Add plugin to dictionary of OVL file plugins
        foreach (var tag in plugin.SupportedFileTypes) {
          if (!registry.TryGetValue(tag, out var list)) {
            list = [];
            registry[tag] = list;
          }
          list.Add(plugin);
        }

        return plugin;
      } catch (Exception ex) {
        Debug.WriteLine($"Failed to load plugin '{wasmPath}': {ex.Message}");
        return null;
      }
    }).Where(plugin => plugin != null).Cast<IPlugin>();
  }
#pragma warning restore IDE0305 // Simplify collection initialization

  /// <summary>Get all viewer plugins that support the given file type tag.</summary>
  public IReadOnlyList<IViewerPlugin> GetViewers(string fileTypeTag) {
    if (registry.TryGetValue(fileTypeTag, out var list))
      return list;
    return [];
  }

  /// <summary>Get the default (first) viewer plugin for the given file type tag.</summary>
  public IViewerPlugin? GetDefaultViewer(string fileTypeTag) {
    var viewers = GetViewers(fileTypeTag);
    return viewers.Count > 0 ? viewers[0] : null;
  }

  /// <summary>Set the default viewer for a file type tag by promoting it to the front of the list.</summary>
  public void SetDefaultViewer(string fileTypeTag, IViewerPlugin plugin) {
    if (!registry.TryGetValue(fileTypeTag, out var list)) return;
    var idx = list.IndexOf(plugin);
    if (idx <= 0) return;
    list.RemoveAt(idx);
    list.Insert(0, plugin);
  }

  /// <summary>Get a snapshot of the registry for use in UI (e.g. default viewer chooser).</summary>
  internal Dictionary<string, List<IViewerPlugin>> GetRegistrySnapshot() =>
    new(registry, StringComparer.OrdinalIgnoreCase);

  public void Dispose() {
    foreach (var plugin in AllPlugins)
      plugin.Dispose();
    registry.Clear();
  }

  /// <summary>Enumerate standard plugin search paths.</summary>
  private static IEnumerable<string> GetSearchPaths() {
    // 1. Bundled: next to the executable
    var exeDir = AppContext.BaseDirectory;
    yield return Path.Combine(exeDir, "plugins");

    // 2. User data directories (OS-specific)
    if (OperatingSystem.IsWindows()) {
      // %APPDATA%/OpenRCT3/Plugins
      var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      if (!string.IsNullOrEmpty(appData))
        yield return Path.Combine(appData, "OpenRCT3", "Plugins");

      // %USERPROFILE%/Documents/OpenRCT3/Plugins
      var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      if (!string.IsNullOrEmpty(docs))
        yield return Path.Combine(docs, "OpenRCT3", "Plugins");
    } else if (OperatingSystem.IsMacOS()) {
      // ~/Library/Application Support/OpenRCT3/Plugins
      var appSupport = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support");
      if (!string.IsNullOrEmpty(appSupport))
        yield return Path.Combine(appSupport, "OpenRCT3", "Plugins");

      // ~/Documents/OpenRCT3/Plugins
      var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      if (!string.IsNullOrEmpty(docs))
        yield return Path.Combine(docs, "OpenRCT3", "Plugins");
    }
  }
}

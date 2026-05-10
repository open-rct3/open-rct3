// PluginManager
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace Dumper.Plugins;

/// <summary>Discovers, loads, and routes Extism viewer plugins by OVL file type tag.</summary>
sealed class PluginManager : IDisposable {
  private readonly Dictionary<string, List<IViewerPlugin>> registry = new(StringComparer.OrdinalIgnoreCase);

  /// <summary>All loaded plugins, keyed by their source path.</summary>
  public IReadOnlyList<IViewerPlugin> AllPlugins { get; private set; } = [];

  public PluginManager() => Load();

  /// <summary>Discover and load all plugins from standard search paths.</summary>
  public void Load() {
    var wasmFiles = new List<string>();

    foreach (var dir in GetSearchPaths()) {
      if (!Directory.Exists(dir)) continue;
      try {
        wasmFiles.AddRange(Directory.EnumerateFiles(dir, "*.wasm", SearchOption.TopDirectoryOnly));
      } catch (DirectoryNotFoundException) {
        // Silently ignore this, the user may have deleted or misconfigured something
      }
    }

    AllPlugins = [.. wasmFiles.Select(wasmPath => {
      try {
        var plugin = ViewerPlugin.Load(wasmPath);

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
      }

      return null;
    }).Where(plugin => plugin != null) as IEnumerable<IViewerPlugin>];
  }

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

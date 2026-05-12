// ContentPanel
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Dumper.Plugins;
using OpenCobra.OVL.Files;

namespace Dumper;

/// <summary>WinForms content panel that renders plugin output via WebView2.</summary>
sealed partial class ContentPanel : UserControl {

  private byte[]? currentData;
  private List<IViewerPlugin> currentViewers = [];
  private IViewerPlugin? activePlugin;
  private bool webViewReady;

  /// <summary>Fired when the user picks a different file viewer.</summary>
  public event EventHandler<IViewerPlugin?>? ActiveViewerChanged;

  public ContentPanel() {
    InitializeComponent();
    header.ViewerChanged += OnViewerChanged;
  }

  /// <summary>Initialize the WebView2 runtime. Call once after the control is added to a form.</summary>
  public async Task InitializeAsync() {
    try {
      await webView.EnsureCoreWebView2Async();
      webViewReady = true;
      ShowEmpty();
    } catch {
      // WebView2 runtime not available
    }
  }

  /// <summary>Show rendered content from a plugin.</summary>
  public void ShowContent(string fileName, IEnumerable<IViewerPlugin> viewers, byte[] data) {
    currentViewers.Clear();
    currentViewers.AddRange(viewers);
    currentData = data;
    activePlugin = currentViewers[0];

    header.SetViewers(fileName, activePlugin, viewers);
    RenderCurrent();
  }

  /// <summary>Show a message indicating no viewer is available for this file type.</summary>
  public void ShowNoViewer(FileType fileType) {
    Reset();
    header.SetMessage(fileType.ToDisplayName());
    Navigate(EmptyView.RenderNoViewer($"{fileType.ToDisplayName()}s"));
  }

  /// <summary>Clear the content panel to its empty state.</summary>
  public void ShowEmpty(bool isOvlOpened = false) {
    Reset();
    header.SetMessage("");
    Navigate(EmptyView.Render(isOvlOpened ? "Select a file to view." : "Open an OVL archive."));
  }

  private void Reset() {
    currentData = null;
    currentViewers.Clear();
    activePlugin = null;
  }

  private void OnViewerChanged(object? sender, IViewerPlugin? plugin) {
    if (plugin == null || currentData == null) return;
    activePlugin = plugin;
    RenderCurrent();
    ActiveViewerChanged?.Invoke(this, plugin);
  }

  private void RenderCurrent() {
    if (activePlugin == null || currentData == null) return;

    var html = activePlugin.Render(currentData);
    Navigate(EmptyView.WrapInShell(html));
  }

  private void Navigate(string html) {
    if (webViewReady && webView.CoreWebView2 != null)
      webView.CoreWebView2.NavigateToString(html);
  }
}

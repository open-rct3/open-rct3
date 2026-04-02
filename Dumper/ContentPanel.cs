// ContentPanel
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Dumper.Plugins;
using OVL.Files;

namespace Dumper;

/// <summary>WinForms content panel that renders plugin output via WebView2.</summary>
sealed partial class ContentPanel : UserControl {

  private byte[]? _currentData;
  private IReadOnlyList<IViewerPlugin>? _currentViewers;
  private IViewerPlugin? _activePlugin;
  private bool _webViewReady;

  /// <summary>Fired when the user picks a different file viewer.</summary>
  public event EventHandler<IViewerPlugin?>? ActiveViewerChanged;

  public ContentPanel() {
    InitializeComponent();
    header.ViewerChanged += OnViewerChanged;
  }

  /// <summary>Initialize the WebView2 runtime. Call once after the control is added to a form.</summary>
  public async System.Threading.Tasks.Task InitializeAsync() {
    try {
      await webView.EnsureCoreWebView2Async();
      _webViewReady = true;
      ShowEmpty();
    } catch {
      // WebView2 runtime not available
    }
  }

  /// <summary>Show rendered content from a plugin.</summary>
  public void ShowContent(IReadOnlyList<IViewerPlugin> viewers, byte[] data) {
    _currentViewers = viewers;
    _currentData = data;
    _activePlugin = viewers[0];

    header.SetViewers(_activePlugin, viewers);
    RenderCurrent();
  }

  /// <summary>Show a message indicating no viewer is available for this file type.</summary>
  public void ShowNoViewer(FileType fileType) {
    Reset();
    header.SetMessage(fileType.ToDisplayName());
    Navigate(EmptyView.RenderNoViewer($"{fileType.ToDisplayName()}s"));
  }

  /// <summary>Clear the content panel to its empty state.</summary>
  public void ShowEmpty() {
    Reset();
    header.SetMessage("");
    Navigate(EmptyView.Render("Select a file to view."));
  }

  private void Reset() {
    _currentData = null;
    _currentViewers = null;
    _activePlugin = null;
  }

  private void OnViewerChanged(object? sender, IViewerPlugin? plugin) {
    if (plugin == null || _currentData == null) return;
    _activePlugin = plugin;
    RenderCurrent();
    ActiveViewerChanged?.Invoke(this, plugin);
  }

  private void RenderCurrent() {
    if (_activePlugin == null || _currentData == null) return;

    var html = _activePlugin.Render(_currentData);
    Navigate(EmptyView.WrapInShell(html));
  }

  private void Navigate(string html) {
    if (_webViewReady && webView.CoreWebView2 != null)
      webView.CoreWebView2.NavigateToString(html);
  }
}

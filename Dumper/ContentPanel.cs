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

  /// <summary>Fired when the user picks a different viewer from the header dropdown.</summary>
  public event EventHandler<IViewerPlugin?>? ActiveViewerChanged;

  public ContentPanel() {
    InitializeComponent();
    header.ViewerChanged += OnViewerChanged;
  }

  /// <summary>Initialize the WebView2 runtime. Call once after the control is added to a form.</summary>
  public async System.Threading.Tasks.Task InitializeAsync() {
    try {
      await webView.EnsureCoreWebView2Async();
    } catch {
      webView.Visible = false;
    }
  }

  /// <summary>Show rendered content from a plugin.</summary>
  public void ShowContent(IReadOnlyList<IViewerPlugin> viewers, byte[] data) {
    _currentViewers = viewers;
    _currentData = data;
    _activePlugin = viewers[0];

    header.SetViewers(_activePlugin, viewers);
    RenderCurrent();

    emptyLabel.Visible = false;
    webView.Visible = true;
  }

  /// <summary>Show a message indicating no viewer is available for this file type.</summary>
  public void ShowNoViewer(FileType fileType) {
    Reset();
    header.SetMessage(fileType.ToDisplayName());
    emptyLabel.Text = $"No viewer for {fileType.ToString()} files";
    emptyLabel.Visible = true;
    webView.Visible = false;
  }

  /// <summary>Clear the content panel to its empty state.</summary>
  public void ShowEmpty() {
    Reset();
    header.SetMessage("");
    emptyLabel.Text = "Select a file to view";
    emptyLabel.Visible = true;
    webView.Visible = false;
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
    var wrappedHtml = WrapHtml(html, _activePlugin.Info.Name);

    if (webView.CoreWebView2 != null) {
      webView.CoreWebView2.NavigateToString(wrappedHtml);
    }
  }

  private static string WrapHtml(string body, string title) {
    return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body {{
    margin: 0;
    padding: 16px;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    font-size: 14px;
    color: #1a1a1a;
    background: #fff;
  }}
  pre {{
    background: #f5f5f5;
    padding: 12px;
    border-radius: 4px;
    overflow-x: auto;
    font-family: 'Cascadia Code', 'Fira Code', monospace;
    font-size: 13px;
  }}
  img {{
    max-width: 100%;
    height: auto;
  }}
  table {{
    border-collapse: collapse;
    width: 100%;
  }}
  th, td {{
    border: 1px solid #ddd;
    padding: 6px 10px;
    text-align: left;
  }}
  th {{
    background: #f0f0f0;
    font-weight: 600;
  }}
</style>
</head>
<body>
{body}
</body>
</html>";
  }
}

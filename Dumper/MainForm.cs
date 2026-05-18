// Main Form
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2025-2026 OpenRCT3 Contributors. All rights reserved.
using System.Security;
using Dumper.Plugins;
using Dumper.Settings;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper;

public partial class MainForm : Form {
  readonly static string ready = "Ready";
  readonly static string openingArchive = "Opening archive…";
  readonly static string resourcesFmt = "{0} Resources";
  readonly static string ovlFmt = "{0} OVLs";

  private readonly Properties.Settings settings = Properties.Settings.Default;
  private readonly PluginManager pluginManager = new();
  private Ovl? currentOvl;
  private readonly Dictionary<TreeNode, OvlFile> _nodeEntries = [];
  private bool suppressSplitterMoved = false;

  public MainForm() {
    InitializeComponent();
    InitializeComponentIcons();

    settings.Reload();
    TryFindRct3();
    TryLoadPlugins();
  }

  protected override void OnShown(EventArgs e) {
    base.OnShown(e);

    // Initialize web view after the form is shown (requires message loop)
    _ = contentPanel.InitializeAsync().ContinueWith(_ => {
      try {
        return Task.FromResult(Invoke(() => contentPanel.Visible = true));
      } catch (Exception exception) {
        return Task.FromException<bool>(exception);
      }
    }
    );
  }

  protected override void OnFormClosed(FormClosedEventArgs e) {
    pluginManager.Dispose();

    base.OnFormClosed(e);
  }

  private async Task OpenOvl() {
    var lastOvlOpened = settings.LastOvlOpened;
    openDialog.InitialDirectory = lastOvlOpened != null
      ? Directory.GetParent(lastOvlOpened)?.FullName ?? ""
      : "";
    openDialog.FileName = Path.GetFileName(lastOvlOpened);
    if (openDialog.ShowDialog() != DialogResult.OK) return;

    this.Cursor = Cursors.WaitCursor;
    LoadOvl(await Task.Run(() => Ovl.Load(openDialog.FileName)));
    this.Cursor = Cursors.Default;

    settings.LastOvlOpened = openDialog.FileName;
    settings.Save();
  }

  private void LoadOvl(Ovl ovl) {
    currentOvl = ovl;
    contentPanel.ShowEmpty(true);

    // Update window title with document name
    var docName = Path.GetFileName(ovl.Keys.First().Path);
    var lower = docName.ToLower();
    if (lower.EndsWith(".common.ovl"))
      docName = docName[..^".common.ovl".Length];
    else if (lower.EndsWith(".unique.ovl"))
      docName = docName[..^".unique.ovl".Length];
    else if (lower.EndsWith(".ovl"))
      docName = docName[..^".ovl".Length];
    else
      docName = Ovl.UnnamedOvl;
    Text = $@"OVL Dumper — {docName}";

    EnsureTreeImageList();
    treeView.Initialize(pluginManager, imageList!);
    treeView.LoadOvl(ovl);

    UpdateStatusBar();

    FitSidebarToContent(ClientSize.Width / 2);
  }

  private void UpdateStatusBar() {
    var ovlCount = currentOvl?.Keys.GroupBy(e => e.Path).Count() ?? 0;
    var resourceCount = treeView.CountLeafNodes();
    ovlCountLabel.Text = string.Format(ovlFmt, ovlCount);
    resourceCountLabel.Text = string.Format(resourcesFmt, resourceCount);
  }

  private void ClearStatusBarCounts() {
    ovlCountLabel.Text = "";
    resourceCountLabel.Text = "";
  }

  private ImageList? imageList;

  private void EnsureTreeImageList() {
    if (imageList != null) return;

    var icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    imageList = new ImageList { ImageSize = IconSize };

    // Add folder icon for file group nodes
    imageList.Images.Add("FolderOpen", Icons.Render(icons, "FolderOpen", Icons.Folder)!);

    // Add icons for each file type, skipping unknown icon names
    foreach (var fileType in Enum.GetValues<FileType>()) {
      var iconName = fileType.ToIconName();
      if (!imageList.Images.ContainsKey(iconName)) {
        var bmp = Icons.Render(icons, iconName);
        if (bmp != null)
          imageList.Images.Add(iconName, bmp);
      }
    }
  }

  private void TryLoadPlugins() {
    // Load plugins
    try {
      pluginManager.Load();
    } catch (Exception ex) {
      Debug.WriteLine($"Plugin loading failed: {ex.Message}");

      if (ex is UnauthorizedAccessException || ex is SecurityException) {
        // TODO: Detect if this is a user-land problem and message the user
      }

      // Show a retry offer to the user
      var result = MessageBox.Show(
        "Could not load plugins.\n\nTry again or continue anyway?",
        @"Error",
        MessageBoxButtons.CancelTryContinue,
        MessageBoxIcon.Error
      );

      if (result == DialogResult.TryAgain) TryLoadPlugins();
      // User cancelled the operation, confirm app exit
      else if (result == DialogResult.Cancel && TryExit() == DialogResult.Yes)
        Application.Exit();
    }
  }

  private void TryFindRct3() {
    try {
      // Add user's RCT3 dir to the open modal
      var rct3Dir = settings.Rct3Dir = settings.Rct3Dir ?? InstallFinder.Find();
      settings.Save();
      openDialog.CustomPlaces.Add(rct3Dir);
    } catch (InstallNotFoundException) {
      MessageBox.Show(
        "Could not automatically find your local RCT3 installation.\n\nYou can set the path in Tools > Settings...",
        "RCT3 Not Found",
        MessageBoxButtons.OK,
        MessageBoxIcon.Warning
      );
    }
  }

  private static DialogResult TryExit() => MessageBox.Show(
    "Are you sure you want to exit?",
    "Exit Dumper?",
    MessageBoxButtons.YesNo,
    MessageBoxIcon.Question,
    MessageBoxDefaultButton.Button2
  );

  private async void OpenMenuItem_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    ClearStatusBarCounts();
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private async void OpenArchive_Click(object sender, EventArgs e) {
    statusLabel.Text = openingArchive;
    progressBar.Visible = true;
    ClearStatusBarCounts();
    await OpenOvl();
    statusLabel.Text = ready;
    progressBar.Visible = false;
  }

  private void ExitMenuItem_Click(object sender, EventArgs e) {
    if (TryExit() == DialogResult.Yes) Application.Exit();
  }

  private void PluginsMenuItem_Click(object sender, EventArgs e) =>
    new PluginsDialog(pluginManager.AllPlugins).ShowDialog(this);

  private void Splitter_MouseDoubleClick(object? sender, MouseEventArgs e) {
    if (currentOvl == null) return;
    FitSidebarToContent(ClientSize.Width / 2);
  }

  private void SplitView_SplitterMoved(object? sender, SplitterEventArgs e) => ClampSplitterDistance();
  private void SplitView_SizeChanged(object? sender, EventArgs e) => ClampSplitterDistance();

  private void ClampSplitterDistance() {
    if (suppressSplitterMoved) return;
    // Prevent infinite loops when user's adjust the splitter distance
    suppressSplitterMoved = true;
    try {
      var maxAllowed = splitView.Width / 2;
      if (splitView.SplitterDistance > maxAllowed) {
        splitView.SplitterDistance = maxAllowed;
      }
    } finally {
      suppressSplitterMoved = false;
    }
  }

  private void FitSidebarToContent(int maxWidth) {
    int contentWidth = 200; // Stub, handled in FileTree now if needed

    var padding = SystemInformation.VerticalScrollBarWidth + 8;
    // Clamp maximum width to no more than 25% wider than content width
    var maxAllowedWidth = (int)(contentWidth * 1.25);
    maxWidth = Math.Min(maxWidth, maxAllowedWidth);

    var target = Math.Min(contentWidth + padding, maxWidth);
    target = Math.Min(target, splitView.Width - splitView.Panel2MinSize);
    target = Math.Max(target, splitView.Panel1MinSize);
    splitView.SplitterDistance = target;
  }
}

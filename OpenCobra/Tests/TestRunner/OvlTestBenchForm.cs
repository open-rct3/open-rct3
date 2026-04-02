using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SysFile = System.IO.File;
using SysDirectory = System.IO.Directory;
using OvlType = OVL.OvlType;
using DColor = System.Drawing.Color;

namespace OvlTestBench;

public partial class OvlTestBenchForm : Form {
    private readonly TabControl tabControl;
    private readonly TreeView testResultsTree;
    private readonly TreeView diagResultsTree = null!;
    private readonly TabPage testTab;
    private readonly TabPage? diagTab;
    private readonly ProgressBar progressBar;
    private readonly Label statusLabel;
    private readonly Label configLabel;
    private readonly Button startButton;
    private readonly Button diagButton = null!;
    private readonly Label timingLabel; // New right-aligned timing label
    private bool running;
    private readonly string? configPath;
    private readonly ExtraOvlConfig? config;
    private readonly TreeView testTreeView; // New tree view for test bed list
    private readonly Panel treePanel; // Panel to contain tree view
    private readonly Panel topPanel; // Panel for top controls
    private Stopwatch? stopwatch;

    public OvlTestBenchForm() {
        Text = "Frontier OVL Test Bench";
        Size = new Size(1000, 700);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;

        // Initialize config path
        configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ovl-tests.local.json");
        if (SysFile.Exists(configPath)) {
            var json = SysFile.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<ExtraOvlConfig>(json);
        }

        // Create main layout containers
        var mainPanel = new Panel {
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };

        // Create top panel with controls
        topPanel = new FlowLayoutPanel {
            Dock = DockStyle.Top,
            Height = 60,
            WrapContents = false,
            Padding = new Padding(5)
        };

        startButton = new Button {
            Text = "Start",
            Width = 80,
            Height = 30
        };
        startButton.Click += StartButton_Click;
        topPanel.Controls.Add(startButton);

        if (config != null) {
            diagButton = new Button {
                Text = "Run Diagnostics",
                Width = 120,
                Height = 30
            };
            diagButton.Click += DiagButton_Click;
            topPanel.Controls.Add(diagButton);
        }

        // Create config label with GDI path trimming
        configLabel = new Label {
            AutoSize = false,
            Width = 300,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(5)
        };
        topPanel.Controls.Add(configLabel);

        // Create timing label (right-aligned)
        timingLabel = new Label {
            AutoSize = false,
            Width = 150,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight,
            BorderStyle = BorderStyle.None,
            Padding = new Padding(5),
            ForeColor = SystemColors.GrayText
        };
        topPanel.Controls.Add(timingLabel);

        // Create status label
        statusLabel = new Label {
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Ready",
            Padding = new Padding(4, 0, 0, 0),
            ForeColor = SystemColors.GrayText
        };

        // Create progress bar
        progressBar = new ProgressBar {
            Dock = DockStyle.Top,
            Height = 20,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 5, 0, 5)
        };

        // Create tree view for test bed list
        testTreeView = new TreeView {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            Scrollable = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9f),
            FullRowSelect = true
        };

        // Enable double buffering to reduce flickering
        typeof(TreeView).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, testTreeView, new object[] { true });

        treePanel = new Panel {
            Dock = DockStyle.Top,
            Height = 200,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.FixedSingle
        };
        treePanel.Controls.Add(testTreeView);

        // Create test results tree view
        testResultsTree = new TreeView {
            Dock = DockStyle.Fill,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            Scrollable = true,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9f),
            FullRowSelect = true
        };

        // Enable double buffering for test results tree
        typeof(TreeView).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, testResultsTree, new object[] { true });

        testTab = new TabPage("Tests") {
            Padding = new Padding(2),
        };
        testTab.Controls.Add(testResultsTree);

        tabControl = new TabControl {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(100, 25),
            Alignment = TabAlignment.Top
        };
        tabControl.TabPages.Add(testTab);

        if (config != null) {
            diagResultsTree = new TreeView {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                Scrollable = true,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f),
                FullRowSelect = true
            };

            // Enable double buffering for diagnostic tree
            typeof(TreeView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, diagResultsTree, new object[] { true });

            diagTab = new TabPage("Diagnostics") {
                Padding = new Padding(2),
            };
            diagTab.Controls.Add(diagResultsTree);
            tabControl.TabPages.Add(diagTab);

            // Initialize config label with trimmed path
            UpdateConfigLabel();
        } else {
            configLabel.Text = "No config — using folder picker";
        }

        // Add controls to main panel
        mainPanel.Controls.Add(treePanel);
        mainPanel.Controls.Add(tabControl);
        mainPanel.Controls.Add(progressBar);
        mainPanel.Controls.Add(statusLabel);
        mainPanel.Controls.Add(topPanel);

        Controls.Add(mainPanel);

        // Set form styles for Win32 HIG compliance
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        Font = new Font("Segoe UI", 9f);
    }

    private async void StartButton_Click(object? sender, EventArgs e) {
        if (running) return;
        running = true;
        startButton.Enabled = false;
        if (config != null) diagButton.Enabled = false;
        testResultsTree.Nodes.Clear();
        testTreeView.Nodes.Clear();
        progressBar.Value = 0;
        timingLabel.Text = "";
        UseWaitCursor = true;
        stopwatch = Stopwatch.StartNew();

        try {
            var (ovlPairs, sourceLabel) = await DiscoverOvlsAsync();
            if (config == null) UpdateConfigLabel();

            if (ovlPairs.Count == 0) {
                var rootNode = testResultsTree.Nodes.Add("Error: No OVL files found");
                rootNode.ForeColor = DColor.Red;
                return;
            }

            // Populate tree view with test bed structure
            foreach (var pair in ovlPairs) {
                var rootNode = new TreeNode(pair.Name) {
                    ToolTipText = $"Common: {pair.CommonPath}\nUnique: {pair.UniquePath}"
                };
                testTreeView.Nodes.Add(rootNode);

                // Add files as child nodes
                foreach (var file in pair.Files) {
                    var fileNode = new TreeNode(Path.GetFileName(file.Path)) {
                        ToolTipText = file.Path,
                        ForeColor = file.Type == OvlType.Common ? Color.DarkBlue : Color.DarkGreen
                    };
                    rootNode.Nodes.Add(fileNode);
                }
            }

            // Expand first node if there are any
            if (testTreeView.Nodes.Count > 0) {
                testTreeView.Nodes[0].Expand();
            }

            var totalOps = ovlPairs.Count * 3;
            var completedOps = 0;
            var pairTimings = new List<double>();

            await Task.Run(() => {
                foreach (var pair in ovlPairs) {
                    var pairStart = Stopwatch.GetTimestamp();
                    UpdateStatus($"Processing: {pair.Name} ({completedOps + 1}/{totalOps})");
                    var groupPassed = true;
                    var details = new List<(string name, bool passed, string error)>();

                    // Test 1: ReadLocalOvl (read both common and unique)
                    try {
                        var readOk = true;
                        var readErrors = new List<string>();
                        foreach (var file in pair.Files) {
                            try {
                                using var stream = SysFile.OpenRead(file.Path);
                                var ovl = OVL.Ovl.Read(stream, file.Path);
                                if (ovl.Type != file.Type) {
                                    readOk = false;
                                    readErrors.Add($"{Path.GetFileName(file.Path)}: expected {file.Type}, got {ovl.Type}");
                                }
                            } catch (Exception ex) {
                                readOk = false;
                                readErrors.Add($"{Path.GetFileName(file.Path)}: {ex.Message}");
                            }
                        }
                        details.Add(("ReadLocalOvl", readOk, string.Join("; ", readErrors)));
                        if (!readOk) groupPassed = false;
                    } catch (Exception ex) {
                        details.Add(("ReadLocalOvl", false, ex.Message));
                        groupPassed = false;
                    }
                    completedOps++;
                    UpdateProgressAndEta(completedOps, totalOps, stopwatch);

                    // Test 2: LocalOvlHasLoaderHeaders
                    try {
                        var headersOk = true;
                        var headerErrors = new List<string>();
                        foreach (var file in pair.Files) {
                            try {
                                using var stream = SysFile.OpenRead(file.Path);
                                var ovl = OVL.Ovl.Read(stream, file.Path);
                                if (ovl.CommonData?.LoaderHeaders.Length > 0 && ovl.LoaderHeaders.Length == 0) {
                                    headersOk = false;
                                    headerErrors.Add($"{Path.GetFileName(file.Path)}: expected headers but got none");
                                }
                            } catch (Exception ex) {
                                headersOk = false;
                                headerErrors.Add($"{Path.GetFileName(file.Path)}: {ex.Message}");
                            }
                        }
                        details.Add(("LocalOvlHasLoaderHeaders", headersOk, string.Join("; ", headerErrors)));
                        if (!headersOk) groupPassed = false;
                    } catch (Exception ex) {
                        details.Add(("LocalOvlHasLoaderHeaders", false, ex.Message));
                        groupPassed = false;
                    }
                    completedOps++;
                    UpdateProgressAndEta(completedOps, totalOps, stopwatch);

                    // Test 3: PairedArchiveHasLoaderEntries
                    try {
                        var entriesOk = true;
                        var entryErrors = new List<string>();
                        if (!string.IsNullOrEmpty(pair.CommonPath)) {
                            try {
                                var ovl = OVL.Ovl.Load(pair.CommonPath);
                                if (ovl.CommonData?.LoaderHeaders.Length > 0 && ovl.LoaderEntries.Count == 0) {
                                    entriesOk = false;
                                    entryErrors.Add("expected loader entries but got none");
                                }
                            } catch (Exception ex) {
                                entriesOk = false;
                                entryErrors.Add(ex.Message);
                            }
                        }
                        details.Add(("PairedArchiveHasLoaderEntries", entriesOk, string.Join("; ", entryErrors)));
                        if (!entriesOk) groupPassed = false;
                    } catch (Exception ex) {
                        details.Add(("PairedArchiveHasLoaderEntries", false, ex.Message));
                        groupPassed = false;
                    }
                    completedOps++;
                    UpdateProgressAndEta(completedOps, totalOps, stopwatch);

                    var pairElapsed = Stopwatch.GetElapsedTime(pairStart).TotalSeconds;
                    pairTimings.Add(pairElapsed);

                    // Write grouped result in tree view
                    var groupNode = testResultsTree.Nodes.Add($"{(groupPassed ? "PASS" : "FAIL")} {pair.Name}");
                    groupNode.ForeColor = groupPassed ? DColor.DarkGreen : DColor.DarkRed;

                    foreach (var (name, passed, error) in details) {
                        var detailNode = groupNode.Nodes.Add($"{(passed ? "  OK" : "  FAIL")} {name}");
                        detailNode.ForeColor = passed ? DColor.DarkGreen : DColor.DarkRed;
                        if (!string.IsNullOrEmpty(error)) {
                            var errorNode = detailNode.Nodes.Add($"    Error: {error}");
                            errorNode.ForeColor = DColor.Red;
                        }
                    }
                }

                var totalElapsed = stopwatch.Elapsed;
                UpdateStatus($"Done - {ovlPairs.Count} archive(s) processed in {FormatDuration(totalElapsed)}");
                timingLabel.Text = FormatDuration(totalElapsed);
            });
        } catch (Exception ex) {
            var rootNode = testResultsTree.Nodes.Add($"Error: {ex.Message}");
            rootNode.ForeColor = DColor.Red;
            statusLabel.Text = "Error - see results";
        } finally {
            running = false;
            startButton.Enabled = true;
            if (config != null) diagButton.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private async void DiagButton_Click(object? sender, EventArgs e) {
        if (running || config == null) return;
        running = true;
        startButton.Enabled = false;
        diagButton.Enabled = false;
        if (diagResultsTree != null) diagResultsTree.Nodes.Clear();
        progressBar.Value = 0;
        timingLabel.Text = "";
        UseWaitCursor = true;
        stopwatch = Stopwatch.StartNew();

        try {
            await Task.Run(() => {
                UpdateStatus("Scanning for Water OVLs...");

                // Find Water OVLs from config
                var waterCommon = "";
                var waterUnique = "";

                foreach (var (_, glob) in config.ExtraOvls!) {
                    var dir = Path.GetDirectoryName(glob);
                    if (string.IsNullOrEmpty(dir) || !SysDirectory.Exists(dir)) continue;
                    var pattern = "*" + Path.GetExtension(glob);
                    var matches = SysDirectory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                    foreach (var file in matches) {
                        var fileName = Path.GetFileName(file);
                        if (fileName.StartsWith("Water.", StringComparison.OrdinalIgnoreCase)) {
                            if (fileName.Contains(".common.")) waterCommon = file;
                            else if (fileName.Contains(".unique.")) waterUnique = file;
                        }
                    }
                }

                if (string.IsNullOrEmpty(waterCommon) || string.IsNullOrEmpty(waterUnique)) {
                    var rootNode = diagResultsTree.Nodes.Add("Water OVLs not found in config paths.");
                    rootNode.ForeColor = DColor.Red;
                    return;
                }

                UpdateStatus($"Diagnosing: {Path.GetFileName(waterCommon)}");

                try {
                    var ovl = OVL.Ovl.Load(waterCommon);

                    // Add diagnostic results to tree view
                    var rootNode = diagResultsTree.Nodes.Add("=== WATER COMMON OVL ===");
                    rootNode.ForeColor = DColor.DarkCyan;
                    rootNode.Nodes.Add($"Version: {ovl.CommonData!.Header.version}");
                    rootNode.Nodes.Add($"Loader entries: {ovl.LoaderEntries.Count}");
                    rootNode.Nodes.Add($"Strings: {ovl.Strings.Count}");
                    rootNode.Nodes.Add($"Symbols: {ovl.Symbols.Count}");

                    var commonEntries = ovl.LoaderEntries.Where(e => e.SourceFile.Contains(".common.")).ToList();
                    var uniqueEntries = ovl.LoaderEntries.Where(e => e.SourceFile.Contains(".unique.")).ToList();
                    rootNode.Nodes.Add($"Common entries: {commonEntries.Count}");
                    rootNode.Nodes.Add($"Unique entries: {uniqueEntries.Count}");

                    // Common entry names
                    var commonEntriesNode = rootNode.Nodes.Add("=== COMMON ENTRY NAMES (first 10) ===");
                    commonEntriesNode.ForeColor = DColor.DarkCyan;
                    foreach (var entry in commonEntries.Take(10)) {
                        commonEntriesNode.Nodes.Add($"  {entry.SymbolName}");
                    }

                    // Unique entry names
                    var uniqueEntriesNode = rootNode.Nodes.Add("=== UNIQUE ENTRY NAMES (first 10) ===");
                    uniqueEntriesNode.ForeColor = DColor.DarkCyan;
                    foreach (var entry in uniqueEntries.Take(10)) {
                        uniqueEntriesNode.Nodes.Add($"  {entry.SymbolName}");
                    }

                    // Check common file block 0
                    var commonBlock0 = ovl.CommonData.FileBlockData[0];
                    var commonBlockNode = rootNode.Nodes.Add("=== COMMON BLOCK 0 ===");
                    commonBlockNode.ForeColor = DColor.DarkCyan;
                    commonBlockNode.Nodes.Add($"Sub-blocks: {commonBlock0.Length}");
                    if (commonBlock0.Length > 0 && commonBlock0[0].Length > 0) {
                        var commonStrings = ReadRawStrings(commonBlock0[0]).ToList();
                        commonBlockNode.Nodes.Add($"Strings in common block 0: {commonStrings.Count}");
                        foreach (var s in commonStrings.Take(5)) {
                            commonBlockNode.Nodes.Add($"  {s}");
                        }
                    }

                    // Check unique file block 0
                    var uniqueBlock0 = ovl.UniqueData!.FileBlockData[0];
                    var uniqueBlockNode = rootNode.Nodes.Add("=== UNIQUE BLOCK 0 ===");
                    uniqueBlockNode.ForeColor = DColor.DarkCyan;
                    uniqueBlockNode.Nodes.Add($"Sub-blocks: {uniqueBlock0.Length}");
                    if (uniqueBlock0.Length > 0 && uniqueBlock0[0].Length > 0) {
                        var uniqueStrings = ReadRawStrings(uniqueBlock0[0]).ToList();
                        uniqueBlockNode.Nodes.Add($"Strings in unique block 0: {uniqueStrings.Count}");
                        foreach (var s in uniqueStrings.Take(5)) {
                            uniqueBlockNode.Nodes.Add($"  {s}");
                        }
                    } else {
                        uniqueBlockNode.Nodes.Add("  (empty)");
                    }

                    // Check loader headers
                    var headersNode = rootNode.Nodes.Add("=== LOADER HEADERS ===");
                    headersNode.ForeColor = DColor.DarkCyan;
                    foreach (var h in ovl.LoaderHeaders) {
                        headersNode.Nodes.Add($"  type={h.type} tag={h.tag} name={h.name} symbolCount={h.symbolCount}");
                    }

                    // Summary
                    var summaryNode = rootNode.Nodes.Add("=== SUMMARY ===");
                    summaryNode.ForeColor = DColor.DarkCyan;
                    var commonNamed = commonEntries.Count(e => e.SymbolName != "No Symbol");
                    var uniqueNamed = uniqueEntries.Count(e => e.SymbolName != "No Symbol");
                    summaryNode.Nodes.Add($"Common: {commonNamed}/{commonEntries.Count} named");
                    summaryNode.Nodes.Add($"Unique: {uniqueNamed}/{uniqueEntries.Count} named");

                    // Symbols
                    var symbolsNode = rootNode.Nodes.Add("=== SYMBOL NAMES (first 20) ===");
                    symbolsNode.ForeColor = DColor.DarkCyan;
                    foreach (var sym in ovl.Symbols.Take(20)) {
                        symbolsNode.Nodes.Add($"  {sym.Name} -> 0x{sym.DataAddress:X}");
                    }

                    // Loader entries data addresses
                    var entriesNode = rootNode.Nodes.Add("=== LOADER ENTRIES DATA ADDRESSES (first 10) ===");
                    entriesNode.ForeColor = DColor.DarkCyan;
                    foreach (var entry in ovl.LoaderEntries.Take(10)) {
                        entriesNode.Nodes.Add($"  type={entry.LoaderType} data=0x{entry.DataAddress:X} symbol={entry.SymbolName}");
                    }

                    var totalElapsed = stopwatch.Elapsed;
                    UpdateStatus($"Diagnostics complete in {FormatDuration(totalElapsed)}");
                    timingLabel.Text = FormatDuration(totalElapsed);
                } catch (Exception ex) {
                    var rootNode = diagResultsTree.Nodes.Add($"Error: {ex.Message}");
                    rootNode.ForeColor = DColor.Red;
                    UpdateStatus("Diagnostics failed");
                }
            });
        } catch (Exception ex) {
            var rootNode = diagResultsTree.Nodes.Add($"Error: {ex.Message}");
            rootNode.ForeColor = DColor.Red;
            statusLabel.Text = "Error - see results";
        } finally {
            running = false;
            startButton.Enabled = true;
            diagButton.Enabled = true;
            progressBar.Value = 0;
            UseWaitCursor = false;
        }
    }

    private static IEnumerable<string> ReadRawStrings(byte[] data) {
        var result = new List<string>();
        var pos = 0;
        while (pos < data.Length) {
            var end = Array.IndexOf(data, (byte)0, pos);
            if (end < 0) break;
            if (end > pos) {
                result.Add(System.Text.Encoding.ASCII.GetString(data, pos, end - pos));
            }
            pos = end + 1;
        }
        return result;
    }

    private void UpdateStatus(string text) {
        if (InvokeRequired) {
            Invoke(() => statusLabel.Text = text);
        } else {
            statusLabel.Text = text;
        }
    }

    private void UpdateProgressAndEta(int completed, int total, Stopwatch stopwatch) {
        if (InvokeRequired) {
            Invoke(() => UpdateProgressAndEta(completed, total, stopwatch));
            return;
        }
        progressBar.Value = (int)((double)completed / total * 100);
        var remaining = total - completed;
        if (remaining > 0 && completed > 0) {
            var elapsed = stopwatch.Elapsed.TotalSeconds;
            var avgPerOp = elapsed / completed;
            var etaSeconds = avgPerOp * remaining;
            statusLabel.Text = $"{statusLabel.Text} — ETA: {FormatDuration(TimeSpan.FromSeconds(etaSeconds))}";
        }
        Application.DoEvents();
    }

    private static string FormatDuration(TimeSpan duration) {
        if (duration.TotalSeconds < 1) return $"{duration.TotalMilliseconds:F0}ms";
        if (duration.TotalMinutes < 1) return $"{duration.TotalSeconds:F1}s";
        if (duration.TotalHours < 1) return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        return $"{(int)duration.TotalHours}h {(int)duration.Minutes}m";
    }

    private async Task<(List<OvlPair> pairs, string configSource)> DiscoverOvlsAsync() {
        if (config?.ExtraOvls != null) {
            UpdateStatus("Scanning for OVL files...");
            return await Task.Run(() => {
                var pairs = new List<OvlPair>();
                var commonFiles = new List<(string path, string name)>();
                var uniqueFiles = new List<(string path, string name)>();

                foreach (var (_, glob) in config.ExtraOvls) {
                    var dir = Path.GetDirectoryName(glob);
                    if (string.IsNullOrEmpty(dir) || !SysDirectory.Exists(dir)) continue;
                    var pattern = "*" + Path.GetExtension(glob);
                    var matches = SysDirectory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                    foreach (var file in matches) {
                        var fileName = Path.GetFileName(file);
                        if (fileName.Contains(".common.")) commonFiles.Add((file, fileName));
                        else if (fileName.Contains(".unique.")) uniqueFiles.Add((file, fileName));
                    }
                }

                foreach (var common in commonFiles) {
                    var prefix = common.name.Split('.')[0];
                    var matchingUnique = uniqueFiles.FirstOrDefault(u => u.name.StartsWith(prefix));
                    if (!string.IsNullOrEmpty(matchingUnique.path)) {
                        pairs.Add(new OvlPair {
                            Name = prefix,
                            CommonPath = common.path,
                            UniquePath = matchingUnique.path,
                            Files = new List<OvlFile> {
                                new() { Path = common.path, Type = OvlType.Common },
                                new() { Path = matchingUnique.path, Type = OvlType.Unique },
                            },
                        });
                    }
                }

                return (pairs, $"Config: {configPath} ({pairs.Count} pairs)");
            });
        }

        // Fall back to folder picker
        using var fbd = new FolderBrowserDialog {
            Description = "Select your RCT3 Assets folder (e.g. Contents\\Assets)",
            UseDescriptionForTitle = true,
        };
        if (fbd.ShowDialog() != DialogResult.OK) {
            return ([], "Cancelled");
        }

        UpdateStatus("Scanning for OVL files...");
        var assetsDir = fbd.SelectedPath;
        return await Task.Run(() => {
            var pairsFromPicker = new List<OvlPair>();
            var commonFromPicker = new List<(string path, string name)>();
            var uniqueFromPicker = new List<(string path, string name)>();

            foreach (var file in SysDirectory.GetFiles(assetsDir, "*.ovl", SearchOption.AllDirectories)) {
                var fileName = Path.GetFileName(file);
                if (fileName.Contains(".common.")) commonFromPicker.Add((file, fileName));
                else if (fileName.Contains(".unique.")) uniqueFromPicker.Add((file, fileName));
            }

            foreach (var common in commonFromPicker) {
                var prefix = common.name.Split('.')[0];
                var matchingUnique = uniqueFromPicker.FirstOrDefault(u => u.name.StartsWith(prefix));
                if (!string.IsNullOrEmpty(matchingUnique.path)) {
                    pairsFromPicker.Add(new OvlPair {
                        Name = prefix,
                        CommonPath = common.path,
                        UniquePath = matchingUnique.path,
                        Files = new List<OvlFile> {
                            new() { Path = common.path, Type = OvlType.Common },
                            new() { Path = matchingUnique.path, Type = OvlType.Unique },
                        },
                    });
                }
            }

            return (pairsFromPicker, $"Picker: {assetsDir} ({pairsFromPicker.Count} pairs)");
        });
    }

    private void UpdateConfigLabel() {
        // Use GDI measurement to trim long paths
        using var g = CreateGraphics();
        var font = configLabel.Font;
        var maxWidth = configLabel.Width;

        // Get full path text
        var fullPath = $"Config: {configPath}";

        // Check if text fits
        var textSize = g.MeasureString(fullPath, font);
        if (textSize.Width <= maxWidth) {
            configLabel.Text = fullPath;
            return;
        }

        // Trim path using ellipsis
        var path = configPath;
        var prefix = "Config: ";

        // Try to keep as much of the path as possible
        var remainingWidth = maxWidth - g.MeasureString(prefix, font).Width;

        // Try with just the filename
        var filename = Path.GetFileName(path);
        if (g.MeasureString(prefix + filename, font).Width <= maxWidth) {
            configLabel.Text = prefix + filename;
            return;
        }

        // Use ellipsis in middle of path
        var pathParts = path.Split(Path.DirectorySeparatorChar);
        var leftPart = "";
        var rightPart = "";

        // Start with first and last parts
        leftPart = pathParts[0];
        if (pathParts.Length > 1) {
            rightPart = pathParts[pathParts.Length - 1];
        }

        // Try to add more parts from left
        for (int i = 1; i < pathParts.Length - 1; i++) {
            var testPath = leftPart + Path.DirectorySeparatorChar + pathParts[i] + "..." + Path.DirectorySeparatorChar + rightPart;
            if (g.MeasureString(prefix + testPath, font).Width <= maxWidth) {
                leftPart += Path.DirectorySeparatorChar + pathParts[i];
            } else {
                break;
            }
        }

        // If still too long, use ellipsis
        var finalPath = leftPart + "..." + Path.DirectorySeparatorChar + rightPart;
        configLabel.Text = prefix + finalPath;
    }
}

internal class OvlPair {
    public string Name = "";
    public string CommonPath = "";
    public string UniquePath = "";
    public List<OvlFile> Files = new();
}

internal class OvlFile {
    public string Path = "";
    public OvlType Type;
}

internal class ExtraOvlConfig {
    [System.Text.Json.Serialization.JsonPropertyName("extraOvls")]
    public Dictionary<string, string>? ExtraOvls { get; set; }
}

internal static class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new OvlTestBenchForm());
    }
}

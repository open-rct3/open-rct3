using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OvlTestBench.Tests;
using SysFile = System.IO.File;
using SysDirectory = System.IO.Directory;
using OvlType = OVL.OvlType;
using DColor = System.Drawing.Color;

namespace OvlTestBench;

public partial class OvlTestBenchForm : Form {
  private const string CONFIG_LABEL_PREFIX = "Config:";
  private bool running;
  private readonly string? configPath;
  private readonly ExtraOvlConfig? config;
  private Stopwatch? stopwatch;
  private CancellationTokenSource? cancellationTokenSource;

  public OvlTestBenchForm() {
    InitializeComponent();
    InitializeComponentIcons();

    // Initialize config path
    configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ovl-tests.local.json");
    if (SysFile.Exists(configPath)) {
      var json = SysFile.ReadAllText(configPath);
      config = JsonSerializer.Deserialize<ExtraOvlConfig>(json);
    }

    // Show/hide diagnostics button based on config
    if (config != null) {
      diagButton.Visible = true;
      UpdateConfigLabel();
    } else {
      diagButton.Visible = false;
      configLabel.Text = $"{CONFIG_LABEL_PREFIX} Using folder picker";
    }
  }

  private async void StartStopButton_Click(object? sender, EventArgs e) {
    if (running) {
      // Stop the currently running task
      cancellationTokenSource?.Cancel();
      startStopButton.Text = "Start";
      progressBar.Value = 0;
      progressBar.Style = ProgressBarStyle.Continuous;
      statusLabel.Text = "Ready";
      UpdateTiming("ETA: About 5 minutes");
      UseWaitCursor = false;
      return;
    }

    running = true;
    startStopButton.Text = "Stop";
    if (config != null) diagButton.Enabled = false;
    resultsTree.Nodes.Clear();
    progressBar.Value = 0;
    UpdateTiming("ETA: Calculating...");
    UseWaitCursor = true;
    stopwatch = Stopwatch.StartNew();
    cancellationTokenSource = new CancellationTokenSource();

    try {
      var (ovlPairs, sourceLabel) = await DiscoverOvlsAsync();
      if (config == null) UpdateConfigLabel();

      if (ovlPairs.Count == 0) {
        var rootNode = resultsTree.Nodes.Add("Error: No OVL files found");
        rootNode.ForeColor = DColor.Red;
        return;
      }

      var totalOps = ovlPairs.Count * LoadOvls.All.Length;
      var completedOps = 0;
      var ct = cancellationTokenSource.Token;

      await Task.Run(() => {
        foreach (var pair in ovlPairs) {
          if (ct.IsCancellationRequested) break;

          UpdateStatus($"Processing: {pair.Name} ({completedOps + 1}/{totalOps})");
          var groupPassed = true;
          var details = new List<(string name, bool passed, string error)>();

          foreach (var test in LoadOvls.All) {
            try {
              test.Test(pair);
            } catch (Exception ex) {
              Assert.AddError(ex.Message);
            }
            var result = Assert.Result(test.Name);
            details.Add((result.Name, result.Passed, result.Error));
            if (!result.Passed) groupPassed = false;
            completedOps++;
            UpdateProgressAndEta(completedOps, totalOps, stopwatch);
          }

          // Write grouped result in tree view on UI thread
          Invoke(() => {
            var groupNode = resultsTree.Nodes.Add($"{(groupPassed ? "PASS" : "FAIL")} {pair.Name}");
            groupNode.ForeColor = groupPassed ? DColor.DarkGreen : DColor.DarkRed;

            foreach (var (name, passed, error) in details) {
              var detailNode = groupNode.Nodes.Add($"{(passed ? "  OK" : "  FAIL")} {name}");
              detailNode.ForeColor = passed ? DColor.DarkGreen : DColor.DarkRed;
              if (!string.IsNullOrEmpty(error)) {
                var errorNode = detailNode.Nodes.Add($"    Error: {error}");
                errorNode.ForeColor = DColor.Red;
              }
            }
          });
        }

        if (!ct.IsCancellationRequested) {
          var totalElapsed = stopwatch.Elapsed;
          UpdateStatus($"Done: {ovlPairs.Count} archives examined");
          UpdateTiming(FormatDuration(totalElapsed));
        } else UpdateStatus("Cancelled");
      }, ct);
    } catch (OperationCanceledException) {
      // Task was cancelled
    } catch (Exception ex) {
      var rootNode = resultsTree.Nodes.Add($"Error: {ex.Message}");
      rootNode.ForeColor = DColor.Red;
      statusLabel.Text = "Error: See Results";
    } finally {
      running = false;
      startStopButton.Text = "Start";
      if (config != null) diagButton.Enabled = true;
      UseWaitCursor = false;
      cancellationTokenSource?.Dispose();
      cancellationTokenSource = null;
    }
  }

  private async void GatherDiagnosticsButton_Click(object? sender, EventArgs e) {
    if (running || config == null) return;

    running = true;
    startStopButton.Text = "Stop";
    diagButton.Enabled = false;
    resultsTree.Nodes.Clear();
    progressBar.Value = 0;
    timingLabel.Text = "ETA: Less than 30 seconds";
    UseWaitCursor = true;
    stopwatch = Stopwatch.StartNew();
    cancellationTokenSource = new CancellationTokenSource();
    var ct = cancellationTokenSource.Token;

    try {
      await Task.Run(() => {
        UpdateStatus("Loading Water OVLs...");
        Invoke(() => progressBar.Style = ProgressBarStyle.Marquee);

        // Find OVLs from config
        var commonOvl = "";
        var uniqueOvl = "";

        foreach (var (_, glob) in config.ExtraOvls!) {
          var dir = Path.GetDirectoryName(glob);
          if (string.IsNullOrEmpty(dir) || !SysDirectory.Exists(dir)) continue;
          var pattern = "*" + Path.GetExtension(glob);
          var matches = SysDirectory.GetFiles(dir, pattern, SearchOption.AllDirectories);
          foreach (var file in matches) {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("Water.", StringComparison.OrdinalIgnoreCase)) {
              if (fileName.Contains(".common.")) commonOvl = file;
              else if (fileName.Contains(".unique.")) uniqueOvl = file;
            }
          }
        }

        if (string.IsNullOrEmpty(commonOvl) || string.IsNullOrEmpty(uniqueOvl)) {
          Invoke(() => {
            var rootNode = resultsTree.Nodes.Add("OVLs not found in config paths.");
            rootNode.ForeColor = DColor.Red;
          });
          return;
        }

        Invoke(() => progressBar.Style = ProgressBarStyle.Continuous);

        try {
          var ovl = OVL.Ovl.Load(commonOvl);

          // Add diagnostic results to tree view on UI thread
          Invoke(() => {
            var rootNode = resultsTree.Nodes.Add("=== WATER COMMON OVL ===");
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
            UpdateStatus("Ready");
            timingLabel.Text = FormatDuration(totalElapsed);
          });
        } catch (Exception ex) {
          Invoke(() => {
            var rootNode = resultsTree.Nodes.Add($"Error: {ex.Message}");
            rootNode.ForeColor = DColor.Red;
            UpdateStatus("Error: See results");
          });
        }
      });
    } catch (Exception ex) {
      var rootNode = resultsTree.Nodes.Add($"Error: {ex.Message}");
      rootNode.ForeColor = DColor.Red;
      statusLabel.Text = "Error - see results";
    } finally {
      running = false;
      startStopButton.Enabled = true;
      diagButton.Enabled = true;
      progressBar.Value = 0;
      UseWaitCursor = false;
    }
  }

  private async void TestPluginsButton_Click(object? sender, EventArgs e) {
    if (running) return;

    running = true;
    testPluginsButton.Enabled = false;
    resultsTree.Nodes.Clear();
    progressBar.Value = 0;
    progressBar.Style = ProgressBarStyle.Continuous;
    UseWaitCursor = true;
    stopwatch = Stopwatch.StartNew();

    var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
    var wasmFiles = SysDirectory.Exists(pluginsDir)
        ? SysDirectory.GetFiles(pluginsDir, "*.wasm")
        : Array.Empty<string>();

    try {
      await Task.Run(() => {
        var totalOps = wasmFiles.Length * PluginTests.All.Length;
        var completedOps = 0;

        foreach (var wasmPath in wasmFiles) {
          var pluginName = Path.GetFileNameWithoutExtension(wasmPath);
          var details = new List<(string name, bool passed, string error)>();

          foreach (var test in PluginTests.All) {
            try {
              test.Test(wasmPath);
            } catch (Exception ex) {
              Assert.AddError(ex.Message);
            }
            var result = Assert.Result(test.Name);
            details.Add((result.Name, result.Passed, result.Error));
            completedOps++;
            UpdateProgressAndEta(completedOps, totalOps, stopwatch);
          }

          bool groupPassed = details.All(d => d.passed);
          Invoke(() => {
            var groupNode = resultsTree.Nodes.Add($"{(groupPassed ? "PASS" : "FAIL")} {pluginName}");
            groupNode.ForeColor = groupPassed ? DColor.DarkGreen : DColor.DarkRed;

            foreach (var (name, passed, error) in details) {
              var detailNode = groupNode.Nodes.Add($"{(passed ? "  OK" : "  FAIL")} {name}");
              detailNode.ForeColor = passed ? DColor.DarkGreen : DColor.DarkRed;
              if (!string.IsNullOrEmpty(error)) {
                var errorNode = detailNode.Nodes.Add($"    Error: {error}");
                errorNode.ForeColor = DColor.Red;
              }
            }
          });
        }

        var totalElapsed = stopwatch.Elapsed;
        UpdateStatus(wasmFiles.Length > 0
            ? $"Tested {wasmFiles.Length} plugin(s)"
            : "No plugins found");
        UpdateTiming(FormatDuration(totalElapsed));
      });
    } catch (Exception ex) {
      var rootNode = resultsTree.Nodes.Add($"Error: {ex.Message}");
      rootNode.ForeColor = DColor.Red;
      UpdateStatus("Error: See Results");
    } finally {
      running = false;
      testPluginsButton.Enabled = true;
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
    if (InvokeRequired) Invoke(() => statusLabel.Text = text);
    else statusLabel.Text = text;
  }

  private void UpdateTiming(string text) {
    if (InvokeRequired) Invoke(() => timingLabel.Text = text);
    else timingLabel.Text = text;
  }

  private void UpdateProgressAndEta(int completed, int total, Stopwatch stopwatch) {
    if (InvokeRequired) {
      Invoke(() => UpdateProgressAndEta(completed, total, stopwatch));
      return;
    }
    progressBar.Value = (int)((double)completed / total * 100);

    // Update status with completion percentage
    statusLabel.Text = $"Processing: {completed}/{total} operations";

    // Calculate and show ETA
    var remaining = total - completed;
    if (remaining > 0 && completed > 0) {
      var elapsed = stopwatch.Elapsed.TotalSeconds;
      var avgPerOp = elapsed / completed;
      var etaSeconds = avgPerOp * remaining;
      var elapsedStr = FormatDuration(stopwatch.Elapsed);
      var etaStr = FormatDuration(TimeSpan.FromSeconds(etaSeconds));
      timingLabel.Text = $"{elapsedStr} elapsed — ETA: {etaStr}";
    } else {
      timingLabel.Text = FormatDuration(stopwatch.Elapsed) + " elapsed";
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
      progressBar.Style = ProgressBarStyle.Marquee;
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

        Invoke(() => progressBar.Style = ProgressBarStyle.Continuous);
        return (pairs, $"Config: {configPath} ({pairs.Count} pairs)");
      });
    }

    // Fall back and let the user find their RCT3 installation
    using var openFolderDialog = new FolderBrowserDialog {
      Description = "Select the root of your RCT3 installation",
      UseDescriptionForTitle = true,
    };
    if (openFolderDialog.ShowDialog() != DialogResult.OK) return ([], "Cancelled");

    UpdateStatus("Scanning for OVL files...");
    var assetsDir = openFolderDialog.SelectedPath;
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

      Invoke(() => progressBar.Style = ProgressBarStyle.Continuous);
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

    // Try to keep as much of the path as possible
    var remainingWidth = maxWidth - g.MeasureString(CONFIG_LABEL_PREFIX, font).Width;

    // Try with just the filename
    var filename = Path.GetFileName(path);
    var newConfigLabelValue = $"{CONFIG_LABEL_PREFIX} {filename}";
    if (g.MeasureString(newConfigLabelValue, font).Width <= maxWidth) {
      configLabel.Text = newConfigLabelValue;
      return;
    }

    // Use ellipsis in middle of path
    var pathParts = path?.Split(Path.DirectorySeparatorChar) ?? [];
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
      if (g.MeasureString($"{CONFIG_LABEL_PREFIX} {testPath}", font).Width <= maxWidth) {
        leftPart += Path.DirectorySeparatorChar + pathParts[i];
      } else {
        break;
      }
    }

    // If still too long, use ellipsis
    var finalPath = leftPart + "..." + Path.DirectorySeparatorChar + rightPart;
    configLabel.Text = $"{CONFIG_LABEL_PREFIX} {finalPath}";
  }

  private void OvlTestBenchForm_Resize(object sender, EventArgs e) {
    UpdateConfigLabel();
  }
}

internal class ExtraOvlConfig {
    [System.Text.Json.Serialization.JsonPropertyName("extraOvls")]
    public Dictionary<string, string>? ExtraOvls { get; set; }
}

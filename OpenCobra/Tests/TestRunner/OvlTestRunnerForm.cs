using System;
using System.Collections.Generic;
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

namespace OvlTestRunner;

public partial class OvlTestRunnerForm : Form {
  private readonly RichTextBox resultsBox;
  private readonly ProgressBar progressBar;
  private readonly Label statusLabel;
  private readonly Label configLabel;
  private readonly Button startButton;
  private bool running;

  public OvlTestRunnerForm() {
    Text = "OVL Test Runner";
    Size = new System.Drawing.Size(800, 600);
    MinimumSize = new System.Drawing.Size(600, 400);
    StartPosition = FormStartPosition.CenterScreen;

    var topPanel = new FlowLayoutPanel {
      Dock = DockStyle.Top,
      Height = 60,
      WrapContents = false,
      AutoSize = false,
    };

    startButton = new Button { Text = "Start", Width = 80, Height = 30 };
    startButton.Click += StartButton_Click;
    topPanel.Controls.Add(startButton);

    configLabel = new Label {
      AutoSize = false,
      Width = 700,
      Height = 30,
      TextAlign = ContentAlignment.MiddleLeft,
    };
    topPanel.Controls.Add(configLabel);

    statusLabel = new Label {
      Dock = DockStyle.Top,
      Height = 20,
      TextAlign = ContentAlignment.MiddleLeft,
      Text = "Ready",
      Padding = new Padding(4, 0, 0, 0),
    };

    progressBar = new ProgressBar {
      Dock = DockStyle.Top,
      Height = 20,
      Style = ProgressBarStyle.Continuous,
    };

    resultsBox = new RichTextBox {
      Dock = DockStyle.Fill,
      ReadOnly = true,
      Font = new System.Drawing.Font("Consolas", 9f),
      BackColor = System.Drawing.SystemColors.Window,
    };

    Controls.Add(resultsBox);
    Controls.Add(statusLabel);
    Controls.Add(progressBar);
    Controls.Add(topPanel);
  }

  private async void StartButton_Click(object? sender, EventArgs e) {
    if (running) return;
    running = true;
    startButton.Enabled = false;
    resultsBox.Clear();
    progressBar.Value = 0;

    try {
      var (ovlPairs, configSource) = DiscoverOvls();
      configLabel.Text = configSource;

      if (ovlPairs.Count == 0) {
        AppendResult("No OVL files found. Check config or RCT3 installation path.", DColor.Red);
        return;
      }

      var totalOps = ovlPairs.Count * 3;
      var completedOps = 0;

      await Task.Run(() => {
        foreach (var pair in ovlPairs) {
          UpdateStatus($"Processing: {pair.Name}");
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
          UpdateProgress((int)((double)completedOps / totalOps * 100));

          // Test 2: LocalOvlHasLoaderHeaders
          try {
            var headersOk = true;
            var headerErrors = new List<string>();
            foreach (var file in pair.Files) {
              try {
                using var stream = SysFile.OpenRead(file.Path);
                var ovl = OVL.Ovl.Read(stream, file.Path);
                if (ovl.LoaderHeaders.Length > 0 && ovl.LoaderHeaders.Length == 0) {
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
          UpdateProgress((int)((double)completedOps / totalOps * 100));

          // Test 3: PairedArchiveHasLoaderEntries
          try {
            var entriesOk = true;
            var entryErrors = new List<string>();
            if (!string.IsNullOrEmpty(pair.CommonPath)) {
              try {
                var ovl = OVL.Ovl.Load(pair.CommonPath);
                if (ovl.LoaderHeaders.Length > 0 && ovl.LoaderEntries.Count == 0) {
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
          UpdateProgress((int)((double)completedOps / totalOps * 100));

          // Write grouped result
          var color = groupPassed ? DColor.DarkGreen : DColor.DarkRed;
          var icon = groupPassed ? "PASS" : "FAIL";
          AppendResult($"{icon} {pair.Name}", color);
          foreach (var (name, passed, error) in details) {
            var lineColor = passed ? DColor.DarkGreen : DColor.DarkRed;
            var lineIcon = passed ? "  OK" : "  FAIL";
            var line = $"{lineIcon} {name}";
            if (!string.IsNullOrEmpty(error)) line += $" - {error}";
            AppendResult(line, lineColor);
          }
          AppendResult("", DColor.Black);
        }

        UpdateStatus($"Done - {ovlPairs.Count} archive(s) processed");
      });
    } catch (Exception ex) {
      AppendResult($"Error: {ex.Message}", DColor.Red);
      statusLabel.Text = "Error - see results";
    } finally {
      running = false;
      startButton.Enabled = true;
    }
  }

  private void UpdateStatus(string text) {
    if (InvokeRequired) {
      Invoke(() => statusLabel.Text = text);
    } else {
      statusLabel.Text = text;
    }
  }

  private void UpdateProgress(int value) {
    if (InvokeRequired) {
      Invoke(() => progressBar.Value = value);
    } else {
      progressBar.Value = value;
    }
    Application.DoEvents();
  }

  private void AppendResult(string text, Color color) {
    if (InvokeRequired) {
      Invoke(() => AppendResult(text, color));
      return;
    }
    var start = resultsBox.TextLength;
    resultsBox.AppendText(text + Environment.NewLine);
    resultsBox.Select(start, resultsBox.TextLength - start);
    resultsBox.SelectionColor = color;
    resultsBox.SelectionStart = resultsBox.TextLength;
    resultsBox.ScrollToCaret();
  }

  private (List<OvlPair> pairs, string configSource) DiscoverOvls() {
    // Try config file first
    var testBinDir = AppDomain.CurrentDomain.BaseDirectory;
    var configPath = Path.Combine(testBinDir, "ovl-tests.local.json");
    if (!SysFile.Exists(configPath)) {
      configPath = Path.Combine(testBinDir, "..", "..", "..", "..", "OVL Tests", "ovl-tests.local.json");
    }

    if (SysFile.Exists(configPath)) {
      var json = SysFile.ReadAllText(configPath);
      var config = JsonSerializer.Deserialize<ExtraOvlConfig>(json);
      if (config?.ExtraOvls != null) {
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
      }
    }

    // Fall back to folder picker
    using var fbd = new FolderBrowserDialog {
      Description = "Select your RCT3 Assets folder (e.g. Contents\\Assets)",
      UseDescriptionForTitle = true,
    };
    if (fbd.ShowDialog() != DialogResult.OK) {
      return ([], "Cancelled");
    }

    var assetsDir = fbd.SelectedPath;
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
    Application.Run(new OvlTestRunnerForm());
  }
}

using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Dumper.Plugins;

/// <summary>
/// A custom Label control that intelligently truncates long text in the middle,
/// preserving the beginning and end portions. Ideal for file paths and URLs.
///
/// When text doesn't fit, it displays: "C:\OpenRCT3\…\ftx-viewer.wasm"
/// </summary>
public class TruncatedLabel : Label {
  private const string ELLIPSIS = "…";

  private string originalText = string.Empty;
  private readonly ToolTip toolTip = new() { AutomaticDelay = 500 };
  private float preserveRatio = 0.3f;
  private string ellipsis = ELLIPSIS;
  private bool smartBreak = true;

  public TruncatedLabel() {
    // Handle resize events to re-evaluate truncation
    this.Resize += (s, e) => UpdateDisplayText();
    this.SizeChanged += (s, e) => UpdateDisplayText();
    this.FontChanged += (s, e) => UpdateDisplayText();
  }

  /// <summary>
  /// Gets or sets the text displayed by the label.
  /// </summary>
  /// <remarks>
  /// The base Text property is hidden to manage the truncation logic.
  /// </remarks>
  [Category("Appearance")]
  public new string Text {
    get => originalText;
    set {
      originalText = value ?? string.Empty;
      UpdateDisplayText();
    }
  }

  /// <summary>
  /// <para>
  /// Controls how much of the beginning and end portions are preserved, up to a maximum of 50%.
  /// </para>
  /// <para>
  /// Default: 30% from start, 30% from end. Minimum is 10%.
  /// Higher values will make more of the original text is visible.
  /// </para>
  /// </summary>
  [Category("Appearance")]
  [Description("Controls how much of the beginning and end portions are preserved, up to a maximum of 50%.")]
  public float PreserveRatio {
    get => preserveRatio;
    set {
      preserveRatio = Math.Max(0.1f, Math.Min(value, 0.5f));
      UpdateDisplayText();
    }
  }

  /// <summary>
  /// Custom ellipsis. Default is "…" but can be customized (e.g., "...")
  /// </summary>
  [Category("Appearance")]
  [Description("Custom ellipsis, e.g. \"…\".")]
  public string Ellipsis {
    get => ellipsis;
    set {
      ellipsis = value ?? ELLIPSIS;
      UpdateDisplayText();
    }
  }

  /// <summary>
  /// Whether to preserve path/filename structure by breaking at separators.
  /// </summary>
  /// <remarks>
  /// For file paths, this respects backslash/forward slash boundaries.
  /// </remarks>
  [Category("Appearance")]
  [Description("Whether to preserve path/filename structure by breaking at separators.")]
  public bool SmartBreak {
    get => smartBreak;
    set {
      smartBreak = value;
      UpdateDisplayText();
    }
  }

  /// <summary>
  /// Recalculates the display text based on available space.
  /// </summary>
  private void UpdateDisplayText() {
    var textFits = TextFits(originalText);
    base.Text = !textFits ? TruncateMiddle(originalText) : originalText;
    // Set tooltip for accessibility
    toolTip.SetToolTip(this, textFits ? null : originalText);
  }

  /// <summary>
  /// Checks if the text fits within the label's current width.
  /// </summary>
  private bool TextFits(string text) {
    if (string.IsNullOrWhiteSpace(text) || Width <= 0) return true;

    using var g = this.CreateGraphics();
    SizeF size = g.MeasureString(text, Font);
    // Use Width directly, subtract padding
    int availableWidth = ClientSize.Width - (Padding.Left + Padding.Right);
    return size.Width <= availableWidth;
  }

  /// <summary>
  /// Truncates the middle of the text, preserving the beginning and end.
  /// </summary>
  /// <remarks>
  /// Attempts to break at path separators when SmartBreak is enabled.
  /// </remarks>
  private string TruncateMiddle(string text) {
    if (text.Length <= ellipsis.Length) return text;

    // Try to break the string inbetween path separators
    if (smartBreak && text.Contains(Path.DirectorySeparatorChar)) {
      int lastSeparatorStart = text.LastIndexOf(Path.DirectorySeparatorChar);
      string startPart = text[..lastSeparatorStart];
      if (startPart.Contains(Path.DirectorySeparatorChar))
        startPart = startPart[..(startPart.LastIndexOf(Path.DirectorySeparatorChar))];

      return FindFittingTruncation(startPart, text[lastSeparatorStart..]);
    }

    // Calculate how many characters we can keep from start and end
    int preserveLength = Math.Max(1, (int)(text.Length * preserveRatio));

    // Adjust if the preserved portions + ellipsis exceed the original length
    if ((preserveLength * 2) + ellipsis.Length >= text.Length)
      preserveLength = Math.Max(1, (text.Length - ellipsis.Length) / 2);

    return FindFittingTruncation(text[..preserveLength], text[^preserveLength..]);
  }

  /// <summary>
  /// Uses binary search to find the longest possible start/end combination that fits.
  /// </summary>
  private string FindFittingTruncation(string startPart, string endPart) {
    var startLen = startPart.Length;
    var endLen = endPart.Length;
    var start = startPart[..startLen];
    var end = endPart[^endLen..];

    // Try progressively shorter combinations until it fits
    // FIXME: I think this will truncate paths weirdly if `start` doesn't fit as-is
    // When `smartBreak` is true:
    // - The first part in `start` shall not be "...", e.g. "C:\Users\...\foo" is acceptable, "...\foo" is not
    // - The last part in `start` shall be a full file name, e.g. "/usr/bin/.../foo"
    while (!TextFits($"{start}{ellipsis}{end}")) {
      // Reduce the longer part
      if (startLen > 0 && endLen > 0) {
        if (startLen >= endLen) startLen--;
        else endLen--;
      }
      else if (startLen > 0) startLen--;
      else if (endLen > 0) endLen--;
      // Can't fit anything meaningful
      else return smartBreak ? $"{ellipsis}{endPart}" : ellipsis;
    }

    start = startPart[..startLen] + (smartBreak ? Path.DirectorySeparatorChar : string.Empty);
    end = endPart[^endLen..];
    return $"{start}{ellipsis}{end}";
  }
}

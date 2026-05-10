using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Dumper.Plugins;

/// <summary>
/// A custom Label control that intelligently truncates long text in the middle,
/// preserving the beginning and end portions. Ideal for file paths and URLs.
///
/// When text doesn't fit, it displays: "C:\OpenRCT3\...\ftx-viewer.wasm"
/// </summary>
public class TruncatedLabel : Label {
  private const string ELLIPSIS = "...";

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
  /// Custom ellipsis. Default is "..." but can be customized (e.g., "…")
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
    if (string.IsNullOrEmpty(originalText)) {
      base.Text = originalText;
      return;
    }

    // Use base.Text to bypass our custom Text property
    string displayText = originalText;

    // Check if text fits in the current bounds
    if (!TextFits(originalText))
      displayText = TruncateMiddle(originalText);

    base.Text = displayText;
    // For accessibility
    toolTip.SetToolTip(this, originalText);
  }

  /// <summary>
  /// Checks if the text fits within the label's current width.
  /// </summary>
  private bool TextFits(string text) {
    if (Width <= 0 || Height <= 0)
      return true;

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
    if (text.Length <= ellipsis.Length)
      return text;

    // Calculate how many characters we can keep from start and end
    int preserveLength = Math.Max(1, (int)(text.Length * preserveRatio));

    // Adjust if the preserved portions + ellipsis exceed the original length
    if ((preserveLength * 2) + ellipsis.Length >= text.Length) {
      preserveLength = Math.Max(1, (text.Length - ellipsis.Length) / 2);
    }

    string startPart = text[..preserveLength];
    string endPart = text[^preserveLength..];

    // Try to end at a path separator on the start part
    if (smartBreak) {
      int lastSeparatorStart = Math.Max(startPart.LastIndexOf('\\'), startPart.LastIndexOf('/'));

      if (lastSeparatorStart > 0 && lastSeparatorStart < startPart.Length - 1) {
        startPart = startPart[..(lastSeparatorStart + 1)];
      }

      // Try to start at a path separator on the end part
      int backslashPos = endPart.IndexOf('\\');
      int slashPos = endPart.IndexOf('/');

      int firstSeparatorEnd = -1;
      if (backslashPos >= 0 && slashPos >= 0)
        firstSeparatorEnd = Math.Min(backslashPos, slashPos);
      else if (backslashPos >= 0)
        firstSeparatorEnd = backslashPos;
      else if (slashPos >= 0)
        firstSeparatorEnd = slashPos;

      // Try to start at a path separator on the end part
      if (firstSeparatorEnd > 0)
        endPart = endPart[firstSeparatorEnd..];
    }

    // Binary search for the longest text that fits
    return FindFittingTruncation(startPart, endPart);
  }

  /// <summary>
  /// Uses binary search to find the longest possible start/end combination that fits.
  /// </summary>
  private string FindFittingTruncation(string startPart, string endPart) {
    int startLen = startPart.Length;
    int endLen = endPart.Length;

    // Try progressively shorter combinations until it fits
    while (!TextFits(startPart[..startLen] + ellipsis + endPart[^endLen..])) {
      if (startLen > 0 && endLen > 0) {
        // Reduce the longer part
        if (startLen >= endLen) startLen--;
        else endLen--;
      }
      else if (startLen > 0) startLen--;
      else if (endLen > 0) endLen--;
        // Can't fit anything meaningful
      else return ellipsis;
    }

    return startPart[..startLen] + ellipsis + endPart[^endLen..];
  }
}

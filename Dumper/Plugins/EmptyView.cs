// EmptyView
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Drawing;
using System.Globalization;
using HtmlTags;

namespace Dumper.Plugins;

static class EmptyView {

  public static string Render(string message) =>
    WrapInShell(new HtmlTag("div").AddClass("empty-state")
      .Append(new HtmlTag("p").AddClass("empty-text").Text(message)));

  public static string RenderNoViewer(string fileTypeName) =>
    WrapInShell(new HtmlTag("div").AddClass("empty-state")
      .Append(new HtmlTag("p").AddClass("empty-text")
        .AppendHtml("No viewer plugin for ")
        .Append(new HtmlTag("span").Text(fileTypeName).AddClass("empty-file-type"))
        .AppendText("."))
      .Append(new HtmlTag("p").AddClass("empty-hint")
        .Text("Install a plugin or choose a different viewer from the dropdown above.")));

  public static string WrapInShell(HtmlTag bodyContent) {
    var colors = CaptureSystemColors();
    var styles = BaseStyles();

    var html = new HtmlTag("html").Attr("lang", "en")
      .Append(new HtmlTag("head")
        .Append(new HtmlTag("meta").Attr("charset", "utf-8"))
        .Append(new HtmlTag("meta").Attr("name", "viewport").Attr("content", "width=device-width, initial-scale=1"))
        .Append(new HtmlTag("style").Text(colors + "\n" + styles).Encoded(false)))
      .Append(new HtmlTag("body").Append(bodyContent));

    return "<!DOCTYPE html>\n" + html.ToString();
  }

  public static string WrapInShell(string htmlBody) {
    var colors = CaptureSystemColors();
    var styles = BaseStyles();

    var html = new HtmlTag("html").Attr("lang", "en")
      .Append(new HtmlTag("head")
        .Append(new HtmlTag("meta").Attr("charset", "utf-8"))
        .Append(new HtmlTag("meta").Attr("name", "viewport").Attr("content", "width=device-width, initial-scale=1"))
        .Append(new HtmlTag("style").Text(colors + "\n" + styles).Encoded(false)))
      .Append(new HtmlTag("body").AppendHtml(htmlBody));

    return "<!DOCTYPE html>\n" + html.ToString();
  }

  private static string CaptureSystemColors() {
    var invariantCulture = CultureInfo.InvariantCulture;

    var window = ToCss(SystemColors.Window);
    var windowText = ToCss(SystemColors.WindowText);
    var control = ToCss(SystemColors.Control);
    var controlText = ToCss(SystemColors.ControlText);
    var controlLight = ToCss(SystemColors.ControlLight);
    var controlDark = ToCss(SystemColors.ControlDark);
    var controlLightLight = ToCss(SystemColors.ControlLightLight);
    var controlDarkDark = ToCss(SystemColors.ControlDarkDark);
    var grayText = ToCss(SystemColors.GrayText);
    var highlight = ToCss(SystemColors.Highlight);
    var highlightText = ToCss(SystemColors.HighlightText);
    var hotTrack = ToCss(SystemColors.HotTrack);
    var inactiveCaption = ToCss(SystemColors.InactiveCaptionText);
    var buttonFace = ToCss(SystemColors.ButtonFace);
    var buttonShadow = ToCss(Color.FromArgb(0xA0, 0xA0, 0xA0));
    var buttonHighlight = ToCss(SystemColors.ButtonHighlight);
    var info = ToCss(SystemColors.Info);
    var infoText = ToCss(SystemColors.InfoText);
    var menuBar = ToCss(SystemColors.MenuBar);
    var menuText = ToCss(SystemColors.MenuText);

    var font = SystemFonts.DefaultFont;
    var fontName = font.FontFamily.Name;
    var fontSizePt = font.SizeInPoints.ToString("0.#", invariantCulture);
    var fontSizeDdu = (font.SizeInPoints / 72.0 * 96.0).ToString("0.#", invariantCulture);

    var labelFont = SystemFonts.DefaultFont;
    var fontSizeMinDdu = (labelFont.SizeInPoints / 72.0 * 96.0).ToString("0.#", invariantCulture);

    var dlu = font.SizeInPoints / 72.0 * 96.0 / 8.0 * 6.0;
    var padOuter = (0.5 * dlu).ToString("0", invariantCulture);
    var padInner = (0.25 * dlu).ToString("0", invariantCulture);
    var padHalf = (2 * dlu).ToString("0", invariantCulture);

    return $@":root {{
  --sys-window: {window};
  --sys-window-text: {windowText};
  --sys-control: {control};
  --sys-control-text: {controlText};
  --sys-control-light: {controlLight};
  --sys-control-dark: {controlDark};
  --sys-control-light-light: {controlLightLight};
  --sys-control-dark-dark: {controlDarkDark};
  --sys-gray-text: {grayText};
  --sys-highlight: {highlight};
  --sys-highlight-text: {highlightText};
  --sys-hot-track: {hotTrack};
  --sys-inactive-caption: {inactiveCaption};
  --sys-button-face: {buttonFace};
  --sys-button-shadow: {buttonShadow};
  --sys-button-highlight: {buttonHighlight};
  --sys-info: {info};
  --sys-info-text: {infoText};
  --sys-menu-bar: {menuBar};
  --sys-menu-text: {menuText};
}}

html {{
  --font-family: '{fontName}', 'Segoe UI', Tahoma, sans-serif;
  --font-size: {fontSizePt}pt;
  --font-size-px: {fontSizeDdu}px;
  --font-size-min: {fontSizeMinDdu}px;
  --line-height: 1.4;
  --pad-outer: {padOuter}px;
  --pad-inner: {padInner}px;
  --pad-half: {padHalf}px;
}}";
  }

  private static string BaseStyles() => @"
* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

html, body {
  height: 100%;
  overflow: auto;
}

body {
  font-family: var(--font-family);
  font-size: var(--font-size);
  line-height: var(--line-height);
  color: var(--sys-window-text);
  background: var(--sys-control);
  padding: var(--pad-outer);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
  text-rendering: optimizeLegibility;
  user-select: none;
  -webkit-user-select: none;
  cursor: default;
}

::selection {
  background: var(--sys-highlight);
  color: var(--sys-highlight-text);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - var(--pad-outer) * 2);
  gap: var(--pad-inner);
  text-align: center;
}

.empty-text {
  color: var(--sys-gray-text);
  line-height: var(--line-height);
}

.empty-hint {
  color: var(--sys-gray-text);
  max-width: 28em;
}

h1, h2, h3, h4, h5, h6 {
  color: var(--sys-window-text);
  margin-bottom: var(--pad-inner);
}

p {
  margin-bottom: var(--pad-half);
}

pre {
  background: var(--sys-control);
  color: var(--sys-control-text);
  border: 1px solid var(--sys-control-dark);
  padding: var(--pad-inner);
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
  font-size: max(var(--font-size-min), calc(var(--font-size) * 0.93));
  line-height: 1.5;
  overflow-x: auto;
}

code {
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
  font-size: max(var(--font-size-min), calc(var(--font-size) * 0.93));
}

img {
  max-width: 100%;
  height: auto;
}

table {
  border-collapse: collapse;
  width: 100%;
  margin-bottom: var(--pad-inner);
}

th, td {
  border: 1px solid var(--sys-control-dark);
  padding: var(--pad-half) var(--pad-inner);
  text-align: left;
  vertical-align: top;
}

th {
  background: var(--sys-button-face);
  color: var(--sys-control-text);
  font-weight: 600;
}

tr:nth-child(even) {
  background: var(--sys-control-light-light);
}

a {
  color: var(--sys-hot-track);
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}

button, input, select, textarea {
  font-family: inherit;
  font-size: inherit;
  color: var(--sys-control-text);
}

hr {
  border: none;
  border-top: 1px solid var(--sys-control-dark);
  margin: var(--pad-inner) 0;
}

.callout {
  background: var(--sys-info);
  color: var(--sys-info-text);
  border: 1px solid var(--sys-control-dark);
  padding: var(--pad-inner);
  margin-bottom: var(--pad-inner);
}";

  private static string ToCss(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}

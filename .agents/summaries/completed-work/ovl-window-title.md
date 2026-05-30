# OVL Window Title

Displays the OVL document name in window chrome.

## Changes

### macOS (`OvlWindowController.cs`)
- Add `FilePath` property
- `DocumentName` strips `.common.ovl` / `.unique.ovl` suffix
- `WindowTitle` override returns `DocumentName`

### Windows (`MainForm.cs`)
- On load, set `Text = $"OVL Dumper \u2014 {docName}"` with same suffix stripping

### Wiring (`OvlDocument.cs`)
- Pass `FileUrl?.Path` to controller in `MakeWindowControllers()`

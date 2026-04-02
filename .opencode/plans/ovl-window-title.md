# Plan: Add OVL document name to window title

## Context
The Dumper app opens OVL archives (`.common.ovl` / `.unique.ovl`). The user wants the extracted document name (e.g. "Water" from "Water.common.ovl") shown in the window title/subtitle. The `OvlWindowController` is currently a thin shell with no title logic.

On macOS, the document name serves as the project name displayed in the window subtitle. On Windows, the document name is appended as a hyphenated suffix to the main window title.

## Files to modify

### 1. `Dumper/Documents/OvlWindowController.cs`

Add three members:

- **`FilePath` property** (`string?`) — tracks the file path the user chose. Set by `OvlDocument` when creating the controller.
- **`DocumentName` computed property** (`string`) — derives the display name by stripping `.common.ovl` / `.unique.ovl` (case-insensitive) from the filename, falling back to plain `.ovl`, then to `Ovl.UnnamedOvl`.
- **`WindowTitle` override** (`string`) — returns `DocumentName`, wiring the derived name into the window chrome.

```csharp
using System.IO;

namespace Dumper.Documents;

public partial class OvlWindowController : NSWindowController {
  public OvlWindowController() { }
  public OvlWindowController(IntPtr handle) : base(handle) { }

  public string? FilePath { get; set; }

  public string DocumentName {
    get {
      if (FilePath == null) return Ovl.UnnamedOvl;
      var fileName = Path.GetFileName(FilePath);
      var lower = fileName.ToLower();
      if (lower.EndsWith(".common.ovl"))
        fileName = fileName[..^".common.ovl".Length];
      else if (lower.EndsWith(".unique.ovl"))
        fileName = fileName[..^".unique.ovl".Length];
      else if (lower.EndsWith(".ovl"))
        fileName = fileName[..^".ovl".Length];
      return fileName;
    }
  }

  public override string WindowTitle => DocumentName;
}
```

### 2. `Dumper/Documents/OvlDocument.cs`

Update `MakeWindowControllers()` to pass `FileUrl?.Path` to the controller:

```csharp
public override void MakeWindowControllers() {
  var controller = new OvlWindowController { FilePath = FileUrl?.Path };
  AddWindowController(controller);
}
```

### 3. `Dumper/MainForm.cs` (Windows)

Update the window title when an OVL is loaded by appending the document name as a hyphenated suffix (e.g. "OVL Dumper — Water"):

```csharp
private void LoadOvl(Ovl ovl) {
  // ... existing tree-building code ...

  // Update window title with document name
  var fileName = Path.GetFileName(ovl.FileName);
  var lower = fileName.ToLower();
  string docName;
  if (lower.EndsWith(".common.ovl"))
    docName = fileName[..^".common.ovl".Length];
  else if (lower.EndsWith(".unique.ovl"))
    docName = fileName[..^".unique.ovl".Length];
  else if (lower.EndsWith(".ovl"))
    docName = fileName[..^".ovl".Length];
  else
    docName = Ovl.UnnamedOvl;
  Text = $"OVL Dumper \u2014 {docName}";
}
```

## Verification
- Build the solution and confirm no compiler errors.
- Open a `.common.ovl` or `.unique.ovl` file and verify:
  - **macOS**: The window subtitle shows the extracted name (e.g. "Water" for "Water.common.ovl").
  - **Windows**: The window title reads "OVL Dumper — Water".
- Open an untitled document and verify the title falls back to "Unnamed OVL".

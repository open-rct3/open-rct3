// OvlWindowController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

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

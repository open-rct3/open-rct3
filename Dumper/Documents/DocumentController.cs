// DocumentController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System.Diagnostics;
using ObjCRuntime;

namespace Dumper.Documents;

// See https://developer.apple.com/documentation/appkit/nsdocumentcontroller
public class DocumentController : NSDocumentController {
  public DocumentController() : base(NSObjectFlag.Empty) { }
  public DocumentController(NativeHandle handle) : base(handle) { }

  public new static void Init() {
    _ = new DocumentController();
  }

  public override void NoteNewRecentDocumentURL(NSUrl url) {
    Debug.WriteLine(url.ToString());
    base.NoteNewRecentDocumentURL(url);
  }

  /// <summary>
  /// Prompt the user to open an OVL file.
  /// </summary>
  public void OpenDocument() {
    OpenDocument(null);
  }
}

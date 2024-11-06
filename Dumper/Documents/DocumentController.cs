// DocumentController
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
#if DEBUG
#define TRACE
#endif

using System.Diagnostics;
using ObjCRuntime;

namespace Dumper.Documents;

// See https://developer.apple.com/documentation/appkit/nsdocumentcontroller
public class DocumentController : NSDocumentController {
  public DocumentController() : base(NSObjectFlag.Empty) { }
  public DocumentController(NativeHandle handle) : base(handle) { }

  public new static void Init() {
    var controller = new DocumentController();
    (controller as NSObject).Init();
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

  public override void OpenDocument(NSUrl url, bool display, OpenDocumentCompletionHandler handler) {
    Debug.WriteLine($"Opening document: {url.Path}");
    if (Path.GetExtension(url.Path)?.ToLower().EndsWith("ovl") ?? false) {
      var isOpen = Documents.Any(doc => (doc.FileUrl?.Path ?? "") == url.Path);
      NSError? error = null;
      // TODO: Handle `canConcurrentlyReadDocumentsOfType`. See https://developer.apple.com/documentation/appkit/nsdocument/1515216-canconcurrentlyreaddocumentsofty
      var document = (isOpen
        ? Documents.First(doc => doc.FileUrl!.Path == url.Path)
        : MakeDocument(url, url, "ovl", out error)) as NSDocument ?? throw new InvalidOperationException();
      AddDocument(document);
      if (display && !isOpen) document.MakeWindowControllers();
      if (display) document.ShowWindows();
      handler(document, isOpen, error!);
    } else base.OpenDocument(url, display, (document, open, error) => {
        Debug.WriteLine(document);
        Debug.WriteLine($"Was already open: {open}");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        Debug.WriteLineIf(error != null, error);
        handler(document, open, error ?? throw new ArgumentNullException(nameof(error)));
      });
  }

  public override NSObject OpenUntitledDocument(bool displayDocument, out NSError outError) {
    Trace.TraceInformation(displayDocument ? "Displaying document..." : "Opening a hidden document...");
    var result = base.OpenUntitledDocument(displayDocument, out outError);
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    if (outError != null) Trace.TraceError(outError.ToString());
    return result;
  }

  public override NSObject MakeDocument(NSUrl? url, NSUrl contentsUrl, string typeName, out NSError outError) {
    Debug.WriteLine($"Making document ({typeName}): {contentsUrl.Path}");
    return typeName == "ovl" ? new OvlDocument(contentsUrl, out outError!) : base.MakeDocument(url, contentsUrl, typeName, out outError);
  }

  public override void AddDocument(NSDocument document) {
    Debug.WriteLine($"Opened document: {document.FileUrl?.Path ?? "Other document"}");
    base.AddDocument(document);
  }

  public override void RemoveDocument(NSDocument document) {
    base.RemoveDocument(document);
  }
}

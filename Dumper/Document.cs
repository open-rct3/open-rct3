// Document
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

#if DEBUG
#define TRACE
#endif

using AppKit;
using Foundation;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UniformTypeIdentifiers;

using OVL;

namespace Dumper;

// See https://developer.apple.com/documentation/appkit/documents_data_and_pasteboard/developing_a_document-based_app
// See https://developer.apple.com/documentation/uniformtypeidentifiers/defining-file-and-data-types-for-your-app
// TODO: https://developer.apple.com/documentation/appkit/nsdocument#1652154
[Register("Document")]
public class Document : NSDocument {
  private const string WindowControllerName = "OVL Document Window Controller";

  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  private readonly UTType ovlContentType = UTType.CreateFromIdentifier("com.open-rct3.ovl")!;

  private Ovl? ovl;
  private long oldHash;

  /// <summary>
  /// Create a new Untitled document.
  /// </summary>
  public Document() { }

  // See https://developer.apple.com/documentation/appkit/nsdocument/1515097-initwithcontentsofurl
  public Document(NSUrl file, out NSError? error) : base(file, "ovl", out error) {
    // Add your subclass-specific initialization here.
  }

  [Export("autosavesInPlace")]
  public static bool AutosaveInPlace() {
    return true;
  }

  public override string? DisplayName {
    get => ovl?.Description ?? Ovl.UnnamedOvl;
    set => base.DisplayName = ovl != null
      ? ovl.Description = value ?? ovl.FileName
      : value ?? Ovl.UnnamedOvl;
  }

  public override string DefaultDraftName => Ovl.UnnamedOvl;

  public override bool IsDraft {
    get => FileUrl != null && IsDocumentEdited;
    set => base.IsDraft = value;
  }

  public override bool IsDocumentEdited => (ovl?.GetHashCode() ?? 0) != oldHash;
  public override bool IsEntireFileLoaded => false;
  public override bool IsInViewingMode => true;

  public override void MakeWindowControllers() {
    AddWindowController(
      (NSWindowController) NSStoryboard.FromName("Main", null)
        .InstantiateControllerWithIdentifier(WindowControllerName)
    );
  }

  public override bool ReadFromUrl(NSUrl url, string typeName, out NSError? outError) {
    try {
      Debug.Assert(url.Path != null);
      // TODO: Add a spinner indicator and spin it while the OVL is loading in a BG thread
      ovl = Ovl.Open(url.Path);
      oldHash = ovl.GetHashCode();

      outError = null;
      Trace.TraceInformation($"Document opened: \"{url.Path}\"");
      return true;
    } catch (Exception ex) {
      // FIXME: https://benscheirman.com/2019/10/troubleshooting-appkit-file-permissions.html
      outError = ex.ToError();
      return false;
    }
  }

  public override bool WriteToUrl(NSUrl url, string typeName, out NSError? outError) {
    // TODO: Implement OVL editing
    throw new NotImplementedException();
  }
}

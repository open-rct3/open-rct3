// Document
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

#if DEBUG
#define TRACE
#endif

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Dumper.Models;
using OVL;
using UniformTypeIdentifiers;

namespace Dumper.Documents;

// See https://developer.apple.com/documentation/appkit/documents_data_and_pasteboard/developing_a_document-based_app
// See https://developer.apple.com/documentation/uniformtypeidentifiers/defining-file-and-data-types-for-your-app
// TODO: https://developer.apple.com/documentation/appkit/nsdocument#1652154
[Register("Document")]
public sealed class OvlDocument : NSDocument {
  private const string WindowControllerName = "OVL Document Window Controller";
  [SuppressMessage(
    "ReSharper",
    "InconsistentNaming",
    Justification = "Pascal case would conflict with NSDocument.FileType"
  )]
  private const string fileType = "ovl";
  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public readonly UTType ContentType = UTType.CreateFromIdentifier("com.open-rct3.ovl")!;

  // TODO: Use the memento pattern for Undo/Redo. See https://refactoring.guru/design-patterns/memento
  private Ovl ovl;
  public Memento<Ovl> Memento { get; }

  /// <summary>
  /// Create a new Untitled document.
  /// </summary>
  public OvlDocument(Ovl? archive = null) {
    ovl = archive ?? new Ovl("untitled.common.ovl");
    Memento = new Memento<Ovl>(ovl);
    FileModificationDate = NSDate.Now;
  }

  /// <summary>
  /// Customize the reopening of autosaved documents.
  /// </summary>
  // See https://developer.apple.com/documentation/appkit/nsdocument/1515097-initwithcontentsofurl
  public OvlDocument(NSUrl file, out NSError? error) : base(file, fileType, out error) {
    Debug.Assert(file.Path != null, "file.Path != null");
    ReadFromUrl(FileUrl = file, FileType, out error);
    Debug.Assert(ovl != null);
    Memento = new Memento<Ovl>(ovl);

    var modificationDate = NSFileManager.DefaultManager.GetAttributes(file.Path, out error)?.ModificationDate;
    FileModificationDate = modificationDate ?? throw new InvalidOperationException();
  }

  [Export("autosavesInPlace")]
  public static bool AutosaveInPlace() {
    return true;
  }

  public override string FileType {
    get => fileType;
    set { }
  }

  public override string? DisplayName {
    get => ovl.Description;
    set => base.DisplayName = ovl.Description = value ?? ovl.FileName;
  }

  public override string DefaultDraftName => Ovl.UnnamedOvl;

  public override bool IsDraft {
    get => FileUrl != null && IsDocumentEdited;
    set => base.IsDraft = value;
  }

  public override bool IsDocumentEdited => Memento.HasChanges;
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

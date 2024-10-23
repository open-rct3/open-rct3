// ProjectDocument
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using Dumper.Models;
using Foundation;
using OVL;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using UniformTypeIdentifiers;

namespace Dumper.Documents;

[Register("ProjectDocument")]
public class ProjectDocument : NSDocument {
  private const string fileType = "fgdkproj";
  // TODO: Use the memento pattern for Undo/Redo. See https://refactoring.guru/design-patterns/memento
  private Project project = new();
  public Memento<Project> Memento { get; }

  /// <summary>
  /// Customize the reopening of autosaved documents.
  /// </summary>
  // See https://developer.apple.com/documentation/appkit/nsdocument/1515097-initwithcontentsofurl
  public ProjectDocument(NSUrl file, out NSError? error) : base(file, fileType, out error) {
    Debug.Assert(file.Path != null, "file.Path != null");
    ReadFromUrl(FileUrl = file, FileType, out error);
    Memento = new Memento<Project>(project);

    var modificationDate = NSFileManager.DefaultManager.GetAttributes(file.Path, out error)?.ModificationDate;
    FileModificationDate = modificationDate ?? throw new InvalidOperationException();
  }

  [Export("autosavesInPlace")]
  public static bool AutosaveInPlace() {
    return true;
  }

  public Project Project => project;

  public override string FileType {
    get => fileType;
    set { }
  }

  public override string? DisplayName {
    get => project.Name;
    set => base.DisplayName = Project.Name = value ?? Project.UnnamedProject;
  }

  public override string DefaultDraftName => Project.UnnamedProject;

  public override bool IsDraft {
    get => FileUrl != null && IsDocumentEdited;
    set => base.IsDraft = value;
  }

  public override bool IsDocumentEdited => Memento.HasChanges;
  public override bool IsEntireFileLoaded => false;
  public override bool IsInViewingMode => false;

  /* TODO: How to handle this?
  public override void MakeWindowControllers() {
    AddWindowController(
      (NSWindowController) NSStoryboard.FromName("Main", null)
        .InstantiateControllerWithIdentifier(WindowControllerName)
    );
  }
  */

  public override bool ReadFromUrl(NSUrl url, string typeName, out NSError? outError) {
    try
    {
      Debug.Assert(url.Path != null);
      // TODO: Add a spinner indicator and spin it while the project is loading
      // TODO: Open the url and load the project
      throw new NotImplementedException();

      outError = null;
      Trace.TraceInformation($"Project opened: \"{url.Path}\"");
      return true;
    }
    catch (Exception ex)
    {
      // FIXME: https://benscheirman.com/2019/10/troubleshooting-appkit-file-permissions.html
      outError = ex.ToError();
      return false;
    }
  }

  public override bool WriteToUrl(NSUrl url, string typeName, out NSError? outError) {
    // TODO: Serialize the project to disk
    throw new NotImplementedException();
  }
}

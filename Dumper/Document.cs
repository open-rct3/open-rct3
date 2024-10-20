// Document
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using AppKit;
using Foundation;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using OVL;

namespace Dumper;

// See https://developer.apple.com/documentation/uniformtypeidentifiers/defining-file-and-data-types-for-your-app
[Register("Document")]
public class Document : NSDocument {
  private Ovl? ovl = null;

  /// <summary>
  /// Create a new Untitled document.
  /// </summary>
  public Document() : base() { }

  /// <see cref="https://developer.apple.com/documentation/appkit/nsdocument/1515097-initwithcontentsofurl"/>
  public Document(NSUrl file, out NSError? error) : base(file, "ovl", out error) {
    // Add your subclass-specific initialization here.
  }

  /// <see cref="https://developer.apple.com/documentation/appkit/nsdocument/1515097-initwithcontentsofurl"/>
  public Document(string fileName, out NSError? error) : base(new NSUrl(fileName), "ovl", out error) {
    // Add your subclass-specific initialization here.
  }

  public override void WindowControllerDidLoadNib(NSWindowController windowController) {
    base.WindowControllerDidLoadNib(windowController);
    // Add any code here that needs to be executed once the windowController has loaded the document's window.
  }

  [Export("autosavesInPlace")]
  public static bool AutosaveInPlace() {
    return true;
  }

  public override string DefaultDraftName => "OVL";

  public override bool IsDocumentEdited => false;

  public override bool IsEntireFileLoaded => false;

  public override bool IsInViewingMode => true;

  public override void MakeWindowControllers() {
    // Override to return the Storyboard file name of the document.
    AddWindowController(
      (NSWindowController) NSStoryboard.FromName("Main", null)
        .InstantiateControllerWithIdentifier("Document Window Controller")
    );
  }

  public override bool ReadFromUrl(NSUrl url, string typeName, out NSError? outError) {
    try
    {
      Debug.Assert(url.Path != null);
      // FIXME: The app hangs here and becomes unresponsive
      // TODO: Add a spinner indicator and spin it while the OVL is loading in a BG thread
      ovl = Ovl.Open(url.Path);

      outError = null;
      return true;
    }
    catch (Exception ex)
    {
      ShowError(new Exception($"{typeName}: {ex.Message}", ex)).Wait();

      // FIXME: https://benscheirman.com/2019/10/troubleshooting-appkit-file-permissions.html
      outError = NSErrorExtensions.FromException(ex);
      return false;
    }
  }

  public override bool WriteToUrl(NSUrl url, string typeName, out NSError? outError) {
    // TODO: Implement OVL editing
    throw new NotImplementedException();
  }

  private async Task ShowError(Exception ex) {
    var sheetCompleted = new TaskCompletionSource();
    new NSAlert {
      MessageText = ex.Message,
      AlertStyle = NSAlertStyle.Informational
    }.BeginSheet(this.WindowForSheet, () => { sheetCompleted.SetException(ex); });
    await sheetCompleted.Task;
  }
}

internal enum ErrorCode : ushort {
  Exception = 1
}

// ReSharper disable once InconsistentNaming
internal static class NSErrorExtensions {
  // See https://stackoverflow.com/a/3276356/1363247
  public static NSError FromException(Exception ex) {
    var domain = new NSString(NSBundle.MainBundle.BundleIdentifier);
    var exData = new NSDictionary();
    Debug.Assert(exData.TryAdd(new NSString("domain"), domain));
    Debug.Assert(exData.TryAdd(new NSString("message"), new NSString(ex.Message)));
    Debug.Assert(
      exData.TryAdd(new NSString("stack"), new NSString(ex.StackTrace ?? "Could not retreive stack trace!"))
    );
    return new NSError(domain, (nint) ErrorCode.Exception, exData);
  }
}

// AppDelegate
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable InconsistentNaming

namespace Dumper;

internal enum ErrorCode : ushort {
  Exception = 1
}

internal static class NSErrorExtensions {
  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public static Exception ToException(this NSError error) {
    return new ErrorException(error);
  }

  public static Task<NSModalResponse> ShowAlert(this NSError error, NSWindow? windowForSheet = null) {
    var alert = new NSAlert {
      MessageText = error.LocalizedDescription,
      AlertStyle = NSAlertStyle.Critical
    };
    return alert.BeginSheetAsync(windowForSheet);
  }
}

internal static class ExceptionExtensions {
  public static NSError ToError(this Exception ex) {
    return new Error(ex);
  }

  public static Task<NSModalResponse> ShowAlert(this Exception ex, NSWindow? windowForSheet = null) {
    var alert = new NSAlert {
      MessageText = ex.Message,
      AlertStyle = NSAlertStyle.Critical
    };
    return alert.BeginSheetAsync(windowForSheet);
  }
}

public sealed class UnderlyingErrors(IEnumerable<Exception> errors) : Exception {
  public readonly IEnumerable<Exception> Errors = errors;
}

[SuppressMessage(
  "Interoperability",
  "CA1416:Validate platform compatibility",
  Justification = "This app requires at least macOS 10.15"
)]
public sealed class ErrorException(NSError error) : Exception(
  error.LocalizedDescription,
  error.UnderlyingErrors.Length > 0
    ? new UnderlyingErrors(error.UnderlyingErrors.Select(err => err.ToException()))
    : null
) {
  public readonly NSError Error = error;

  public string Domain => Error.Domain;
  public int ErrorCode => Error.Code.ToInt32();
}

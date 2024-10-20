using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

// ReSharper disable InconsistentNaming

namespace Dumper;

internal enum ErrorCode : ushort {
  Exception = 1
}

sealed internal class UnderlyingErrors(IEnumerable<Exception> errors) : Exception("Multiple errors occurred!") {
  public readonly IEnumerable<Exception> Errors = errors;
}

internal static class NSErrorExtensions {
  [SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "This app requires at least macOS 10.15"
  )]
  public static Exception ToException(this NSError error) {
    var underlyingErrors = error.UnderlyingErrors.Select(err => err.ToException()).ToArray();
    var innerException = underlyingErrors.Length > 0 ? new UnderlyingErrors(underlyingErrors) : null;
    // TODO: Retrieve the stack-trace from `UserInfo` and supply it to a new `ErrorException` type.
    return error.Domain == NSBundle.MainBundle.BundleIdentifier
      ? new Exception(error.UserInfo["message"].ToString(), innerException)
      : new Exception(error.Description, innerException);
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
  // See https://stackoverflow.com/a/3276356/1363247
  public static NSError ToError(this Exception ex) {
    var domain = new NSString(NSBundle.MainBundle.BundleIdentifier);
    var stackTrace = ex.StackTrace ?? "Could not retrieve stack trace!";

    var details = new Dictionary<NSString, NSObject>();
    Debug.Assert(details.TryAdd(new NSString("domain"), domain));
    Debug.Assert(details.TryAdd(new NSString("message"), new NSString(ex.Message)));
    Debug.Assert(details.TryAdd(new NSString("stack"), new NSString(stackTrace)));

    var userInfo = NSDictionary.FromObjectsAndKeys(details.Values.ToArray(), details.Keys.Cast<NSObject>().ToArray());
    return new NSError(domain, (nint) ErrorCode.Exception, userInfo);
  }

  public static Task<NSModalResponse> ShowAlert(this Exception ex, NSWindow? windowForSheet = null) {
    var alert = new NSAlert {
      MessageText = ex.Message,
      AlertStyle = NSAlertStyle.Critical
    };
    return alert.BeginSheetAsync(windowForSheet);
  }
}

// Error
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
namespace Dumper;

// See https://stackoverflow.com/a/3276356/1363247
public sealed class Error : NSError {
  public Exception? Exception { get; }

  public Error(Exception exception) : base(
    new NSString(NSBundle.MainBundle.BundleIdentifier),
    (nint) ErrorCode.Exception,
    NSDictionary<NSString, NSObject>.FromObjectsAndKeys(
      [new NSString($"{NSBundle.MainBundle.BundleIdentifier}.error")],
      [new NSString(exception.Message)]
    )
  ) {
    Exception = exception;
  }

  public Error(string message) : base(
    new NSString(NSBundle.MainBundle.BundleIdentifier),
    (nint) ErrorCode.Exception,
    NSDictionary<NSString, NSObject>.FromObjectsAndKeys(
      [new NSString($"{NSBundle.MainBundle.BundleIdentifier}.error")],
      [new NSString(message)]
    )
  ) {
    Exception = null;
  }

  public override string LocalizedDescription => Exception?.Message ?? base.LocalizedDescription;

  public override string ToString() {
    return Exception?.Message ?? base.ToString();
  }
}

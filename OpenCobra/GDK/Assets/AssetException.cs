// AssetException
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.GDK.Assets;

[Serializable]
internal class AssetException : Exception {
  public AssetException() {}

  public AssetException(Exception? innerException) : base("Could not process asset", innerException) {}

  public AssetException(string? message) : base(message) {}

  public AssetException(string? message, Exception? innerException) : base(message, innerException) {}
}

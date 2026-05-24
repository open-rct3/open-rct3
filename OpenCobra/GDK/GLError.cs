// OpenGL Error
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NLog;
using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;

namespace OpenCobra.GDK;

/// <summary>
/// Represents an OpenGL error.
/// </summary>
/// <remarks>
/// <see cref="GL.GetError"/>
/// </remarks>
public class GLError(string? message = null, Exception? innerException = null)
  : Exception(message, innerException) {}

public static class GLExtensions {
  private static readonly Logger logger = LogManager.GetCurrentClassLogger();

  /// <summary>
  /// Check for an OpenGL validation error.
  /// </summary>
  /// <remarks>The given <paramref name="context"/> is logged as a tracing message.</remarks>
  /// <param name="gl">OpenGL context</param>
  /// <param name="context">Local application context</param>
  /// <exception cref="GLError"></exception>
  [Conditional("DEBUG")]
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void CheckError(this GL gl, string? context = null) {
    var errCode = gl.GetError();
    if (errCode == 0) return;

    logger.Trace(context);
    logger.Error("Validation error: {code}", errCode);
    throw new GLError(string.Format("Unexpected OpenGL validation error: {0}", errCode));
  }
}

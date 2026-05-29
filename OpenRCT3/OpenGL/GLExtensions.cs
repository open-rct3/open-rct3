// OpenGL API Extensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

// ReSharper disable InconsistentNaming
using NLog;
using Silk.NET.OpenGL;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenRCT3.OpenGL;

public static class GLExtensions {
  [Conditional("DEBUG")]
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void HookupDebugCallback(this GL gl) {
    if (!gl.IsExtensionPresent("GL_ARB_debug_output") || !gl.IsExtensionPresent("GL_KHR_debug")) return;

    gl.Enable(EnableCap.DebugOutput);
    gl.Enable(EnableCap.DebugOutputSynchronous);
    gl.DebugMessageCallback<byte>(DebugProc, []);

    // TODO: Decide which debugging logs to keep in prod, if any
    //gl.DebugMessageControl()
  }

  internal static void DebugProc(GLEnum src, GLEnum type, int id, GLEnum severity, int msgLen, nint msgPtr, nint _userData) {
    // Ignore non-significant error and warning codes
    // QUESTION: What are these errors?
    if (id == 131169 || id == 131185 || id == 131218 || id == 131204) return;

    // Massage together a log event
    var logger = LogManager.GetCurrentClassLogger();
    var message = Marshal.PtrToStringAnsi(msgPtr, msgLen);
    var @event = new LogEventInfo {
      Level = LogLevel.Debug,
      Message = string.Format("{0} validation error: ({1}: {2}) {3}", src switch {
        GLEnum.DebugSourceApi => "API",
        GLEnum.DebugSourceWindowSystem => "Window System",
        GLEnum.DebugSourceShaderCompiler => "Shader Compiler",
        GLEnum.DebugSourceThirdParty => "Third Party",
        GLEnum.DebugSourceApplication => "Application",
        GLEnum.DebugSourceOther => "Other",
        _ => "Unknown OpenGL"
      }, id, type switch {
        GLEnum.DebugTypeDeprecatedBehavior => "Deprecation",
        GLEnum.DebugTypeUndefinedBehavior => "Undefined Behavior",
        GLEnum.DebugTypePushGroup => "Push Group",
        GLEnum.DebugTypePopGroup => "Pop Group",
        _ => type.ToString().Replace("DebugType", "")
      }, message)
    };

    switch (severity) {
      case GLEnum.DebugSeverityNotification:
        logger.Warn(message);
        break;
      case GLEnum.DebugSeverityLow:
      case GLEnum.DebugSeverityMedium:
        @event.Level = LogLevel.Error;
        break;
      case GLEnum.DebugSeverityHigh:
        @event.Level = LogLevel.Error;
        break;
      default: throw new InvalidOperationException("Unknown OpenGL debug severity");
    }

    logger.Log(@event);
  }
}

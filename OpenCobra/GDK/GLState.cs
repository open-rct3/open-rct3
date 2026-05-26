// GL State
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Concurrent;
using DryIoc;
using OpenCobra.GDK.Services;
using Silk.NET.OpenGL;

namespace OpenCobra.GDK;

/// <summary>
/// Captures and restores the OpenGL state machine via a thread-safe concurrent
/// stack. Avoids manual save/restore boilerplate across the render loop and
/// ImGui integration.
/// </summary>
/// <remarks>
/// Use with <c>using var guard = GLState.Push();</c> for RAII-style automatic
/// restoration.
/// </remarks>
// ReSharper disable once InconsistentNaming
public readonly struct GLState : IDisposable {
  private readonly static ConcurrentStack<GLState> stack = new();

  private readonly int activeTexture;
  private readonly int currentProgram;
  private readonly int textureBinding2D;
  private readonly int arrayBufferBinding;
  private readonly int vertexArrayBinding;
  private readonly int scissorBoxX;
  private readonly int scissorBoxY;
  private readonly uint scissorBoxWidth;
  private readonly uint scissorBoxHeight;
  private readonly int blendSrcRgb;
  private readonly int blendSrcAlpha;
  private readonly int blendDstRgb;
  private readonly int blendDstAlpha;
  private readonly int blendEquationRgb;
  private readonly int blendEquationAlpha;
  private readonly bool blendEnabled;
  private readonly bool cullFaceEnabled;
  private readonly bool depthTestEnabled;
  private readonly bool stencilTestEnabled;
  private readonly bool scissorTestEnabled;
  private readonly int frontPolygonMode;
  private readonly int backPolygonMode;

  private GLState(GL gl) {
    activeTexture = gl.GetInteger(GLEnum.ActiveTexture);
    currentProgram = gl.GetInteger(GLEnum.CurrentProgram);
    textureBinding2D = gl.GetInteger(GLEnum.TextureBinding2D);
    arrayBufferBinding = gl.GetInteger(GLEnum.ArrayBufferBinding);
    vertexArrayBinding = gl.GetInteger(GLEnum.VertexArrayBinding);

    Span<int> scissor = stackalloc int[4];
    gl.GetInteger(GLEnum.ScissorBox, scissor);
    scissorBoxX = scissor[0];
    scissorBoxY = scissor[1];
    scissorBoxWidth = (uint)scissor[2];
    scissorBoxHeight = (uint)scissor[3];

    blendSrcRgb = gl.GetInteger(GLEnum.BlendSrcRgb);
    blendSrcAlpha = gl.GetInteger(GLEnum.BlendSrcAlpha);
    blendDstRgb = gl.GetInteger(GLEnum.BlendDstRgb);
    blendDstAlpha = gl.GetInteger(GLEnum.BlendDstAlpha);
    blendEquationRgb = gl.GetInteger(GLEnum.BlendEquationRgb);
    blendEquationAlpha = gl.GetInteger(GLEnum.BlendEquationAlpha);

    blendEnabled = gl.IsEnabled(EnableCap.Blend);
    cullFaceEnabled = gl.IsEnabled(EnableCap.CullFace);
    depthTestEnabled = gl.IsEnabled(EnableCap.DepthTest);
    stencilTestEnabled = gl.IsEnabled(EnableCap.StencilTest);
    scissorTestEnabled = gl.IsEnabled(EnableCap.ScissorTest);

    Span<int> polyMode = stackalloc int[2];
    gl.GetInteger(GLEnum.PolygonMode, polyMode);
    frontPolygonMode = polyMode[0];
    backPolygonMode = polyMode[1];
  }

  /// <summary>
  /// Captures the current OpenGL state and pushes it onto a concurrent stack.
  /// </summary>
  /// <returns>
  /// A <see cref="GLState"/> that will restore the captured state when
  /// disposed.
  /// </returns>
  public static GLState Push() {
    var gl = Scene.IoC.Resolve<IContextSource>().Context;
    var state = new GLState(gl);
    stack.Push(state);
    return state;
  }

  /// <summary>
  /// Pops the most recently captured state from the stack and restores it.
  /// </summary>
  public static void Pop() {
    if (!stack.TryPop(out var state)) return;
    state.Restore();
  }

  /// <summary>
  /// Restores every captured GL field to its pre-Push value.
  /// </summary>
  private void Restore() {
    var gl = Scene.IoC.Resolve<IContextSource>().Context;

    SetEnabled(gl, EnableCap.Blend, blendEnabled);
    gl.CheckError("GLState restore Blend");
    SetEnabled(gl, EnableCap.CullFace, cullFaceEnabled);
    gl.CheckError("GLState restore CullFace");
    SetEnabled(gl, EnableCap.DepthTest, depthTestEnabled);
    gl.CheckError("GLState restore DepthTest");
    SetEnabled(gl, EnableCap.StencilTest, stencilTestEnabled);
    gl.CheckError("GLState restore StencilTest");
    SetEnabled(gl, EnableCap.ScissorTest, scissorTestEnabled);
    gl.CheckError("GLState restore ScissorTest");

    gl.BlendEquationSeparate(
      (BlendEquationModeEXT)blendEquationRgb,
      (BlendEquationModeEXT)blendEquationAlpha);
    gl.CheckError("GLState restore BlendEquation");
    gl.BlendFuncSeparate(
      (BlendingFactor)blendSrcRgb,
      (BlendingFactor)blendDstRgb,
      (BlendingFactor)blendSrcAlpha,
      (BlendingFactor)blendDstAlpha);
    gl.CheckError("GLState restore BlendFunc");

    gl.PolygonMode(TriangleFace.Front, (PolygonMode)frontPolygonMode);
    gl.CheckError("GLState restore PolygonMode Front");
    gl.PolygonMode(TriangleFace.Back, (PolygonMode)backPolygonMode);
    gl.CheckError("GLState restore PolygonMode Back");

    gl.Scissor(scissorBoxX, scissorBoxY, scissorBoxWidth, scissorBoxHeight);
    gl.CheckError("GLState restore ScissorBox");

    gl.ActiveTexture((TextureUnit)activeTexture);
    gl.CheckError("GLState restore ActiveTexture");
    gl.BindTexture(TextureTarget.Texture2D, (uint)textureBinding2D);
    gl.CheckError("GLState restore Texture2D");
    gl.BindVertexArray((uint)vertexArrayBinding);
    gl.CheckError("GLState restore VAO");
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)arrayBufferBinding);
    gl.CheckError("GLState restore ArrayBuffer");
    gl.UseProgram((uint)currentProgram);
    gl.CheckError("GLState restore Program");
  }

  public void Dispose() => Pop();

  private static void SetEnabled(GL gl, EnableCap cap, bool enabled) {
    if (enabled) gl.Enable(cap);
    else gl.Disable(cap);
  }
}

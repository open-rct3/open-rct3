// GL State
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Concurrent;
using DryIoc;
using OpenCobra.GDK.Platform;
using Silk.NET.OpenGL;

namespace OpenCobra.GDK;

/// <summary>
/// <para>
/// Captures and restores the OpenGL state machine via a thread-safe concurrent
/// stack. Avoids manual save/restore boilerplate across the render loop and
/// <see href="https://www.nuget.org/packages/Silk.NET.OpenGL.Extensions.ImGui">ImGui</see>
/// integration.
/// </para>
/// </summary>
/// <remarks class="usage">
/// <para>
/// <c>using var guard = GLState.Push();</c> for
/// <abbr title="Resource acquisition is initialization">RAII</abbr>-style automatic
/// restoration. When <c>guard</c> is disposed, <see cref="Pop"/> is automatically called.
/// </para>
/// </remarks>
/// <remarks class="notes">
/// <para>
/// <see cref="PolygonMode"/> is always restored using
/// <see cref="TriangleFace.FrontAndBack"/> regardless of the current profile.
/// </para>
/// <para>
/// This is intentional: <c>GL_FRONT_AND_BACK</c> is valid in all OpenGL versions
/// (2.0+), whereas <c>GL_FRONT</c> and <c>GL_BACK</c> are invalid in core
/// profile (OpenGL 3.2+). See
/// <see href="https://docs.gl/gl2/glPolygonMode">glPolygonMode</see>
/// and <see href="https://docs.gl/gl3/glPolygonMode">glPolygonMode (GL3)</see>.
/// </para>
/// </remarks>
// ReSharper disable once InconsistentNaming
public readonly struct GLState : IDisposable {
  private readonly static ConcurrentStack<GLState> stack = new();

  private readonly Version version;
  private readonly ContextFlagMask contextFlags;
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
  private readonly int frontAndBackPolygonMode;

  // FIXME: This feels incorrect. See 
  public readonly bool IsCoreProfile =>
    contextFlags.HasFlag(ContextFlagMask.DebugBit) ||
    version.Major >= 3;

  private GLState(GL gl) {
    if (Version.TryParse(gl.GetStringS(StringName.Version), out var version))
      this.version = version;
    else this.version = new();
    contextFlags = (ContextFlagMask)gl.GetInteger(GetPName.ContextFlags);

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
    frontAndBackPolygonMode = polyMode[0];
  }

  /// <summary>
  /// Captures the current OpenGL state and pushes it onto a concurrent stack.
  /// </summary>
  /// <returns>
  /// A <see cref="GLState"/> that will restore the captured state when
  /// disposed.
  /// </returns>
  public static GLState Push() {
    var gl = Scene.IoC.Resolve<GL>();
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
    var gl = Scene.IoC.Resolve<GL>();

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

    gl.PolygonMode(TriangleFace.FrontAndBack, (PolygonMode)frontAndBackPolygonMode);
    gl.CheckError("GLState restore PolygonMode");

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

  public void Dispose() {
    // RAII; only pop if this instance is at the top of the stack
    if (stack.TryPeek(out var top) && top.Equals(this))
      Pop();
  }

  private static void SetEnabled(GL gl, EnableCap cap, bool enabled) {
    if (enabled) gl.Enable(cap);
    else gl.Disable(cap);
  }
}

using NUnit.Framework;
using OpenCobra.GDK.Shaders;
using Silk.NET.OpenGL;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class ShaderProgramTests {
  [Test]
  public void ShaderProgramCreation_EmptySources() {
    var shader = new ShaderProgram(0);
    using (Assert.EnterMultipleScope()) {
      Assert.That(shader.Shader.Handle, Is.Zero);
      Assert.That(shader.Attributes, Is.Empty);
      Assert.That(shader.Uniforms, Is.Empty);
    }
  }

  [Test]
  public void UniformBinding_CanAddAndRetrieve() {
    var shader = new ShaderProgram(0);
    shader.Uniforms.Add(new Uniform { Name = "u_Test", Type = UniformType.Float, Value = 1.0f });
    Assert.That(shader.Uniforms, Has.Count.EqualTo(1));
    Assert.That(shader.Uniforms[0].Name, Is.EqualTo("u_Test"));
  }
}

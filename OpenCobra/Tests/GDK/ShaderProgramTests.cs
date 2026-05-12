using NUnit.Framework;
using OpenCobra.GDK.Shaders;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class ShaderProgramTests {
  [Test]
  public void ShaderProgramCreation_EmptySources() {
    var shader = new ShaderProgram();
    Assert.That(shader.VertexSource, Is.Empty);
    Assert.That(shader.FragmentSource, Is.Empty);
  }

  [Test]
  public void UniformBinding_CanAddAndRetrieve() {
    var shader = new ShaderProgram();
    var uniform = new Uniform { Name = "u_Test", Type = UniformType.Float, Value = 1.0f };
    shader.Uniforms.Add(uniform);
    Assert.That(shader.Uniforms, Has.Count.EqualTo(1));
    Assert.That(shader.Uniforms[0].Name, Is.EqualTo("u_Test"));
  }
}

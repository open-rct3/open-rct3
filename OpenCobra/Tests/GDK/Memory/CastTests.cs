using NUnit.Framework;
using OpenCobra.GDK.Memory;

namespace OpenCobra.Tests.GDK.Memory;

[TestFixture]
public class CastTests {
  [Test]
  public void To_WidensSignedToLargerSigned_PreservesValue() {
    // Regression test: this is exactly the int -> nint offset cast that Mesh.Upload passes to
    // glVertexAttribPointer. The old Unsafe.As-based implementation read 4 bytes of stack garbage past
    // the 4-byte int, so this returned garbage instead of 32 (see
    // .agents/bugs/terrain-render-black-and-misoriented.md).
    Assert.That(CastFrom<int>.To<nint>(32), Is.EqualTo((nint)32));
    Assert.That(CastFrom<int>.To<nint>(0), Is.EqualTo((nint)0));
    Assert.That(CastFrom<int>.To<long>(int.MaxValue), Is.EqualTo((long)int.MaxValue));
  }

  [Test]
  public void To_SameSizeSignedToUnsigned_PreservesValue() {
    // The int -> uint attribute-location cast Mesh.Upload also relies on.
    Assert.That(CastFrom<int>.To<uint>(0), Is.EqualTo(0u));
    Assert.That(CastFrom<int>.To<uint>(1), Is.EqualTo(1u));
    Assert.That(CastFrom<int>.To<uint>(42), Is.EqualTo(42u));
  }

  [Test]
  public void To_NarrowsToSmallerType_PreservesValueWhenInRange() {
    Assert.That(CastFrom<int>.To<short>(100), Is.EqualTo((short)100));
    Assert.That(CastFrom<long>.To<int>(12345), Is.EqualTo(12345));
  }

  [Test]
  public void To_NegativeToUnsigned_ThrowsInsteadOfWrapping() {
    // The old bit-reinterpretation would have silently produced a huge/wrapped uint. A "safe cast"
    // should fail loudly on a value that doesn't fit, not corrupt it.
    Assert.Throws<OverflowException>(new Action(() => CastFrom<int>.To<uint>(-1)));
  }

  [Test]
  public void To_OutOfRangeNarrowing_ThrowsInsteadOfTruncating() {
    Assert.Throws<OverflowException>(new Action(() => CastFrom<int>.To<short>(int.MaxValue)));
    Assert.Throws<OverflowException>(new Action(() => CastFrom<long>.To<int>(long.MaxValue)));
  }

  [Test]
  public void To_IdentityCast_PreservesValue() {
    Assert.That(CastFrom<int>.To<int>(7), Is.EqualTo(7));
  }
}

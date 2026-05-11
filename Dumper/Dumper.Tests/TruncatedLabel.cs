using Dumper.Plugins;

namespace Dumper.Tests;

/// <summary>
/// Test cases and validation for TruncatedLabel control using NUnit.
/// Run via NUnit test runner to verify truncation behavior across various scenarios.
/// </summary>
[TestFixture]
public class TruncatedLabelTests {
  private TruncatedLabel label;
  private Form testForm;

  [SetUp]
  public void Setup() {
    // Create an invisible form to host the label
    testForm = new Form { Visible = false };
    label = new TruncatedLabel {
      Width = 300,
      Height = 25,
      AutoSize = false,
      Font = new("Consolas", Control.DefaultFont.Size)
    };
    testForm.Controls.Add(label);
    testForm.Show();
  }

  [TearDown]
  public void TearDown() {
    testForm?.Dispose();
    label?.Dispose();
  }

  [Test]
  public void TestFilePath_PreservesFilenameAndRoot() {
    string original = "C:\\OpenRCT3\\Plugins\\Dumper\\ftx-viewer.wasm";

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain("ftx-viewer.wasm"), "Must preserve filename");
    Assert.That(result, Does.Contain("C:\\"), "Must preserve root");
  }

  [Test]
  public void TestURL_PreservesFilenameAndProtocol() {
    string original = "https://github.com/microsoft/vscode/releases/download/1.85.0/code-setup-x64-1.85.0.exe";

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain("code-setup-x64-1.85.0.exe"), "Must preserve filename");
    Assert.That(result, Does.Contain("https://"), "Must preserve protocol");
  }

  [Test]
  public void TestShortText_NoTruncation() {
    string original = "C:\\file.txt";

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain("file.txt"), "Short text should fit without modification");
  }

  [Test]
  public void TestPreserveRatio_HigherRatioPreservesMoreText() {
    string original = "C:\\Users\\Developer\\Projects\\MyApplication\\src\\Components\\Button.tsx";

    // Test at 0.2 ratio
    label.PreserveRatio = 0.2f;
    label.Text = original;
    string result1 = label.Text;

    // Test at 0.4 ratio
    label.PreserveRatio = 0.4f;
    label.Text = original;
    string result2 = label.Text;

    using (Assert.EnterMultipleScope()) {
      Assert.That(result2, Is.Not.Null);
      Assert.That(result2, Has.Length.GreaterThanOrEqualTo(result1.Length), "Higher ratio should preserve more text");
      Assert.That(result1, Does.Contain("Button.tsx"), "Must preserve filename at 0.2 ratio");
      Assert.That(result2, Does.Contain("Button.tsx"), "Must preserve filename at 0.4 ratio");
    }
  }

  [Test]
  public void TestSmartBreak_PreservesFilenameWithAndWithout() {
    string original = "C:\\Users\\Developer\\Projects\\MyApplication\\bin\\Release\\app.exe";

    // With SmartBreak enabled
    label.SmartBreak = true;
    label.Text = original;
    string resultSmart = label.Text;

    // With SmartBreak disabled
    label.SmartBreak = false;
    label.Text = original;
    string resultDumb = label.Text;

    using (Assert.EnterMultipleScope()) {
      Assert.That(resultSmart, Does.Contain("app.exe"), "Smart break must preserve filename");
      Assert.That(resultDumb, Does.Contain("app.exe"), "Dumb break must still preserve filename");
    }
  }

  [Test]
  public void TestEllipsisCustomization_StandardEllipsis() {
    string original = "C:\\OpenRCT3\\Plugins\\Dumper\\ftx-viewer.wasm";
    label.Ellipsis = "...";

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain("ftx-viewer.wasm").Or.Contain("..."),
        "Result must contain either filename or ellipsis");
  }

  [Test]
  public void TestEllipsisCustomization_UnicodeEllipsis() {
    string original = "C:\\OpenRCT3\\Plugins\\Dumper\\ftx-viewer.wasm";
    label.Ellipsis = "…";

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain("ftx-viewer.wasm").Or.Contain("…"),
        "Result must contain either filename or Unicode ellipsis");
  }

  [Test]
  public void TestEmptyString_RemainsEmpty() {
    label.Text = "";
    string result = label.Text;

    Assert.That(result, Is.EqualTo(""), "Empty string should remain empty");
  }

  [Test]
  public void TestVeryLongPath_PreservesFilename() {
    // Force truncation
    string original = "\\\\networkserver\\shared\\department\\subdepartment\\projects\\2024\\Q1\\marketing\\campaign\\assets\\final\\approved\\version3\\poster-design-final-FINAL-v3-revised-2024-01-15.psd";
    label.Width = 250;
    label.AutoSize = false;

    label.Text = original;
    string result = label.Text;

    using (Assert.EnterMultipleScope()) {
      Assert.That(result, Does.EndWith("poster-design-final-FINAL-v3-revised-2024-01-15.psd"), "Must preserve filename");
      Assert.That(result, Does.Contain("..."), "Must contain ellipsis when truncated");
      Assert.That(result, Has.Length.LessThan(original.Length), "Result must be shorter than original");
    }
  }

  [Test]
  public void TestPreserveRatio_BoundsChecking() {
    // Test minimum bound (0.1)
    label.PreserveRatio = 0.05f;
    Assert.That(label.PreserveRatio, Is.GreaterThanOrEqualTo(0.1f), "PreserveRatio should be clamped to minimum 0.1");

    // Test maximum bound (0.5)
    label.PreserveRatio = 0.8f;
    Assert.That(label.PreserveRatio, Is.LessThanOrEqualTo(0.5f), "PreserveRatio should be clamped to maximum 0.5");
  }

  [Test]
  [TestCase("C:\\file.txt")]
  [TestCase("/usr/local/bin/executable")]
  [TestCase("https://example.com/path/to/file.html")]
  public void TestVariousPaths_PreservesEndComponent(string path) {
    label.Width = 300;

    label.Text = path;
    string result = label.Text;

    using (Assert.EnterMultipleScope()) {
      Assert.That(result, Is.Not.Empty, "Result should not be empty");
      Assert.That(result, Does.EndWith(path[(path.LastIndexOf('/') > path.LastIndexOf('\\')
          ? path.LastIndexOf('/') + 1
          : Math.Max(0, path.LastIndexOf('\\') + 1))..]).Or.Contain("..."),
          "Result should preserve the final component or contain ellipsis");
    }
  }

  [Test]
  public void TestTextProperty_StoresOriginalValue() {
    string original = "C:\\Users\\Developer\\Projects\\MyApplication\\bin\\Release\\app.exe";

    label.Text = original;

    Assert.That(label.Text, Is.EqualTo(original), "Text property should return the original assigned value");
  }

  [Test]
  public void TestResize_RetriggersTruncation() {
    string original = "C:\\Users\\Developer\\Projects\\MyApplication\\bin\\Release\\app.exe";
    label.Text = original;
    label.Width = 300;
    string resultSmall = label.Text;

    label.Width = 800;
    string resultLarge = label.Text;

    Assert.That(resultLarge, Has.Length.GreaterThanOrEqualTo(resultSmall.Length),
        "Larger width should allow more text to display");
  }

  [Test]
  public void TestNullText_HandledGracefully() {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
    Assert.DoesNotThrow(() => label.Text = null);
    Assert.That(label.Text, Is.EqualTo(string.Empty), "Null text should be converted to empty string");
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
  }

  [Test]
  [TestCase("...")]
  [TestCase("…")]
  [TestCase(" >> ")]
  public void TestCustomEllipsis_AppliedCorrectly(string ellipsis) {
    string original = "C:\\OpenRCT3\\Plugins\\Dumper\\ftx-viewer.wasm";
    label.Ellipsis = ellipsis;

    label.Text = original;
    string result = label.Text;

    Assert.That(result, Does.Contain(ellipsis).Or.Contain("ftx-viewer.wasm"),
        $"Result should contain either custom ellipsis '{ellipsis}' or the filename");
  }
}

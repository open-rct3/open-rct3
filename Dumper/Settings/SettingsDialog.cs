using Dumper.Controls;
using Rop.Winforms8.DuotoneIcons;
using Rop.Winforms8.DuotoneIcons.MaterialDesign;

namespace Dumper.Settings;

public partial class SettingsDialog : Form {
  private readonly Properties.Settings memento;

  public SettingsDialog() {
    InitializeComponent();
    InitializeComponentIcons();

    Properties.Settings.Default.Reload();
    memento = Properties.Settings.Default;
    rct3Path.Text = memento.Rct3Dir;
  }

  private void InitializeComponentIcons() {
    IEmbeddedIcons icons = IconRepository.GetEmbeddedIcons<MaterialDesignIcons>();
    Icon = Icons.Render(icons, "Cog")?.ToIcon() ?? Icons.DefaultWindowIcon;
  }

  private void Rct3Path_TextChanged(object sender, EventArgs e) {
    var pathExists = Path.Exists(rct3Path.Text);
    if (!pathExists) {
      ShowPathError();
      applyBtn.Enabled = false;
      return;
    }

    applyBtn.Enabled = rct3Path.Text != memento.Rct3Dir;
  }

  private void BrowseBtn_Click(object sender, EventArgs e) {
    openFolder.InitialDirectory = memento.Rct3Dir ?? "";

    if (openFolder.ShowDialog() != DialogResult.OK) return;
    rct3Path.Text = openFolder.SelectedPath;
    applyBtn.Enabled = true;
  }

  private void OkayBtn_Click(object sender, EventArgs e) {
    ApplyBtn_Click(sender, e);
    DialogResult = DialogResult.OK;
    this.Close();
  }

  private void ApplyBtn_Click(object sender, EventArgs e) {
    var pathExists = Path.Exists(rct3Path.Text);
    if (!pathExists) {
      ShowPathError();
      return;
    }

    Properties.Settings.Default.Rct3Dir = rct3Path.Text;
    Properties.Settings.Default.Save();
  }

  private void CancelBtn_Click(object sender, EventArgs e) {
    DialogResult = DialogResult.Cancel;
    this.Close();
  }

  private void ShowPathError() => BalloonTip.Show(rct3Path.Handle, new BalloonTipContent {
    Title = "Path Not Found",
    Icon = BalloonIcon.Error,
    Text = "The path entered does not exist."
  });
}

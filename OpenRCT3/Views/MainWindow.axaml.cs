using Avalonia.Controls;

namespace OpenRCT3.Views;

public partial class MainWindow : Window {
  public MainWindow() {
    InitializeComponent();

    this.MinWidth = 640;
    this.MinHeight = 420;
  }
}
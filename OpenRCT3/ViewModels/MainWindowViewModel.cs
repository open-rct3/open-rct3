// MainWindowViewModel
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.ViewModels;

public partial class MainWindowViewModel : IViewModel {
#pragma warning disable CA1822 // Mark members as static
  public string Greeting => "Welcome to Avalonia!";
#pragma warning restore CA1822 // Mark members as static
}

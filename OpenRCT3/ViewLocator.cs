// ViewLocator
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpenRCT3.ViewModels;

namespace OpenRCT3;

public class ViewLocator : IDataTemplate {

  public Control? Build(object? data) {
    if (data is null)
      return null;

    var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
    var type = Type.GetType(name);

    if (type != null) {
      var control = (Control)Activator.CreateInstance(type)!;
      control.DataContext = data;
      return control;
    }

    return new TextBlock { Text = "Not Found: " + name };
  }

  public bool Match(object? data) {
    return data is ViewModelBase;
  }
}

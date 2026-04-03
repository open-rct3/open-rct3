// Inspector
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024 OpenRCT3 Contributors. All rights reserved.
using System;
using System.Collections.Generic;
using OVL.Files;

namespace OpenRCT3.ViewModels;

public class Inspector : IViewModel {
  public string Name { get; set; } = string.Empty;
  public string? FilePath { get; set; }
  public FileType? Type { get; set; }
  public IReadOnlyList<(string Key, string Value)> Properties { get; set; } = [];
}

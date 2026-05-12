// Settings
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Configuration;

namespace Dumper.Properties;

/// <summary>Application user settings.</summary>
/// <remarks>
/// <para>
///   Settings are saved in <i>user.config</i>, residing in the location specified by
///   <see cref="Application.LocalUserAppDataPath"/>.
/// </para>
/// <para>If a saved file does not exist, one is created in the following format:</para>
/// <para>
///   <i>Base Path</i> \
///   {<see cref="Application.CompanyName"/>} \
///   {<see cref="Application.ProductName"/>} \
///   {<see cref="Application.ProductVersion"/>} \
///   user.config
/// </para>
/// </remarks>
internal partial class Settings : ApplicationSettingsBase {
  /// <summary>
  /// The last OVL archive opened by the user, if any.
  /// </summary>
  [UserScopedSetting]
  public string? LastOvlOpened {
    get => this[nameof(LastOvlOpened)]?.ToString() ?? null;
    set => this[nameof(LastOvlOpened)] = value;
  }
  /// <summary>
  /// The last document exported by the user, if any.
  /// </summary>
  [UserScopedSetting]
  public string? LastDocumentExported {
    get => this[nameof(LastDocumentExported)]?.ToString() ?? null;
    set => this[nameof(LastDocumentExported)] = value;
  }
}

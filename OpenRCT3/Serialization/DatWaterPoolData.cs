// DatWaterPoolData
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;

namespace OpenRCT3.Serialization;

/// <summary>
/// One constant-height water pool decoded from an RCT3 DAT file.
/// </summary>
internal sealed class DatWaterPoolData {
  public float Height { get; }
  public IReadOnlyList<DatWaterRecord> Records { get; }

  public DatWaterPoolData(float height, DatWaterRecord[] records) {
    if (!float.IsFinite(height)) throw new ArgumentOutOfRangeException(nameof(height));
    ArgumentNullException.ThrowIfNull(records);

    Height = height;
    Records = Array.AsReadOnly((DatWaterRecord[])records.Clone());
  }
}

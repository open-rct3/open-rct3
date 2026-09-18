// DatWaterManagerData
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;

namespace OpenRCT3.Serialization;

/// <summary>
/// Water-grid metadata and pools decoded from an RCT3 DAT file.
/// </summary>
internal sealed class DatWaterManagerData {
  public int Width { get; }
  public int Height { get; }
  public IReadOnlyList<DatWaterPoolData> Pools { get; }

  public DatWaterManagerData(int width, int height, DatWaterPoolData[] pools) {
    if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
    ArgumentNullException.ThrowIfNull(pools);

    Width = width;
    Height = height;
    Pools = Array.AsReadOnly((DatWaterPoolData[])pools.Clone());
  }
}

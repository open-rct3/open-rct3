// UnownedAttribute
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK;

/// <summary>
/// Indicates that a field, property, or parameter is not owned by its containing type and should not be modified.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter)]
public class UnownedAttribute(string justification) : Attribute {
    // TODO: static assert: (justification != null && justification != string.Empty)
    public string Description { get; } = justification;
}

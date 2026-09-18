// TakesOwnershipAttribute
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK;

/// <summary>
/// Marks a constructor or method parameter of an <see cref="System.IDisposable"/> type as taking
/// ownership of the argument: the callee will dispose it, so the caller must not dispose it (or
/// let a `using` on it) independently. Enforced by the <c>GDK002</c>/<c>GDK003</c> analyzers in
/// <c>OpenCobra.Analyzers.DisposableOwnershipAnalyzer</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TakesOwnershipAttribute : Attribute;

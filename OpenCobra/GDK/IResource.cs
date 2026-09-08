// GPU-Resident Resource State
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK;

/// <summary>
/// Represents a GPU-resident resource.
/// </summary>
public interface IResource {
  State State { get; }
}

/// <summary>
/// State of a GPU-resident resource.
/// </summary>
public enum State : uint { Uninitialized = 0, Ready, Disposed }

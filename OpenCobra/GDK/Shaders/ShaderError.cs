// ShaderError
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System;

namespace OpenCobra.GDK.Shaders;

public class ShaderError(string message) : Exception(message);

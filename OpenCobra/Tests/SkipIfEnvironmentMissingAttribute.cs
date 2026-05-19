// ExtractResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DotNetEnv;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace OVL.Tests;

[AttributeUsage(AttributeTargets.Method)]
public class SkipIfEnvironmentMissingAttribute(string envVar, string reason) : NUnitAttribute, IApplyToTest {
  public void ApplyToTest(Test test) {
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar))) return;

    // Try to look up the environment from the nearest .env
    var envFile = Path.Exists(Constants.EnvFilePath) ? Constants.EnvFilePath : null;
    Env.NoClobber().Load(envFile);
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar))) return;

    // The required environment variable couldn't be found; skip this test verbosely
    test.RunState = RunState.Ignored;
    test.Properties.Set(PropertyNames.SkipReason, reason);
  }
}

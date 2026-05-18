// ExtractResources
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace OVL.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SkipIfEnvironmentMissingAttribute(
  string envVar, string reason = "Cannot find RCT3. Skipping test."
) : NUnitAttribute, IApplyToTest {
  public void ApplyToTest(Test test) {
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar))) {
      test.RunState = RunState.Ignored;
      test.Properties.Set(PropertyNames.SkipReason, reason);
    }
  }
}

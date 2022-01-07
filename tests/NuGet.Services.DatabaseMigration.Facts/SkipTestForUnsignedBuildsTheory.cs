// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Services.DatabaseMigration.Facts
{
    public class SkipTestForUnsignedBuildsTheory : TheoryAttribute
    {
        private static string BUILD_TYPE = "BuildType";

        public SkipTestForUnsignedBuildsTheory()
        {
            if (IsUnsignedBuild())
            {
                Skip = "This test needs signed builds to run (due to Project/package dual reference conflict), as such will always fail when running on CI/locally. Skip it until we fix the project references issue.";
            }
        }

        private static bool IsUnsignedBuild()
        {
            var buildTypeValue = Environment.GetEnvironmentVariable(BUILD_TYPE);
            return !string.IsNullOrWhiteSpace(buildTypeValue) 
                && buildTypeValue.Equals("unsigned", StringComparison.OrdinalIgnoreCase);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using BasicSearchTests.FunctionalTests.Core.Models;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Services.AzureSearch.FunctionalTests
{
    public static class TestUtilities
    {
        public static bool IsPrerelease(string original)
        {
            if (!NuGetVersion.TryParse(original, out var nugetVersion))
            {
                return false;
            }

            return nugetVersion.IsPrerelease;
        }

        /// <summary>
        /// Check for package version to be a SemVer2. This method only tests the version supplied.
        /// The package can still be SemVer2 if its dependency is SemVer2, however this test only tests for the 
        /// provided version.
        /// </summary>
        /// <param name="original">Version string</param>
        /// <returns>True if the provided string is SemVer2, false otherwise</returns>
        public static bool IsSemVer2(string original)
        {
            if (!NuGetVersion.TryParse(original, out var nugetVersion))
            {
                return false;
            }

            return nugetVersion.IsSemVer2;
        }
    }
}

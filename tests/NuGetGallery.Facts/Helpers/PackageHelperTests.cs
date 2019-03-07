﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.Helpers
{
    public class PackageHelperTests
    {
        [Theory]
        [InlineData("http://nuget.org", false, true)]
        [InlineData("http://nuget.org", true, false)]
        [InlineData("https://nuget.org", false, true)]
        [InlineData("https://nuget.org", true, true)]
        [InlineData("git://nuget.org", true, false)]
        [InlineData("git://nuget.org", false, false)]
        [InlineData("not a url", false, false)]
        public void ShouldRenderUrlTests(string url, bool secureOnly, bool shouldRender)
        {
            Assert.Equal(shouldRender, PackageHelper.ShouldRenderUrl(url, secureOnly: secureOnly));
        }

        public class TheGetSelectListTextMethod
        {
            public const string Version = "1.0.1+build";

            [Theory]
            [InlineData(false, false, false, false, Version)]
            [InlineData(false, false, false, true, Version + " (Deprecated - Other)")]
            [InlineData(false, false, true, false, Version + " (Deprecated - Legacy)")]
            [InlineData(false, false, true, true, Version + " (Deprecated - Legacy, Other)")]
            [InlineData(false, true, false, false, Version + " (Deprecated - Vulnerable)")]
            [InlineData(false, true, false, true, Version + " (Deprecated - Vulnerable, Other)")]
            [InlineData(false, true, true, false, Version + " (Deprecated - Vulnerable, Legacy)")]
            [InlineData(false, true, true, true, Version + " (Deprecated - Vulnerable, Legacy, Other)")]
            [InlineData(true, false, false, false, Version + " (Latest)")]
            [InlineData(true, false, false, true, Version + " (Latest, Deprecated - Other)")]
            [InlineData(true, false, true, false, Version + " (Latest, Deprecated - Legacy)")]
            [InlineData(true, false, true, true, Version + " (Latest, Deprecated - Legacy, Other)")]
            [InlineData(true, true, false, false, Version + " (Latest, Deprecated - Vulnerable)")]
            [InlineData(true, true, false, true, Version + " (Latest, Deprecated - Vulnerable, Other)")]
            [InlineData(true, true, true, false, Version + " (Latest, Deprecated - Vulnerable, Legacy)")]
            [InlineData(true, true, true, true, Version + " (Latest, Deprecated - Vulnerable, Legacy, Other)")]
            public void ReturnsCorrectSelectListText(bool latest, bool isVulnerable, bool isLegacy, bool isOther, string expected)
            {
                var package = new Package
                {
                    Version = Version,
                    IsLatestSemVer2 = latest
                };

                if (isVulnerable || isLegacy || isOther)
                {
                    var status = PackageDeprecationStatus.NotDeprecated;
                    if (isVulnerable)
                    {
                        status = status | PackageDeprecationStatus.Vulnerable;
                    }

                    if (isLegacy)
                    {
                        status = status | PackageDeprecationStatus.Legacy;
                    }

                    if (isOther)
                    {
                        status = status | PackageDeprecationStatus.Other;
                    }

                    var deprecation = new PackageDeprecation
                    {
                        Status = status
                    };

                    package.Deprecations.Add(deprecation);
                }

                Assert.Equal(expected, PackageHelper.GetSelectListText(package));
            }
        }
    }
}

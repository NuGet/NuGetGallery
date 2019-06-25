// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;
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
            [InlineData(false, true, false, false, Version + " (Deprecated - Legacy)")]
            [InlineData(false, true, false, true, Version + " (Deprecated - Legacy, Other)")]
            [InlineData(false, false, true, false, Version + " (Deprecated - Critical Bugs)")]
            [InlineData(false, false, true, true, Version + " (Deprecated - Critical Bugs, Other)")]
            [InlineData(false, true, true, false, Version + " (Deprecated - Legacy, Critical Bugs)")]
            [InlineData(false, true, true, true, Version + " (Deprecated - Legacy, Critical Bugs, Other)")]
            [InlineData(true, false, false, false, Version + " (Latest)")]
            [InlineData(true, false, false, true, Version + " (Latest, Deprecated - Other)")]
            [InlineData(true, true, false, false, Version + " (Latest, Deprecated - Legacy)")]
            [InlineData(true, true, false, true, Version + " (Latest, Deprecated - Legacy, Other)")]
            [InlineData(true, false, true, false, Version + " (Latest, Deprecated - Critical Bugs)")]
            [InlineData(true, false, true, true, Version + " (Latest, Deprecated - Critical Bugs, Other)")]
            [InlineData(true, true, true, false, Version + " (Latest, Deprecated - Legacy, Critical Bugs)")]
            [InlineData(true, true, true, true, Version + " (Latest, Deprecated - Legacy, Critical Bugs, Other)")]
            public void ReturnsCorrectSelectListText(bool latest, bool isLegacy, bool hasCriticalBugs, bool isOther, string expected)
            {
                var package = new Package
                {
                    Version = Version,
                    IsLatestSemVer2 = latest
                };

                if (isLegacy || hasCriticalBugs || isOther)
                {
                    var status = PackageDeprecationStatus.NotDeprecated;

                    if (isLegacy)
                    {
                        status |= PackageDeprecationStatus.Legacy;
                    }

                    if (hasCriticalBugs)
                    {
                        status |= PackageDeprecationStatus.CriticalBugs;
                    }

                    if (isOther)
                    {
                        status |= PackageDeprecationStatus.Other;
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

        public class TheValidateNuGetPackageMetadataMethod
        {
            [Fact]
            public void ChecksIdVersionCombinedLength()
            {
                var metadata = new PackageMetadata(
                    new Dictionary<string, string>
                    {
                        { "id", "someidthatis128characterslong.padding.padding.padding.padding.padding.padding.padding.padding.padding.padding.padding.padding.a" },
                        { "version", "1.2.3-versionthatis64characterslong-padding-padding-padding-pad" },
                        { "description", "test description" }
                    },
                    Enumerable.Empty<PackageDependencyGroup>(),
                    Enumerable.Empty<FrameworkSpecificGroup>(),
                    Enumerable.Empty<NuGet.Packaging.Core.PackageType>(),
                    minClientVersion: null,
                    repositoryMetadata: null);

                var ex = Assert.Throws<EntityException>(() => PackageHelper.ValidateNuGetPackageMetadata(metadata));
                Assert.Contains("ID and version", ex.Message);
            }
        }
    }
}

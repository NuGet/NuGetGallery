// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGet.Versioning;
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

        [Theory]
        [InlineData("http://nuget.org/", false, "https://nuget.org/", true)]
        [InlineData("http://nuget.org/", true, "https://nuget.org/", true)]
        [InlineData("https://nuget.org/", false, "https://nuget.org/", true)]
        [InlineData("https://nuget.org/", true, "https://nuget.org/", true)]
        [InlineData("http://nugettest.org/", false, "https://nugettest.org/", true)]
        [InlineData("http://nugettest.org/", true, "https://nugettest.org/", true)]
        [InlineData("https://nugettest.org/", false, "https://nugettest.org/", true)]
        [InlineData("https://nugettest.org/", true, "https://nugettest.org/", true)]
        [InlineData("http://www.github.com/", false, "https://www.github.com/", true)]
        [InlineData("http://www.github.com/", true, "https://www.github.com/", true)]
        [InlineData("https://www.github.com/", false, "https://www.github.com/", true)]
        [InlineData("https://www.github.com/", true, "https://www.github.com/", true)]
        [InlineData("http://fake.github.com/", false, "https://fake.github.com/", true)]
        [InlineData("http://fake.github.com/", true, "https://fake.github.com/", true)]
        [InlineData("https://fake.github.com/", false, "https://fake.github.com/", true)]
        [InlineData("https://fake.github.com/", true, "https://fake.github.com/", true)]
        [InlineData("http://github.com/", false, "https://github.com/", true)]
        [InlineData("http://github.com/", true, "https://github.com/", true)]
        [InlineData("https://github.com/", false, "https://github.com/", true)]
        [InlineData("https://github.com/", true, "https://github.com/", true)]
        [InlineData("http://fake.github.io/", false, "https://fake.github.io/", true)]
        [InlineData("http://fake.github.io/", true, "https://fake.github.io/", true)]
        [InlineData("https://fake.github.io/", false, "https://fake.github.io/", true)]
        [InlineData("https://fake.github.io/", true, "https://fake.github.io/", true)]
        [InlineData("http://codeplex.com/", false, "https://codeplex.com/", true)]
        [InlineData("http://codeplex.com/", true, "https://codeplex.com/", true)]
        [InlineData("https://codeplex.com/", false, "https://codeplex.com/", true)]
        [InlineData("https://codeplex.com/", true, "https://codeplex.com/", true)]
        [InlineData("http://microsoft.com/", false, "https://microsoft.com/", true)]
        [InlineData("http://microsoft.com/", true, "https://microsoft.com/", true)]
        [InlineData("https://microsoft.com/", false, "https://microsoft.com/", true)]
        [InlineData("https://microsoft.com/", true, "https://microsoft.com/", true)]
        [InlineData("http://asp.net/", false, "https://asp.net/", true)]
        [InlineData("http://asp.net/", true, "https://asp.net/", true)]
        [InlineData("https://asp.net/", false, "https://asp.net/", true)]
        [InlineData("https://asp.net/", true, "https://asp.net/", true)]
        [InlineData("http://msdn.com/", false, "https://msdn.com/", true)]
        [InlineData("http://msdn.com/", true, "https://msdn.com/", true)]
        [InlineData("https://msdn.com/", false, "https://msdn.com/", true)]
        [InlineData("https://msdn.com/", true, "https://msdn.com/", true)]
        [InlineData("http://aka.ms/", false, "https://aka.ms/", true)]
        [InlineData("http://aka.ms/", true, "https://aka.ms/", true)]
        [InlineData("https://aka.ms/", false, "https://aka.ms/", true)]
        [InlineData("https://aka.ms/", true, "https://aka.ms/", true)]
        [InlineData("http://www.mono-project.com/", false, "https://www.mono-project.com/", true)]
        [InlineData("http://www.mono-project.com/", true, "https://www.mono-project.com/", true)]
        [InlineData("https://www.mono-project.com/", false, "https://www.mono-project.com/", true)]
        [InlineData("https://www.mono-project.com/", true, "https://www.mono-project.com/", true)]
        [InlineData("http://www.odata.org/", false, "https://www.odata.org/", true)]
        [InlineData("http://www.odata.org/", true, "https://www.odata.org/", true)]
        [InlineData("https://www.odata.org/", false, "https://www.odata.org/", true)]
        [InlineData("https://www.odata.org/", true, "https://www.odata.org/", true)]
        [InlineData("git://nuget.org", true, null, false)]
        [InlineData("git://nuget.org", false, null, false)]
        public void PrepareUrlForRenderingTest(string input, bool alwaysRewriteHttp, string expectedOutput, bool expectConversion)
        {
            Assert.Equal(expectConversion, PackageHelper.TryPrepareUrlForRendering(input, out string readyUriString, alwaysRewriteHttp));
            Assert.Equal(expectedOutput, readyUriString);
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

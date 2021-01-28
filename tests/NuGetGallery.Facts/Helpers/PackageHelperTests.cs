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

        [Theory]
        [InlineData("https://api.bintray.com/example/image.svg", true, "https://api.bintray.com/example/image.svg", true)]
        [InlineData("https://api.bintray.com/example/image.svg", false, "https://api.bintray.com/example/image.svg", true)]
        [InlineData("http://api.bintray.com/example/image.svg", true, "https://api.bintray.com/example/image.svg", true)]
        [InlineData("http://api.bintray.com/example/image.svg", false, null, false)]
        [InlineData("https://api.codacy.com/project/badge/Grade/image.svg", true, "https://api.codacy.com/project/badge/Grade/image.svg", true)]
        [InlineData("https://api.codacy.com/project/badge/Grade/image.svg", false, "https://api.codacy.com/project/badge/Grade/image.svg", true)]
        [InlineData("http://api.codacy.com/project/badge/Grade/image.svg", true, "https://api.codacy.com/project/badge/Grade/image.svg", true)]
        [InlineData("http://api.codacy.com/project/badge/Grade/image.svg", false, null, false)]
        [InlineData("https://api.travis-ci.com/example/image.svg", true, "https://api.travis-ci.com/example/image.svg", true)]
        [InlineData("https://api.travis-ci.com/example/image.svg", false, "https://api.travis-ci.com/example/image.svg", true)]
        [InlineData("http://api.travis-ci.com/example/image.svg", true, "https://api.travis-ci.com/example/image.svg", true)]
        [InlineData("http://api.travis-ci.com/example/image.svg", false, null, false)]
        [InlineData("https://app.fossa.io/example/image.svg", true, "https://app.fossa.io/example/image.svg", true)]
        [InlineData("https://app.fossa.io/example/image.svg", false, "https://app.fossa.io/example/image.svg", true)]
        [InlineData("http://app.fossa.io/example/image.svg", true, "https://app.fossa.io/example/image.svg", true)]
        [InlineData("http://app.fossa.io/example/image.svg", false, null, false)]
        [InlineData("https://badge.fury.io/example/image.svg", true, "https://badge.fury.io/example/image.svg", true)]
        [InlineData("https://badge.fury.io/example/image.svg", false, "https://badge.fury.io/example/image.svg", true)]
        [InlineData("http://badge.fury.io/example/image.svg", true, "https://badge.fury.io/example/image.svg", true)]
        [InlineData("http://badge.fury.io/example/image.svg", false, null, false)]
        [InlineData("https://badgen.net/example/image.svg", true, "https://badgen.net/example/image.svg", true)]
        [InlineData("https://badgen.net/example/image.svg", false, "https://badgen.net/example/image.svg", true)]
        [InlineData("http://badgen.net/example/image.svg", true, "https://badgen.net/example/image.svg", true)]
        [InlineData("http://badgen.net/example/image.svg", false, null, false)]
        [InlineData("https://badges.frapsoft.com/example/image.svg", true, null, false)]
        [InlineData("http://badges.frapsoft.com/example/image.svg", false, null, false)]
        [InlineData("https://badges.gitter.im/example/image.svg", true, "https://badges.gitter.im/example/image.svg", true)]
        [InlineData("https://badges.gitter.im/example/image.svg", false, "https://badges.gitter.im/example/image.svg", true)]
        [InlineData("http://badges.gitter.im/example/image.svg", true, "https://badges.gitter.im/example/image.svg", true)]
        [InlineData("http://badges.gitter.im/example/image.svg", false, null, false)]
        [InlineData("https://bettercodehub.com/example/image.svg", true, "https://bettercodehub.com/example/image.svg", true)]
        [InlineData("https://bettercodehub.com/example/image.svg", false, "https://bettercodehub.com/example/image.svg", true)]
        [InlineData("http://bettercodehub.com/example/image.svg", true, "https://bettercodehub.com/example/image.svg", true)]
        [InlineData("http://bettercodehub.com/example/image.svg", false, null, false)]
        [InlineData("https://build.appcenter.ms/example/image.svg", true, null, false)]
        [InlineData("https://build.appcenter.ms/example/image.svg", false, null, false)]
        [InlineData("https://buildstats.info/example/image.svg", true, "https://buildstats.info/example/image.svg", true)]
        [InlineData("https://buildstats.info/example/image.svg", false, "https://buildstats.info/example/image.svg", true)]
        [InlineData("http://buildstats.info/example/image.svg", true, "https://buildstats.info/example/image.svg", true)]
        [InlineData("http://buildstats.info/example/image.svg", false, null, false)]
        [InlineData("https://ci.appveyor.com/example/image.svg", true, "https://ci.appveyor.com/example/image.svg", true)]
        [InlineData("https://ci.appveyor.com/example/image.svg", false, "https://ci.appveyor.com/example/image.svg", true)]
        [InlineData("http://ci.appveyor.com/example/image.svg", true, "https://ci.appveyor.com/example/image.svg", true)]
        [InlineData("http://ci.appveyor.com/example/image.svg", false, null, false)]
        [InlineData("https://circleci.com/example/image.svg", true, "https://circleci.com/example/image.svg", true)]
        [InlineData("https://circleci.com/example/image.svg", false, "https://circleci.com/example/image.svg", true)]
        [InlineData("http://circleci.com/example/image.svg", true, "https://circleci.com/example/image.svg", true)]
        [InlineData("http://circleci.com/example/image.svg", false, null, false)]
        [InlineData("http://codeclimate.com/example/image.svg", true, null, false)]
        [InlineData("http://codeclimate.com/example/image.svg", false, null, false)]
        [InlineData("https://codecov.io/example/image.svg", true, "https://codecov.io/example/image.svg", true)]
        [InlineData("https://codecov.io/example/image.svg", false, "https://codecov.io/example/image.svg", true)]
        [InlineData("http://codecov.io/example/image.svg", true, "https://codecov.io/example/image.svg", true)]
        [InlineData("http://codecov.io/example/image.svg", false, null, false)]
        [InlineData("https://codefactor.io/example/image.svg", true, "https://codefactor.io/example/image.svg", true)]
        [InlineData("https://codefactor.io/example/image.svg", false, "https://codefactor.io/example/image.svg", true)]
        [InlineData("http://codefactor.io/example/image.svg", true, "https://codefactor.io/example/image.svg", true)]
        [InlineData("http://codefactor.io/example/image.svg", false, null, false)]
        [InlineData("https://coveralls.io/example/image.svg", true, "https://coveralls.io/example/image.svg", true)]
        [InlineData("https://coveralls.io/example/image.svg", false, "https://coveralls.io/example/image.svg", true)]
        [InlineData("http://coveralls.io/example/image.svg", true, "https://coveralls.io/example/image.svg", true)]
        [InlineData("http://coveralls.io/example/image.svg", false, null, false)]
        [InlineData("https://dev.azure.com/example/image.svg", true, "https://dev.azure.com/example/image.svg", true)]
        [InlineData("https://dev.azure.com/example/image.svg", false, "https://dev.azure.com/example/image.svg", true)]
        [InlineData("http://dev.azure.com/example/image.svg", true, "https://dev.azure.com/example/image.svg", true)]
        [InlineData("http://dev.azure.com/example/image.svg", false, null, false)]
        [InlineData("https://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", true, "https://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", true)]
        [InlineData("https://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", false, "https://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", true)]
        [InlineData("http://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", true, "https://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", true)]
        [InlineData("http://github.com/cedx/where.dart/workflows/.github/workflows/ci.yaml/badge.svg?branch=feature-1", false, null, false)]
        [InlineData("https://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", true, "https://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", true)]
        [InlineData("https://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", false, "https://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", true)]
        [InlineData("http://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", true, "https://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", true)]
        [InlineData("http://github.com/cedx/where.dart/workflows/Continuous%20integration/badge.svg", false, null, false)]
        [InlineData("https://githuB.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", true, "https://github.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", true)]
        [InlineData("https://githuB.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", false, "https://github.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", true)]
        [InlineData("http://githuB.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", true, "https://github.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", true)]
        [InlineData("http://githuB.com/peaceiris/actions-gh-pages/workflows/.github/workflows/docker-image-ci.yml/badge.svg?event=pull_request", false, null, false)]
        [InlineData("https://Github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", true, "https://github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", true)]
        [InlineData("https://Github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", false, "https://github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", true)]
        [InlineData("http://Github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", true, "https://github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", true)]
        [InlineData("http://Github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/badge.svg", false, null, false)]
        [InlineData("https://github.com/4lejandrito/creepyface/actions?query=workflow%3ABuild+branch%3Amaster.svg", true, null, false)]
        [InlineData("https://github.com/peaceiris/actions-gh-pages/WORKFLOWS/.github/WORKFLOWS/docker-image-ci.yml/badge.svg", true, null, false)]
        [InlineData("https://githubb.com/peaceiris/actions-gh-pages/workflows/.github/WORKFLOWS/docker-image-ci.yml/badge.svg", true, null, false)]
        [InlineData("https://github.com/peaceiris/actions-gh-pages/workflows/.GIT/workflows/docker-image-ci.yml/BADGE.svg", true, null, false)]
        [InlineData("https://git@github.com/peaceiris/actions-gh-pages/workflows/docker-image-ci/something/badge.svg", true, null, false)]
        [InlineData("https://github.com/peaceiris/actions-gh-pages/workFLOWS/.github/workflows/docker-image-ci.yml/badge.SVG", true, null, false)]
        [InlineData("https://github.com/nuget/NuGetGallery/workflows/something/blank.svg", true, null, false)]
        [InlineData("https://gitlab.com/nuget/NuGetGallery/workflows/something/badges.svg", true, "https://gitlab.com/nuget/NuGetGallery/workflows/something/badges.svg", true)]
        [InlineData("https://skyapmtest.github.io/page-resources/SkyAPM/skyapm.png", true, null, false)]
        [InlineData("https://user-images.githubusercontent.com/page-resources/SkyAPM/skyapm.png", true, "https://user-images.githubusercontent.com/page-resources/SkyAPM/skyapm.png", true)]
        [InlineData("https://raw.github.com/page-resources/SkyAPM/skyapm.png", true, "https://raw.github.com/page-resources/SkyAPM/skyapm.png", true)]
        [InlineData("https://img.shields.io/example/image.svg", true, "https://img.shields.io/example/image.svg", true)]
        [InlineData("https://isitmaintained.com/example/image.svg", true, "https://isitmaintained.com/example/image.svg", true)]
        [InlineData("https://opencollective.com/example/image.svg", true, "https://opencollective.com/example/image.svg", true)]
        [InlineData("https://snyk.io/example/image.svg", true, "https://snyk.io/example/image.svg", true)]
        [InlineData("https://sonarcloud.io/example/image.svg", true, "https://sonarcloud.io/example/image.svg", true)]
        [InlineData("https://travis-ci.com/example/image.svg", true, null, false)]
        [InlineData("https://feeds.dev.azure.com/example/image.svg", true, null, false)]
        [InlineData("https://circleci.com/gh/CptWesley/Nhahaha.svg?style=shield", true, "https://circleci.com/gh/CptWesley/Nhahaha.svg?style=shield", true)]
        [InlineData("https://travis-ci.org/Azure/azure-relay-aspnetserver.svg?branch=dev", true, null, false)]
        [InlineData("https://travis-ci.org/Azure/azure-relay-aspnetserver.svg?branch=dev", false, null, false)]
        [InlineData("http://nuget.org/", true, null, false)]
        [InlineData("https://raw.github.com/image", true, "https://raw.github.com/image", true)]
        [InlineData("https://user-images.githubusercontent.com/image", true, "https://user-images.githubusercontent.com/image", true)]
        [InlineData("https://camo.githubusercontent.com/image", true, "https://camo.githubusercontent.com/image", true)]
        public void TryPrepareImageUrlForRendering(string input, bool alwaysRewriteHttp, string expectedOutput, bool expectConversion)
        {
            Assert.Equal(expectConversion, PackageHelper.TryPrepareImageUrlForRendering(input, out string readyUriString, alwaysRewriteHttp));
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

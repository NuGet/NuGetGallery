// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Jobs.Catalog2Registration
{
    public class RegistrationUrlBuilderFacts
    {
        public class GetIndexPath : Facts
        {
            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetIndexPath("测试更新包");

                Assert.Equal("%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/index.json", path);
            }

            [Fact]
            public void LowercasesId()
            {
                var path = Target.GetIndexPath("NuGet.Versioning");

                Assert.Equal("nuget.versioning/index.json", path);
            }
        }

        public class GetIndexUrl : Facts
        {
            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetIndexUrl(HiveType.Legacy, "测试更新包");

                Assert.Equal("https://example/v3-reg/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/index.json", path);
            }

            [Fact]
            public void LowercasesId()
            {
                Config.LegacyBaseUrl = "https://example/v3-REG/";

                var path = Target.GetIndexUrl(HiveType.Legacy, "NuGet.Versioning");

                Assert.Equal("https://example/v3-REG/nuget.versioning/index.json", path);
            }

            [Theory]
            [MemberData(nameof(HiveTestData))]
            public void HandlesAllBaseUrls(HiveType hive)
            {
                var baseUrl = GetBaseUrl(hive);

                var path = Target.GetIndexUrl(hive, "NuGet.Versioning");

                Assert.Equal(baseUrl + "nuget.versioning/index.json", path);
            }
        }

        public class GetInlinedPageUrl : Facts
        {
            public GetInlinedPageUrl()
            {
                Lower = NuGetVersion.Parse("1.0.0");
                Upper = NuGetVersion.Parse("2.0.0");
            }

            public NuGetVersion Lower { get; set; }
            public NuGetVersion Upper { get; set; }

            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetInlinedPageUrl(HiveType.Legacy, "测试更新包", Lower, Upper);

                Assert.Equal("https://example/v3-reg/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/index.json#page/1.0.0/2.0.0", path);
            }

            [Fact]
            public void LowercasesIdAndVersions()
            {
                Lower = NuGetVersion.Parse("1.0.0-BETA");
                Config.LegacyBaseUrl = "https://example/v3-REG/";

                var path = Target.GetInlinedPageUrl(HiveType.Legacy, "NuGet.Versioning", Lower, Upper);

                Assert.Equal("https://example/v3-REG/nuget.versioning/index.json#page/1.0.0-beta/2.0.0", path);
            }

            [Fact]
            public void UsesNormalizedVersion()
            {
                Lower = NuGetVersion.Parse("1.0.01.0-BETA.1+git");

                var path = Target.GetInlinedPageUrl(HiveType.Legacy, "NuGet.Versioning", Lower, Upper);

                Assert.Equal("https://example/v3-reg/nuget.versioning/index.json#page/1.0.1-beta.1/2.0.0", path);
            }

            [Theory]
            [MemberData(nameof(HiveTestData))]
            public void HandlesAllBaseUrls(HiveType hive)
            {
                var baseUrl = GetBaseUrl(hive);

                var path = Target.GetInlinedPageUrl(hive, "NuGet.Versioning", Lower, Upper);

                Assert.Equal(baseUrl + "nuget.versioning/index.json#page/1.0.0/2.0.0", path);
            }
        }

        public class ConvertHive : Facts
        {
            [Fact]
            public void RejectsMismatchingBaseUrl()
            {
                var url = "https://example/v3-reg/nuget.versioning/index.json";

                var ex = Assert.Throws<InvalidOperationException>(
                    () => Target.ConvertHive(HiveType.Gzipped, HiveType.SemVer2, url));
                Assert.Equal($"URL '{url}' does not start with expected base URL 'https://example/v3-reg-gz/'.", ex.Message);
            }

            [Fact]
            public void ConvertsToSameHive()
            {
                var url = "https://example/v3-reg/nuget.versioning/index.json";

                var converted = Target.ConvertHive(HiveType.Legacy, HiveType.Legacy, url);

                Assert.Equal(url, converted);
            }

            [Fact]
            public void ConvertsToDifferentHive()
            {
                var url = "https://example/v3-reg/nuget.versioning/index.json";
                var expected = "https://example/v3-reg-gz/nuget.versioning/index.json";

                var converted = Target.ConvertHive(HiveType.Legacy, HiveType.Gzipped, url);

                Assert.Equal(expected, converted);
            }
        }

        public class ConvertToPath : Facts
        {
            [Fact]
            public void RejectsMismatchingBaseUrl()
            {
                var url = "https://example/v3-reg/nuget.versioning/index.json";

                var ex = Assert.Throws<InvalidOperationException>(
                    () => Target.ConvertToPath(HiveType.Gzipped, url));
                Assert.Equal($"URL '{url}' does not start with expected base URL 'https://example/v3-reg-gz/'.", ex.Message);
            }

            [Fact]
            public void ConvertsToPath()
            {
                var expected = "nuget.versioning/index.json";
                var url = "https://example/v3-reg/" + expected;
                
                var converted = Target.ConvertToPath(HiveType.Legacy, url);

                Assert.Equal(expected, converted);
            }
        }

        public class GetPageUrl : Facts
        {
            public GetPageUrl()
            {
                Lower = NuGetVersion.Parse("1.0.0");
                Upper = NuGetVersion.Parse("2.0.0");
            }

            public NuGetVersion Lower { get; set; }
            public NuGetVersion Upper { get; set; }

            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetPageUrl(HiveType.Legacy, "测试更新包", Lower, Upper);

                Assert.Equal("https://example/v3-reg/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/page/1.0.0/2.0.0.json", path);
            }

            [Fact]
            public void LowercasesIdAndVersions()
            {
                Lower = NuGetVersion.Parse("1.0.0-BETA");
                Config.LegacyBaseUrl = "https://example/v3-REG/";

                var path = Target.GetPageUrl(HiveType.Legacy, "NuGet.Versioning", Lower, Upper);

                Assert.Equal("https://example/v3-REG/nuget.versioning/page/1.0.0-beta/2.0.0.json", path);
            }

            [Fact]
            public void UsesNormalizedVersion()
            {
                Lower = NuGetVersion.Parse("1.0.01.0-BETA.1+git");

                var path = Target.GetPageUrl(HiveType.Legacy, "NuGet.Versioning", Lower, Upper);

                Assert.Equal("https://example/v3-reg/nuget.versioning/page/1.0.1-beta.1/2.0.0.json", path);
            }

            [Theory]
            [MemberData(nameof(HiveTestData))]
            public void HandlesAllBaseUrls(HiveType hive)
            {
                var baseUrl = GetBaseUrl(hive);

                var path = Target.GetPageUrl(hive, "NuGet.Versioning", Lower, Upper);

                Assert.Equal(baseUrl + "nuget.versioning/page/1.0.0/2.0.0.json", path);
            }
        }

        public class GetPagePath : Facts
        {
            public GetPagePath()
            {
                Lower = NuGetVersion.Parse("1.0.0");
                Upper = NuGetVersion.Parse("2.0.0");
            }

            public NuGetVersion Lower { get; set; }
            public NuGetVersion Upper { get; set; }

            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetPagePath("测试更新包", Lower, Upper);

                Assert.Equal("%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/page/1.0.0/2.0.0.json", path);
            }

            [Fact]
            public void LowercasesIdAndVersions()
            {
                Lower = NuGetVersion.Parse("1.0.0-BETA");

                var path = Target.GetPagePath("NuGet.Versioning", Lower, Upper);

                Assert.Equal("nuget.versioning/page/1.0.0-beta/2.0.0.json", path);
            }

            [Fact]
            public void UsesNormalizedVersion()
            {
                Lower = NuGetVersion.Parse("1.0.01.0-BETA.1+git");

                var path = Target.GetPagePath("NuGet.Versioning", Lower, Upper);

                Assert.Equal("nuget.versioning/page/1.0.1-beta.1/2.0.0.json", path);
            }
        }

        public class GetLeafPath : Facts
        {
            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetLeafPath("测试更新包", NuGetVersion.Parse("1.0.0"));

                Assert.Equal("%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/1.0.0.json", path);
            }

            [Fact]
            public void LowercasesIdAndVersions()
            {
                var path = Target.GetLeafPath("NuGet.Versioning", NuGetVersion.Parse("1.0.0-BETA"));

                Assert.Equal("nuget.versioning/1.0.0-beta.json", path);
            }

            [Fact]
            public void UsesNormalizedVersion()
            {
                var path = Target.GetLeafPath("NuGet.Versioning", NuGetVersion.Parse("1.0.01.0-BETA.1+git"));

                Assert.Equal("nuget.versioning/1.0.1-beta.1.json", path);
            }
        }

        public class GetLeafUrl : Facts
        {
            [Fact]
            public void EncodesUnsafeCharacters()
            {
                var path = Target.GetLeafUrl(HiveType.Legacy, "测试更新包", NuGetVersion.Parse("1.0.0"));

                Assert.Equal("https://example/v3-reg/%E6%B5%8B%E8%AF%95%E6%9B%B4%E6%96%B0%E5%8C%85/1.0.0.json", path);
            }

            [Fact]
            public void LowercasesIdAndVersions()
            {
                Config.LegacyBaseUrl = "https://example/v3-REG/";

                var path = Target.GetLeafUrl(HiveType.Legacy, "NuGet.Versioning", NuGetVersion.Parse("1.0.0-BETA"));

                Assert.Equal("https://example/v3-REG/nuget.versioning/1.0.0-beta.json", path);
            }

            [Fact]
            public void UsesNormalizedVersion()
            {
                var path = Target.GetLeafUrl(HiveType.Legacy, "NuGet.Versioning", NuGetVersion.Parse("1.0.01.0-BETA.1+git"));

                Assert.Equal("https://example/v3-reg/nuget.versioning/1.0.1-beta.1.json", path);
            }

            [Theory]
            [MemberData(nameof(HiveTestData))]
            public void HandlesAllBaseUrls(HiveType hive)
            {
                var baseUrl = GetBaseUrl(hive);

                var path = Target.GetLeafUrl(hive, "NuGet.Versioning", NuGetVersion.Parse("1.0.0"));

                Assert.Equal(baseUrl + "nuget.versioning/1.0.0.json", path);
            }
        }

        public abstract class Facts
        {
            public Facts()
            {
                Options = new Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                Config = new Catalog2RegistrationConfiguration
                {
                    LegacyBaseUrl = "https://example/v3-reg/",
                    GzippedBaseUrl = "https://example/v3-reg-gz/",
                    SemVer2BaseUrl = "https://example/v3-reg-gz-semver2/",
                };
                Options.Setup(x => x.Value).Returns(() => Config);
            }

            public Mock<IOptionsSnapshot<Catalog2RegistrationConfiguration>> Options { get; }
            public Catalog2RegistrationConfiguration Config { get; }
            public RegistrationUrlBuilder Target => new RegistrationUrlBuilder(Options.Object);

            public string GetBaseUrl(HiveType hive)
            {
                switch (hive)
                {
                    case HiveType.Legacy:
                        return Config.LegacyBaseUrl;
                    case HiveType.Gzipped:
                        return Config.GzippedBaseUrl;
                    case HiveType.SemVer2:
                        return Config.SemVer2BaseUrl;
                    default:
                        throw new NotImplementedException();
                }
            }

            public static IEnumerable<object[]> HiveTestData => Enum
                .GetValues(typeof(HiveType))
                .Cast<HiveType>()
                .Select(x => new object[] { x });
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Stats.AzureCdnLogs.Common;
using Stats.ImportAzureCdnStatistics;
using System;
using Xunit;

namespace Tests.Stats.ImportAzureCdnStatistics
{
    public class PackageStatisticsParserFacts
    {
        [Theory]
        [InlineData("SemVer1Version", "1.0.0", "1.0.0")]
        [InlineData("SemVer1VersionPreRel", "1.0.0-beta", "1.0.0-beta")]
        [InlineData("SemVer2Version", "1.0.0-1.0", "1.0.0-1.0")]
        [InlineData("System.VersionEndNonZero", "1.0.0.2", "1.0.0.2")]
        [InlineData("SemVer2VersionWithMetadata", "2.4.0-beta.1.build3958", "2.4.0-beta.1.build3958")]
        public void PackageVersionsAreParsedCorrectly(string packageId, string packageVersion, string expectedVersion)
        {
            // Arrange
            var logEntry = GetCdnLogEntry($"http://test.me/{packageId}.{packageVersion}.nupkg");
            var statsParser = new PackageStatisticsParser(null, new LoggerFactory());

            // Act
            var stats = statsParser.FromCdnLogEntry(logEntry);

            // Assert
            Assert.Equal(packageId, stats.PackageId);
            Assert.Equal(expectedVersion, stats.PackageVersion);
        }

        [Theory]
        [InlineData("donottranslate", "0.1.0")]
        [InlineData("package4.0", "1.0.0")]
        [InlineData("package4.0", "2.3.5")]
        [InlineData("package4.5", "1.0.0")]
        [InlineData("package4.5", "2.3.5")]
        [InlineData("Microsoft.VisualStudio.Shell.15.0", "15.6.27413")]
        public void TranslatesPackagesCorrectly(string packageId, string packageVersion)
        {
            // Arrange
            var logEntry = GetCdnLogEntry($"http://test.me/{packageId}.{packageVersion}.nupkg");

            var translator = new TestablePackageTranslator();
            var statsParser = new PackageStatisticsParser(translator, new LoggerFactory());

            // Act
            var stats = statsParser.FromCdnLogEntry(logEntry);

            // Assert
            Assert.Equal(packageId, stats.PackageId);
            Assert.Equal(packageVersion, stats.PackageVersion);
        }

        private CdnLogEntry GetCdnLogEntry(string requestUrl)
        {
            return new CdnLogEntry
            {
                RequestUrl = requestUrl,
                EdgeServerTimeDelivered = DateTime.UtcNow,
                EdgeServerIpAddress = "0.0.0.0",
                UserAgent = "fakeAgent"
            };
        }
    }
}

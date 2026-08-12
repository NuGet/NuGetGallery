// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Xunit;

namespace NuGetGallery
{
    public class StagingJsonResultFacts
    {
        [Fact]
        public void SerializesUnspecifiedDatabaseTimestampsAsUtcWithoutShifting()
        {
            var unspecified = new DateTime(2026, 8, 3, 20, 0, 0, DateTimeKind.Unspecified);
            var resource = new StagedPackageResource
            {
                Id = "PackageA",
                Version = "1.0.0",
                Owner = "owner",
                Created = unspecified,
                Expires = unspecified.AddDays(30),
                Package = new StagingArtifactResource
                {
                    Source = StagingResourceValues.SourceStaged,
                    Status = StagingResourceValues.StatusReady,
                    Uploaded = unspecified,
                    Validated = unspecified.AddMinutes(3),
                },
                Promotion = new StagingPromotionResource
                {
                    Status = StagingResourceValues.StatusPromoting,
                    Started = unspecified.AddHours(1),
                },
            };

            var json = StagingJsonResult.Serialize(resource);

            Assert.Contains("\"created\":\"2026-08-03T20:00:00.0000000Z\"", json);
            Assert.Contains("\"expires\":\"2026-09-02T20:00:00.0000000Z\"", json);
            Assert.Contains("\"uploaded\":\"2026-08-03T20:00:00.0000000Z\"", json);
            Assert.Contains("\"validated\":\"2026-08-03T20:03:00.0000000Z\"", json);
            Assert.Contains("\"started\":\"2026-08-03T21:00:00.0000000Z\"", json);
        }

        [Fact]
        public void ConvertsLocalTimestampsToUtc()
        {
            var local = new DateTime(2026, 8, 3, 20, 0, 0, DateTimeKind.Local);
            var resource = new StagedPackageResource
            {
                Id = "PackageA",
                Version = "1.0.0",
                Owner = "owner",
                Created = local,
                Expires = local,
            };

            var json = StagingJsonResult.Serialize(resource);

            var expected = local.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            Assert.Contains("\"created\":\"" + expected + "\"", json);
        }

        [Fact]
        public void SerializesExplicitUtcTimestampsWithZ()
        {
            var utc = new DateTime(2026, 8, 3, 20, 0, 0, DateTimeKind.Utc);
            var resource = new StagedPackageResource
            {
                Id = "PackageA",
                Version = "1.0.0",
                Owner = "owner",
                Created = utc,
                Expires = utc,
            };

            var json = StagingJsonResult.Serialize(resource);

            Assert.Contains("\"created\":\"2026-08-03T20:00:00.0000000Z\"", json);
            Assert.Contains("\"expires\":\"2026-08-03T20:00:00.0000000Z\"", json);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using Xunit;

namespace CatalogTests.Helpers
{
    public class RegistrationCatalogWriterHelperTests
    {
        private static readonly DateTime LastCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastEdited = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastDeleted = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime LastRegistrationEdited = new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc);

        private readonly MemoryStorage _storage = new MemoryStorage(new Uri("http://tempuri.org/"));
        private readonly Mock<ITelemetryService> _telemetryService = new Mock<ITelemetryService>();

        [Fact]
        public async Task ThrowsForNullRegistrations()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => WriteAsync(registrations: null));
        }

        [Fact]
        public async Task ThrowsForNullStorage()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => RegistrationCatalogWriterHelper.WritePackageSponsorshipDetailsToCatalogAsync(
                    CreateBatch(),
                    storage: null,
                    LastCreated,
                    LastEdited,
                    LastDeleted,
                    LastRegistrationEdited,
                    new CatalogContext(),
                    CancellationToken.None,
                    _telemetryService.Object,
                    logger: null));
        }

        [Fact]
        public async Task ThrowsForNullTelemetryService()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => RegistrationCatalogWriterHelper.WritePackageSponsorshipDetailsToCatalogAsync(
                    CreateBatch(),
                    _storage,
                    LastCreated,
                    LastEdited,
                    LastDeleted,
                    LastRegistrationEdited,
                    new CatalogContext(),
                    CancellationToken.None,
                    telemetryService: null,
                    logger: null));
        }

        [Fact]
        public async Task EmptyBatchWritesNothingAndReturnsInputCursor()
        {
            var result = await WriteAsync(new SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>>());

            Assert.Equal(LastRegistrationEdited, result);
            Assert.Empty(_storage.Content);
        }

        [Fact]
        public async Task AdvancesCursorToNewestTimestampInBatch()
        {
            var newest = LastRegistrationEdited.AddHours(2);
            var batch = new SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>>
            {
                { LastRegistrationEdited.AddHours(1), new[] { CreateDetails("A", newest, "https://github.com/sponsors/a") } },
                { newest, new[] { CreateDetails("B", newest, "https://github.com/sponsors/b") } },
            };

            var result = await WriteAsync(batch);

            Assert.Equal(newest, result);

            var index = ReadIndex();
            Assert.Equal(newest, index.Value<DateTime>("nuget:lastRegistrationEdited").ToUniversalTime());
        }

        [Fact]
        public async Task PreservesOtherCursorsUnchanged()
        {
            await WriteAsync(CreateBatch());

            var index = ReadIndex();
            Assert.Equal(LastCreated, index.Value<DateTime>("nuget:lastCreated").ToUniversalTime());
            Assert.Equal(LastEdited, index.Value<DateTime>("nuget:lastEdited").ToUniversalTime());
            Assert.Equal(LastDeleted, index.Value<DateTime>("nuget:lastDeleted").ToUniversalTime());
        }

        [Fact]
        public async Task WritesOneLeafPerPackageId()
        {
            var timestamp = LastRegistrationEdited.AddHours(1);
            var batch = new SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>>
            {
                {
                    timestamp,
                    new[]
                    {
                        CreateDetails("Package.One", timestamp, "https://github.com/sponsors/one"),
                        CreateDetails("Package.Two", timestamp, "https://github.com/sponsors/two"),
                    }
                },
            };

            await WriteAsync(batch);

            var leaves = GetLeaves().ToList();
            Assert.Equal(2, leaves.Count);
            Assert.Contains(leaves, leaf => (string)leaf["id"] == "Package.One");
            Assert.Contains(leaves, leaf => (string)leaf["id"] == "Package.Two");
        }

        [Fact]
        public async Task RetractionLeafOmitsSponsorshipUrls()
        {
            var timestamp = LastRegistrationEdited.AddHours(1);
            var batch = new SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>>
            {
                { timestamp, new[] { CreateDetails("Retracted.Package", timestamp, sponsorshipUrls: null) } },
            };

            await WriteAsync(batch);

            var leaf = Assert.Single(GetLeaves());
            Assert.Equal("Retracted.Package", (string)leaf["id"]);
            Assert.Null(leaf["sponsorshipUrls"]);
        }

        private Task<DateTime> WriteAsync(SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>> registrations)
        {
            return RegistrationCatalogWriterHelper.WritePackageSponsorshipDetailsToCatalogAsync(
                registrations,
                _storage,
                LastCreated,
                LastEdited,
                LastDeleted,
                LastRegistrationEdited,
                new CatalogContext(),
                CancellationToken.None,
                _telemetryService.Object,
                logger: null);
        }

        private static SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>> CreateBatch()
        {
            var timestamp = LastRegistrationEdited.AddHours(1);
            return new SortedList<DateTime, IList<PackageRegistrationSponsorshipDetails>>
            {
                { timestamp, new[] { CreateDetails("NuGet.Versioning", timestamp, "https://github.com/sponsors/octocat") } },
            };
        }

        private static PackageRegistrationSponsorshipDetails CreateDetails(string id, DateTime timestamp, string sponsorshipUrls)
        {
            var urls = sponsorshipUrls == null ? null : new List<string> { sponsorshipUrls };
            return new PackageRegistrationSponsorshipDetails(id, timestamp, urls);
        }

        private JObject ReadIndex()
        {
            var indexUri = _storage.ResolveUri("index.json");
            Assert.True(_storage.Content.TryGetValue(indexUri, out var content), "index.json was not written.");
            return ParseContent(content);
        }

        private IEnumerable<JObject> GetLeaves()
        {
            return _storage.Content
                .Where(pair => pair.Key.AbsoluteUri.Contains("/data/"))
                .Select(pair => ParseContent(pair.Value));
        }

        private static JObject ParseContent(StorageContent content)
        {
            using (var reader = new StreamReader(content.GetContentStream()))
            {
                return JObject.Parse(reader.ReadToEnd());
            }
        }
    }
}

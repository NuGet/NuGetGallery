// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CatalogTests.Helpers;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog.Registration;
using NuGet.Versioning;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationMakerTests
    {
        private const string _cacheControl = "no-store";
        private const string _contentType = "application/json";
        private const int _partitionSize = 1;
        private const int _packageCountThreshold = 128;

        private static readonly Uri _contentBaseAddress = new Uri("https://nuget.test/");
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly MemoryStorageFactory _storageFactory = new MemoryStorageFactory(new Uri("https://nuget.test/v3-registration3/"));
        private readonly Mock<ITelemetryService> _telemetryService = new Mock<ITelemetryService>();

        public RegistrationMakerTests()
        {
            RegistrationMakerCatalogItem.PackagePathProvider = new PackagesFolderPackagePathProvider();
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyStorageAndEmptyNewItems_DoesNotCreateAnything()
        {
            var emptyItems = new Dictionary<string, IGraph>();
            var storage = await ProcessAsync(emptyItems, packageId: null);

            Assert.Empty(storage.Content);
            Assert.Empty(storage.ContentBytes);
        }

        [Fact]
        public async Task ProcessAsync_WithEmptyStorageAndOneNewItem_CreatesIndexAndLeaf()
        {
            var packageDetails = new CatalogPackageDetails();
            var newItem = CreateNewItem(packageDetails);
            var storage = await ProcessAsync(newItem, packageDetails.Id);

            Verify(
                storage,
                expectedStorageContentCount: 2,
                expectedPage: new ExpectedPage(packageDetails));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task ProcessAsync_WithCustomPartitionSize_FillsPages(int partitionSize)
        {
            // The idea here is that we should see pages filled before a new pages is created.
            // Ultimately, we should have 2 filled pages plus one unfilled page.
            var packageVersionCount = partitionSize * 2 + 1;
            var pages = new List<ExpectedPage>();

            for (var i = 0; i < packageVersionCount; ++i)
            {
                var packageDetails = new CatalogPackageDetails(id: "A", version: $"1.0.{i}");
                var newItem = CreateNewItem(packageDetails);
                var storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

                pages.Add(new ExpectedPage(packageDetails));

                var expectedPages = Repaginate(pages, partitionSize);

                Verify(
                    storage,
                    expectedStorageContentCount: pages.Count + 1 /*index*/,
                    expectedPages: expectedPages);
            }
        }

        [Fact]
        public async Task ProcessAsync_WithVariablePartitionSize_RepaginatesOlderPages()
        {
            var partitionSize = 2;
            var packageVersionCount = partitionSize * 2;
            var pages = new List<ExpectedPage>();
            CatalogPackageDetails packageDetails;
            IReadOnlyDictionary<string, IGraph> newItem;
            MemoryStorage storage;
            IReadOnlyList<ExpectedPage> expectedPages;

            for (var i = 0; i < packageVersionCount; ++i)
            {
                packageDetails = new CatalogPackageDetails(id: "a", version: $"1.0.{i}");
                newItem = CreateNewItem(packageDetails);
                storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

                pages.Add(new ExpectedPage(packageDetails));

                expectedPages = Repaginate(pages, partitionSize);

                Verify(
                    storage,
                    expectedStorageContentCount: pages.Count + 1 /*index*/,
                    expectedPages: expectedPages);
            }

            partitionSize = 3;
            ++packageVersionCount;

            packageDetails = new CatalogPackageDetails(id: "a", version: $"1.0.{packageVersionCount}");
            newItem = CreateNewItem(packageDetails);
            storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

            pages.Add(new ExpectedPage(packageDetails));

            expectedPages = Repaginate(pages, partitionSize);

            Verify(
                storage,
                expectedStorageContentCount: pages.Count + 1 /*index*/,
                expectedPages: expectedPages);
        }

        private static IReadOnlyDictionary<string, IGraph> CreateNewItem(CatalogPackageDetails packageDetails)
        {
            var json = JsonConvert.SerializeObject(packageDetails, _jsonSettings);

            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.DateTimeOffset; // make sure we always preserve timezone info

                var jObject = JObject.Load(reader);
                var graph = Utils.CreateGraph(jObject, readOnly: true);

                return new Dictionary<string, IGraph>()
                {
                    { packageDetails.IdKeyword, graph }
                };
            }
        }

        private async Task<MemoryStorage> ProcessAsync(
            IReadOnlyDictionary<string, IGraph> newItems,
            string packageId,
            int? partitionSize = null)
        {
            var registrationKey = new RegistrationKey(packageId?.ToLowerInvariant() ?? string.Empty);

            await RegistrationMaker.ProcessAsync(
                registrationKey,
                newItems,
                _storageFactory,
                _contentBaseAddress,
                partitionSize ?? _partitionSize,
                _packageCountThreshold,
                _telemetryService.Object,
                CancellationToken.None);

            return (MemoryStorage)_storageFactory.Create(registrationKey.ToString());
        }

        private void Verify(
            MemoryStorage registrationStorage,
            int expectedStorageContentCount,
            ExpectedPage expectedPage)
        {
            Verify(registrationStorage, expectedStorageContentCount, new[] { expectedPage });
        }

        private void Verify(
            MemoryStorage registrationStorage,
            int expectedStorageContentCount,
            IReadOnlyList<ExpectedPage> expectedPages)
        {
            Assert.Equal(expectedStorageContentCount, registrationStorage.Content.Count);

            var firstPage = expectedPages.First();
            var packageId = firstPage.Details.First().Id.ToLowerInvariant();
            var indexUri = new Uri(GetRegistrationPackageIndexUri(packageId));
            var index = GetStorageContent<RegistrationIndex>(registrationStorage, indexUri);

            VerifyIndex(index, indexUri, packageId, expectedPages);
        }

        private void VerifyIndex(
            RegistrationIndex index,
            Uri indexUri,
            string packageId,
            IReadOnlyList<ExpectedPage> expectedPages)
        {
            Assert.Equal(indexUri.AbsoluteUri, index.IdKeyword);
            Assert.Equal(
                new[] { "catalog:CatalogRoot", "PackageRegistration", CatalogConstants.CatalogPermalink },
                index.TypeKeyword);

            Assert.True(Guid.TryParse(index.CommitId, out var guid));
            Assert.True(DateTime.TryParse(index.CommitTimeStamp, out var datetime));
            Assert.Equal(expectedPages.Count, index.Count);
            Assert.Equal(index.Count, index.Items.Length);

            for (var i = 0; i < index.Count; ++i)
            {
                var page = index.Items[i];
                var expectedPageVersionRange = expectedPages[i];

                VerifyIndexItems(
                    page,
                    indexUri,
                    packageId,
                    expectedPageVersionRange,
                    index.CommitId,
                    index.CommitTimeStamp);
            }

            var expectedContext = new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Catalog, CatalogConstants.NuGetCatalogSchemaUri),
                new JProperty(CatalogConstants.Xsd, CatalogConstants.XmlSchemaUri),
                new JProperty(CatalogConstants.Items,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.CatalogItem),
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
                new JProperty(CatalogConstants.CommitTimeStamp,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.CatalogCommitTimeStamp),
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.CommitId,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.CatalogCommitId))),
                new JProperty(CatalogConstants.Count,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.CatalogCount))),
                new JProperty(CatalogConstants.Parent,
                    new JObject(
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.CatalogParent),
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.Tags,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword),
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Tag))),
                new JProperty(CatalogConstants.PackageTargetFrameworks,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword),
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.PackageTargetFramework))),
                new JProperty(CatalogConstants.DependencyGroups,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword),
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.DependencyGroup))),
                new JProperty(CatalogConstants.Dependencies,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword),
                        new JProperty(CatalogConstants.IdKeyword, CatalogConstants.Dependency))),
                new JProperty(CatalogConstants.PackageContent,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.Published,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))),
                new JProperty(CatalogConstants.Registration,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))));

            Assert.Equal(expectedContext.ToString(), index.ContextKeyword.ToString());
        }

        private void VerifyIndexItems(
            RegistrationIndexPage items,
            Uri indexUri,
            string packageId,
            ExpectedPage expectedPage,
            string commitId,
            string commitTimeStamp)
        {
            Assert.Equal(
                GetRegistrationPackageIndexUri(packageId) + $"#page/{expectedPage.LowerVersion}/{expectedPage.UpperVersion}",
                items.IdKeyword);
            Assert.Equal(CatalogConstants.CatalogCatalogPage, items.TypeKeyword);
            Assert.Equal(commitId, items.CommitId);
            Assert.Equal(commitTimeStamp, items.CommitTimeStamp);
            Assert.Equal(items.Count, items.Items.Length);
            Assert.Equal(GetRegistrationPackageIndexUri(packageId), items.Parent);
            Assert.Equal(expectedPage.LowerVersion, items.Lower);
            Assert.Equal(expectedPage.UpperVersion, items.Upper);

            for (var i = 0; i < items.Count; ++i)
            {
                var item = items.Items[i];
                var packageDetails = expectedPage.Details[i];

                VerifyIndexItem(item, packageDetails, commitId, commitTimeStamp);
            }
        }

        private void VerifyIndexItem(
            RegistrationIndexPackageDetails item,
            CatalogPackageDetails packageDetails,
            string commitId,
            string commitTimeStamp)
        {
            var packageId = packageDetails.Id.ToLowerInvariant();
            var packageVersion = packageDetails.Version.ToLowerInvariant();

            Assert.Equal(GetRegistrationPackageVersionUri(packageId, packageVersion), item.IdKeyword);
            Assert.Equal(CatalogConstants.Package, item.TypeKeyword);
            Assert.Equal(commitId, item.CommitId);
            Assert.Equal(commitTimeStamp, item.CommitTimeStamp);
            Assert.Equal(packageDetails.IdKeyword, item.CatalogEntry.IdKeyword);
            Assert.Equal(CatalogConstants.PackageDetails, item.CatalogEntry.TypeKeyword);
            Assert.Equal(packageDetails.Authors, item.CatalogEntry.Authors);
            Assert.Equal(packageDetails.Description, item.CatalogEntry.Description);
            Assert.Empty(item.CatalogEntry.IconUrl);
            Assert.Equal(packageDetails.Id, item.CatalogEntry.Id);
            Assert.Empty(item.CatalogEntry.Language);
            Assert.Empty(item.CatalogEntry.LicenseUrl);
            Assert.Equal(packageDetails.Listed, item.CatalogEntry.Listed);
            Assert.Empty(item.CatalogEntry.MinClientVersion);
            Assert.Equal(GetPackageContentUri(packageId, packageVersion), item.CatalogEntry.PackageContent);
            Assert.Empty(item.CatalogEntry.ProjectUrl);
            Assert.Equal(GetRegistrationDateTime(packageDetails.Published), item.CatalogEntry.Published);
            Assert.Equal(packageDetails.RequireLicenseAcceptance, item.CatalogEntry.RequireLicenseAcceptance);
            Assert.Empty(item.CatalogEntry.Summary);
            Assert.Equal(new[] { string.Empty }, item.CatalogEntry.Tags);
            Assert.Empty(item.CatalogEntry.Title);
            Assert.Equal(packageDetails.Version, item.CatalogEntry.Version);

            var leafUri = new Uri(GetRegistrationPackageVersionUri(packageId, packageVersion));
            var registrationStorage = (MemoryStorage)_storageFactory.Create(packageId);
            var leaf = GetStorageContent<RegistrationPackage>(registrationStorage, leafUri);

            VerifyLeaf(leaf, leafUri, packageDetails, packageId, packageVersion);
        }

        private void VerifyLeaf(
            RegistrationPackage leaf,
            Uri expectedIdKeyword,
            CatalogPackageDetails packageDetails,
            string packageId,
            string packageVersion)
        {
            Assert.Equal(expectedIdKeyword.AbsoluteUri, leaf.IdKeyword);
            Assert.Equal(
                new[] { CatalogConstants.Package, CatalogConstants.NuGetCatalogSchemaPermalinkUri },
                leaf.TypeKeyword);
            Assert.Equal(packageDetails.Listed, leaf.Listed);
            Assert.Equal(GetPackageContentUri(packageId, packageVersion), leaf.PackageContent);
            Assert.Equal(GetRegistrationDateTime(packageDetails.Published), leaf.Published);
            Assert.Equal(GetRegistrationPackageIndexUri(packageId), leaf.Registration);

            var expectedContext = new JObject(
                new JProperty(CatalogConstants.VocabKeyword, CatalogConstants.NuGetSchemaUri),
                new JProperty(CatalogConstants.Xsd, CatalogConstants.XmlSchemaUri),
                new JProperty(CatalogConstants.CatalogEntry,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.Registration,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.PackageContent,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.IdKeyword))),
                new JProperty(CatalogConstants.Published,
                    new JObject(
                        new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.XsdDateTime))));

            Assert.Equal(expectedContext.ToString(), leaf.ContextKeyword.ToString());
        }

        private static string GetRegistrationDateTime(string catalogDateTime)
        {
            return DateTimeOffset.Parse(catalogDateTime)
                .ToLocalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.FFFzzz");
        }

        private static T GetStorageContent<T>(MemoryStorage registrationStorage, Uri contentUri)
        {
            Assert.True(registrationStorage.Content.TryGetValue(contentUri, out var content));

            var jTokenStorageContent = content as JTokenStorageContent;

            Assert.NotNull(jTokenStorageContent);
            Assert.Equal(_cacheControl, jTokenStorageContent.CacheControl);
            Assert.Equal(_contentType, jTokenStorageContent.ContentType);

            var properties = typeof(T).GetProperties(BindingFlags.NonPublic | BindingFlags.Instance);

            // Verify that no new properties were added unexpectedly.
            Assert.Equal(properties.Length, ((JObject)jTokenStorageContent.Content).Count);

            var json = jTokenStorageContent.Content.ToString();

            using (var stringReader = new StringReader(json))
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
        }

        private static string GetPackageContentUri(string packageId, string packageVersion)
        {
            return $"{_contentBaseAddress.AbsoluteUri}packages/{packageId}.{packageVersion}.nupkg";
        }

        private string GetRegistrationPackageIndexUri(string packageId)
        {
            return $"{_storageFactory.BaseAddress.AbsoluteUri}{packageId}/index.json";
        }

        private string GetRegistrationPackageVersionUri(string packageId, string packageVersion)
        {
            return $"{_storageFactory.BaseAddress.AbsoluteUri}{packageId}/{packageVersion}.json";
        }

        private static IReadOnlyList<ExpectedPage> Repaginate(
            IReadOnlyList<ExpectedPage> pages,
            int partitionSize)
        {
            return pages.Select((page, index) => new { Page = page, Index = index })
                .GroupBy(x => x.Index / partitionSize)
                .Select(group => new ExpectedPage(group.SelectMany(x => x.Page.Details).ToArray()))
                .ToList();
        }

        private sealed class ExpectedPage
        {
            internal IReadOnlyList<CatalogPackageDetails> Details { get; }
            internal string LowerVersion { get; }
            internal string UpperVersion { get; }

            internal ExpectedPage(params CatalogPackageDetails[] packageDetails)
            {
                Details = packageDetails;

                var versions = packageDetails.Select(details => new NuGetVersion(details.Version)).ToArray();

                LowerVersion = versions.Min().ToNormalizedString().ToLowerInvariant();
                UpperVersion = versions.Max().ToNormalizedString().ToLowerInvariant();
            }
        }
    }
}
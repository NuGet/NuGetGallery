// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.AzureSearch.Wrappers;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch.Integration
{
    public class AzureSearchCollectorLogicIntegrationTests
    {
        private Catalog2AzureSearchConfiguration _config;
        private Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>> _options;
        private InMemoryRegistrationClient _registrationClient;
        private InMemoryCatalogClient _catalogClient;
        private CatalogLeafFetcher _fetcher;
        private SearchDocumentBuilder _search;
        private HijackDocumentBuilder _hijack;
        private CatalogIndexActionBuilder _builder;
        private InMemoryCoreFileStorageService _coreFileStorageService;
        private VersionListDataClient _versionListDataClient;
        private Mock<ISearchIndexClientWrapper> _searchIndex;
        private InMemoryDocumentsOperations _searchDocuments;
        private Mock<ISearchIndexClientWrapper> _hijackIndex;
        private InMemoryDocumentsOperations _hijackDocuments;
        private AzureSearchCollectorLogic _collector;

        public AzureSearchCollectorLogicIntegrationTests(ITestOutputHelper output)
        {
            _config = new Catalog2AzureSearchConfiguration
            {
                MaxConcurrentBatches = 1,
                MaxConcurrentVersionListWriters = 1,
                AzureSearchBatchSize = 1000,
                StoragePath = "integration-tests",
                RegistrationsBaseUrl = "https://example/registrations/",
            };
            _options = new Mock<IOptionsSnapshot<Catalog2AzureSearchConfiguration>>();
            _options.Setup(x => x.Value).Returns(() => _config);

            _registrationClient = new InMemoryRegistrationClient();
            _catalogClient = new InMemoryCatalogClient();
            _fetcher = new CatalogLeafFetcher(
                _registrationClient,
                _catalogClient,
                _options.Object,
                output.GetLogger<CatalogLeafFetcher>());
            _search = new SearchDocumentBuilder();
            _hijack = new HijackDocumentBuilder();
            _builder = new CatalogIndexActionBuilder(
                _fetcher,
                _search,
                _hijack,
                output.GetLogger<CatalogIndexActionBuilder>());
            _coreFileStorageService = new InMemoryCoreFileStorageService();
            _versionListDataClient = new VersionListDataClient(
                _coreFileStorageService,
                _options.Object,
                output.GetLogger<VersionListDataClient>());

            _searchIndex = new Mock<ISearchIndexClientWrapper>();
            _searchDocuments = new InMemoryDocumentsOperations();
            _searchIndex.Setup(x => x.Documents).Returns(() => _searchDocuments);
            _hijackIndex = new Mock<ISearchIndexClientWrapper>();
            _hijackDocuments = new InMemoryDocumentsOperations();
            _hijackIndex.Setup(x => x.Documents).Returns(() => _hijackDocuments);

            _collector = new AzureSearchCollectorLogic(
                _catalogClient,
                _versionListDataClient,
                _builder,
                () => new BatchPusher(
                    _searchIndex.Object,
                    _hijackIndex.Object,
                    _versionListDataClient,
                    _options.Object,
                    output.GetLogger<BatchPusher>()),
                _options.Object,
                output.GetLogger<AzureSearchCollectorLogic>());
        }

        [Fact]
        public async Task AddTwoVersionsThenUnlist()
        {
            var identity1 = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0.0-alpha"));
            var identity2 = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("2.0.0+git"));

            // Step #1 - add a version
            {
                // Arrange
                var leafUrl = "https://example/catalog/0/nuget.versioning.1.0.0-alpha.json";
                _catalogClient.PackageDetailsLeaves[leafUrl] = CreateLeaf(leafUrl, identity1, listed: true);
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 10, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: identity1)
                };

                // Act
                await _collector.OnProcessBatchAsync(items);

                // Assert
                // Hijack documents
                var hijackBatch = Assert.Single(_hijackDocuments.Batches);
                var hijackAction = Assert.Single(hijackBatch.Actions);
                Assert.Equal(IndexActionType.MergeOrUpload, hijackAction.ActionType);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity1.Id, identity1.Version.ToNormalizedString()),
                    hijackAction.Document.Key);
                Assert.IsType<HijackDocument.Full>(hijackAction.Document);

                // Search documents
                var searchBatch = Assert.Single(_searchDocuments.Batches);
                AssertSearchBatch(
                    identity1.Id,
                    searchBatch,
                    exDefault: IndexActionType.Delete,
                    exIncludePrerelease: IndexActionType.MergeOrUpload,
                    exIncludeSemVer2: IndexActionType.Delete,
                    exIncludePrereleaseAndSemVer2: IndexActionType.MergeOrUpload);

                // Version list
                var pair = Assert.Single(_coreFileStorageService.Files);
                Assert.Equal("content/integration-tests/version-lists/nuget.versioning.json", pair.Key);
                var inMemoryFile = Assert.IsType<InMemoryFileReference>(pair.Value);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0-alpha"": {
      ""Listed"": true
    }
  }
}", inMemoryFile.AsString);
            }

            ClearBatches();

            // Step #2 - add another version
            {
                // Arrange
                var leafUrl = "https://example/catalog/0/nuget.versioning.2.0.0.json";
                _catalogClient.PackageDetailsLeaves[leafUrl] = CreateLeaf(leafUrl, identity2, listed: true);
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 11, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: identity2)
                };

                // Act
                await _collector.OnProcessBatchAsync(items);

                // Assert
                // Hijack documents
                var hijackBatch = Assert.Single(_hijackDocuments.Batches);
                var actions = hijackBatch.Actions.OrderBy(x => x.Document.Key).ToList();
                Assert.Equal(2, actions.Count);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity2.Id, identity1.Version.ToNormalizedString()),
                    actions[0].Document.Key);
                Assert.Equal(IndexActionType.Merge, actions[0].ActionType);
                Assert.IsType<HijackDocument.Latest>(actions[0].Document);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity2.Id, identity2.Version.ToNormalizedString()),
                    actions[1].Document.Key);
                Assert.Equal(IndexActionType.MergeOrUpload, actions[1].ActionType);
                Assert.IsType<HijackDocument.Full>(actions[1].Document);

                // Search documents
                var searchBatch = Assert.Single(_searchDocuments.Batches);
                AssertSearchBatch(
                    identity2.Id,
                    searchBatch,
                    exDefault: IndexActionType.Delete,
                    exIncludePrerelease: IndexActionType.Merge,
                    exIncludeSemVer2: IndexActionType.MergeOrUpload,
                    exIncludePrereleaseAndSemVer2: IndexActionType.MergeOrUpload);

                // Version list
                var pair = Assert.Single(_coreFileStorageService.Files);
                Assert.Equal("content/integration-tests/version-lists/nuget.versioning.json", pair.Key);
                var inMemoryFile = Assert.IsType<InMemoryFileReference>(pair.Value);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0-alpha"": {
      ""Listed"": true
    },
    ""2.0.0+git"": {
      ""Listed"": true,
      ""SemVer2"": true
    }
  }
}", inMemoryFile.AsString);
            }

            ClearBatches();

            // Step #3 - unlist the first version
            {
                // Arrange
                var leafUrl = "https://example/catalog/2/nuget.versioning.1.0.0-alpha.json";
                _catalogClient.PackageDetailsLeaves[leafUrl] = CreateLeaf(leafUrl, identity1, listed: false);
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 12, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: identity1)
                };

                // Act
                await _collector.OnProcessBatchAsync(items);

                // Assert
                // Hijack documents
                var hijackBatch = Assert.Single(_hijackDocuments.Batches);
                var actions = hijackBatch.Actions.OrderBy(x => x.Document.Key).ToList();
                Assert.Equal(2, actions.Count);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity1.Id, identity1.Version.ToNormalizedString()),
                    actions[0].Document.Key);
                Assert.Equal(IndexActionType.MergeOrUpload, actions[0].ActionType);
                Assert.IsType<HijackDocument.Full>(actions[0].Document);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity2.Id, identity2.Version.ToNormalizedString()),
                    actions[1].Document.Key);
                Assert.Equal(IndexActionType.Merge, actions[1].ActionType);
                Assert.IsType<HijackDocument.Latest>(actions[1].Document);

                // Search documents
                var searchBatch = Assert.Single(_searchDocuments.Batches);
                AssertSearchBatch(
                    identity1.Id,
                    searchBatch,
                    exDefault: IndexActionType.Delete,
                    exIncludePrerelease: IndexActionType.Delete,
                    exIncludeSemVer2: IndexActionType.Merge,
                    exIncludePrereleaseAndSemVer2: IndexActionType.Merge);

                // Version list
                var pair = Assert.Single(_coreFileStorageService.Files);
                Assert.Equal("content/integration-tests/version-lists/nuget.versioning.json", pair.Key);
                var inMemoryFile = Assert.IsType<InMemoryFileReference>(pair.Value);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0-alpha"": {},
    ""2.0.0+git"": {
      ""Listed"": true,
      ""SemVer2"": true
    }
  }
}", inMemoryFile.AsString);
            }
        }

        private void ClearBatches()
        {
            _hijackDocuments.Clear();
            _searchDocuments.Clear();
        }

        [Fact]
        public async Task DowngradeDueToDelete()
        {
            var identity1 = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.0"));
            var identity2 = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("2.0.0-alpha"));
            var leafUrl1 = "https://example/catalog/0/nuget.versioning.1.0.0.json";

            // Step #1 - add two versions
            {
                // Arrange
                var leafUrl2 = "https://example/catalog/0/nuget.versioning.2.0.0-alpha.json";
                _catalogClient.PackageDetailsLeaves[leafUrl1] = CreateLeaf(leafUrl1, identity1, listed: true);
                _catalogClient.PackageDetailsLeaves[leafUrl2] = CreateLeaf(leafUrl2, identity2, listed: true);
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl1),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 10, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: identity1),
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl2),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 10, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDetails },
                        packageIdentity: identity2),
                };

                // Act
                await _collector.OnProcessBatchAsync(items);

                // Assert
                // Hijack documents
                var hijackBatch = Assert.Single(_hijackDocuments.Batches);
                var actions = hijackBatch.Actions.OrderBy(x => x.Document.Key).ToList();
                Assert.Equal(2, actions.Count);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity1.Id, identity1.Version.ToNormalizedString()),
                    actions[0].Document.Key);
                Assert.Equal(IndexActionType.MergeOrUpload, actions[0].ActionType);
                Assert.IsType<HijackDocument.Full>(actions[0].Document);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity2.Id, identity2.Version.ToNormalizedString()),
                    actions[1].Document.Key);
                Assert.Equal(IndexActionType.MergeOrUpload, actions[1].ActionType);
                Assert.IsType<HijackDocument.Full>(actions[1].Document);

                // Search documents
                var searchBatch = Assert.Single(_searchDocuments.Batches);
                AssertSearchBatch(
                    identity1.Id,
                    searchBatch,
                    exDefault: IndexActionType.MergeOrUpload,
                    exIncludePrerelease: IndexActionType.MergeOrUpload,
                    exIncludeSemVer2: IndexActionType.MergeOrUpload,
                    exIncludePrereleaseAndSemVer2: IndexActionType.MergeOrUpload);

                // Version list
                var pair = Assert.Single(_coreFileStorageService.Files);
                Assert.Equal("content/integration-tests/version-lists/nuget.versioning.json", pair.Key);
                var inMemoryFile = Assert.IsType<InMemoryFileReference>(pair.Value);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0"": {
      ""Listed"": true
    },
    ""2.0.0-alpha"": {
      ""Listed"": true
    }
  }
}", inMemoryFile.AsString);
            }

            ClearBatches();

            // Step #2 - delete the latest version
            {
                // Arrange
                var leafUrl = "https://example/catalog/1/nuget.versioning.2.0.0-alpha.json";
                var items = new[]
                {
                    new CatalogCommitItem(
                        uri: new Uri(leafUrl),
                        commitId: null,
                        commitTimeStamp: new DateTime(2018, 12, 11, 0, 0, 0),
                        types: new List<string>(),
                        typeUris: new List<Uri> { Schema.DataTypes.PackageDelete },
                        packageIdentity: identity2),
                };
                var registrationIndexUrl = "https://example/registrations/nuget.versioning/index.json";
                var registrationPageUrl = "https://example/registrations/nuget.versioning/page/0.json";
                _registrationClient.Indexes[registrationIndexUrl] = new RegistrationIndex
                {
                    Items = new List<RegistrationPage>
                    {
                        new RegistrationPage
                        {
                            Lower = identity1.Version.ToNormalizedString(),
                            Upper = identity1.Version.ToNormalizedString(),
                            Url = registrationPageUrl,
                        },
                    },
                };
                _registrationClient.Pages[registrationPageUrl] = new RegistrationPage
                {
                    Items = new List<RegistrationLeafItem>
                    {
                        new RegistrationLeafItem
                        {
                            CatalogEntry = new RegistrationCatalogEntry
                            {
                                Url = leafUrl1,
                                Version = identity1.Version.ToNormalizedString(),
                            }
                        }
                    }
                };

                // Act
                await _collector.OnProcessBatchAsync(items);

                // Assert
                // Hijack documents
                var hijackBatch = Assert.Single(_hijackDocuments.Batches);
                var actions = hijackBatch.Actions.OrderBy(x => x.Document.Key).ToList();
                Assert.Equal(2, actions.Count);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity1.Id, identity1.Version.ToNormalizedString()),
                    actions[0].Document.Key);
                Assert.Equal(IndexActionType.MergeOrUpload, actions[0].ActionType);
                Assert.IsType<HijackDocument.Full>(actions[0].Document);
                Assert.Equal(
                    DocumentUtilities.GetHijackDocumentKey(identity2.Id, identity2.Version.ToNormalizedString()),
                    actions[1].Document.Key);
                Assert.Equal(IndexActionType.Delete, actions[1].ActionType);
                Assert.IsType<KeyedDocument>(actions[1].Document);

                // Search documents
                var searchBatch = Assert.Single(_searchDocuments.Batches);
                AssertSearchBatch(
                    identity1.Id,
                    searchBatch,
                    exDefault: IndexActionType.MergeOrUpload,
                    exIncludePrerelease: IndexActionType.MergeOrUpload,
                    exIncludeSemVer2: IndexActionType.MergeOrUpload,
                    exIncludePrereleaseAndSemVer2: IndexActionType.MergeOrUpload);

                // Version list
                var pair = Assert.Single(_coreFileStorageService.Files);
                Assert.Equal("content/integration-tests/version-lists/nuget.versioning.json", pair.Key);
                var inMemoryFile = Assert.IsType<InMemoryFileReference>(pair.Value);
                Assert.Equal(@"{
  ""VersionProperties"": {
    ""1.0.0"": {
      ""Listed"": true
    }
  }
}", inMemoryFile.AsString);
            }
        }

        private static PackageDetailsCatalogLeaf CreateLeaf(string url, PackageIdentity identity, bool listed)
        {
            return new PackageDetailsCatalogLeaf
            {
                Url = url,
                PackageId = identity.Id,
                PackageVersion = identity.Version.ToFullString(),
                VerbatimVersion = identity.Version.OriginalVersion,
                IsPrerelease = identity.Version.IsPrerelease,
                Listed = listed,
            };
        }

        private static void AssertSearchBatch(
            string packageId,
            IndexBatch<KeyedDocument> batch,
            IndexActionType exDefault,
            IndexActionType exIncludePrerelease,
            IndexActionType exIncludeSemVer2,
            IndexActionType exIncludePrereleaseAndSemVer2)
        {
            Assert.Equal(4, batch.Actions.Count());
            var defaultSearch = Assert.Single(
                batch.Actions,
                x => DocumentUtilities.GetSearchDocumentKey(packageId, SearchFilters.Default) == x.Document.Key);
            Assert.Equal(exDefault, defaultSearch.ActionType);
            var includePrereleaseSearch = Assert.Single(
                batch.Actions,
                x => DocumentUtilities.GetSearchDocumentKey(packageId, SearchFilters.IncludePrerelease) == x.Document.Key);
            Assert.Equal(exIncludePrerelease, includePrereleaseSearch.ActionType);
            var includeSemVer2Search = Assert.Single(
                batch.Actions,
                x => DocumentUtilities.GetSearchDocumentKey(packageId, SearchFilters.IncludeSemVer2) == x.Document.Key);
            Assert.Equal(exIncludeSemVer2, includeSemVer2Search.ActionType);
            var includePrereleaseAndSemVer2Search = Assert.Single(
                batch.Actions,
                x => DocumentUtilities.GetSearchDocumentKey(packageId, SearchFilters.IncludePrereleaseAndSemVer2) == x.Document.Key);
            Assert.Equal(exIncludePrereleaseAndSemVer2, includePrereleaseAndSemVer2Search.ActionType);
        }
    }
}

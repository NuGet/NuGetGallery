// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogTests.Helpers;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Infrastructure;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Registration;
using VDS.RDF;
using Xunit;

namespace CatalogTests.Registration
{
    public class RegistrationMakerTests : RegistrationTestBase
    {
        private const int _defaultPackageCountThreshold = 128;

        private static readonly Uri _contentBaseAddress = new Uri("https://nuget.test/");
        private static readonly Uri _galleryBaseAddress = new Uri("https://nuget.org/");

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
            var storage = await ProcessAsync(emptyItems, packageId: null, partitionSize: 1);

            Assert.Empty(storage.Content);
            Assert.Empty(storage.ContentBytes);
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
                var packageDetails = new CatalogIndependentPackageDetails(id: "A", version: $"1.0.{i}");
                var newItem = CreateNewItem(packageDetails);
                var storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

                pages.Add(new ExpectedPage(packageDetails));

                var expectedPages = Repaginate(pages, partitionSize);

                Verify(storage, expectedPages, partitionSize);
            }
        }

        [Fact]
        public async Task ProcessAsync_WithVariablePartitionSize_RepaginatesExistingPages()
        {
            var partitionSize = 2;
            var packageVersionCount = partitionSize * 2;
            var pages = new List<ExpectedPage>();
            CatalogIndependentPackageDetails packageDetails;
            IReadOnlyDictionary<string, IGraph> newItem;
            MemoryStorage storage;
            IReadOnlyList<ExpectedPage> expectedPages;

            for (var i = 0; i < packageVersionCount; ++i)
            {
                packageDetails = new CatalogIndependentPackageDetails(id: "a", version: $"1.0.{i}");
                newItem = CreateNewItem(packageDetails);
                storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

                pages.Add(new ExpectedPage(packageDetails));

                expectedPages = Repaginate(pages, partitionSize);

                Verify(storage, expectedPages, partitionSize);
            }

            partitionSize = 3;
            ++packageVersionCount;

            packageDetails = new CatalogIndependentPackageDetails(id: "a", version: $"1.0.{packageVersionCount}");
            newItem = CreateNewItem(packageDetails);
            storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

            pages.Add(new ExpectedPage(packageDetails));

            expectedPages = Repaginate(pages, partitionSize);

            Verify(storage, expectedPages, partitionSize);
        }

        [Fact]
        public async Task ProcessAsync_WhenNewPackageVersionWouldChangeExistingPage_RepaginatesExistingPage()
        {
            const int partitionSize = 2;
            var pages = new List<ExpectedPage>();
            CatalogIndependentPackageDetails packageDetails;
            IReadOnlyDictionary<string, IGraph> newItem;
            MemoryStorage storage;
            IReadOnlyList<ExpectedPage> expectedPages;

            for (var i = 0; i < 2; ++i)
            {
                packageDetails = new CatalogIndependentPackageDetails(id: "a", version: $"1.0.{i * 2}");
                newItem = CreateNewItem(packageDetails);
                storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

                pages.Add(new ExpectedPage(packageDetails));

                expectedPages = Repaginate(pages, partitionSize);

                Verify(storage, expectedPages, partitionSize);
            }

            packageDetails = new CatalogIndependentPackageDetails(id: "a", version: "1.0.1");
            newItem = CreateNewItem(packageDetails);
            storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize);

            pages.Insert(index: 1, item: new ExpectedPage(packageDetails));

            expectedPages = Repaginate(pages, partitionSize);

            Verify(storage, expectedPages, partitionSize);
        }

        [Fact]
        public async Task ProcessAsync_WithCustomPackageCountThreshold_TransitionsToPageWhenThresholdHit()
        {
            const int partitionSize = 2;
            const int packageCountThreshold = 2;
            var pages = new List<ExpectedPage>();
            CatalogIndependentPackageDetails packageDetails;
            IReadOnlyDictionary<string, IGraph> newItem;
            MemoryStorage storage;
            IReadOnlyList<ExpectedPage> expectedPages;

            for (var i = 0; i < 2; ++i)
            {
                packageDetails = new CatalogIndependentPackageDetails(id: "a", version: $"1.0.{i}");
                newItem = CreateNewItem(packageDetails);
                storage = await ProcessAsync(newItem, packageDetails.Id, partitionSize, packageCountThreshold);

                pages.Add(new ExpectedPage(packageDetails));

                expectedPages = Repaginate(pages, partitionSize);

                Verify(storage, expectedPages, partitionSize, packageCountThreshold);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ProcessAsync_WithDeprecationInformation_FormatsPageCorrectly(bool filterOutDeprecation)
        {
            const int partitionSize = 1;
            var pages = new List<ExpectedPage>();
            IReadOnlyDictionary<string, IGraph> newItem;
            MemoryStorage storage;
            IReadOnlyList<ExpectedPage> expectedPages;

            var packageDetailsList = new[]
            {
                // No deprecation
                new CatalogIndependentPackageDetails(
                    id: "deprecationTests", 
                    version: "2.4.3"),

                // Single reason
                new CatalogIndependentPackageDetails(
                    id: "deprecationTests", 
                    version: "3.6.5", 
                    deprecation: new RegistrationPackageDeprecation(
                        new[] {"r1" })),

                // Message
                new CatalogIndependentPackageDetails(
                    id: "deprecationTests",
                    version: "4.7.6",
                    deprecation: new RegistrationPackageDeprecation(
                        new[] {"r1", "r2" },
                        "the cow goes moo")),

                // Alternate package
                new CatalogIndependentPackageDetails(
                    id: "deprecationTests",
                    version: "5.9.8",
                    deprecation: new RegistrationPackageDeprecation(
                        new[] {"r1", "r2" },
                        null,
                        new RegistrationPackageDeprecationAlternatePackage("altPkg", "breezepackages"))),

                // Message and alternate package
                new CatalogIndependentPackageDetails(
                    id: "deprecationTests",
                    version: "6.0.2",
                    deprecation: new RegistrationPackageDeprecation(
                        new[] {"r1", "r2" },
                        "the package goes nuu",
                        new RegistrationPackageDeprecationAlternatePackage("altPkg", "breezepackages"))),
            };

            foreach (var packageDetails in packageDetailsList)
            {
                newItem = CreateNewItem(packageDetails);
                storage = await ProcessAsync(
                    newItem, 
                    packageDetails.Id, 
                    partitionSize, 
                    filterOutDeprecation: filterOutDeprecation);

                pages.Add(new ExpectedPage(packageDetails));

                expectedPages = Repaginate(pages, partitionSize);

                Verify(
                    storage, 
                    expectedPages, 
                    partitionSize, 
                    filterOutDeprecation: filterOutDeprecation);
            }
        }

        private static IReadOnlyDictionary<string, IGraph> CreateNewItem(CatalogIndependentPackageDetails packageDetails)
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
            int partitionSize,
            int packageCountThreshold = _defaultPackageCountThreshold,
            bool filterOutDeprecation = false)
        {
            var registrationKey = new RegistrationKey(packageId?.ToLowerInvariant() ?? string.Empty);

            await RegistrationMaker.ProcessAsync(
                registrationKey,
                newItems,
                (g, u, k) => true,
                _storageFactory,
                filterOutDeprecation ? RegistrationCollector.FilterOutDeprecationInformation : g => g,
                _contentBaseAddress,
                _galleryBaseAddress,
                partitionSize,
                packageCountThreshold,
                forcePackagePathProviderForIcons: false,
                telemetryService: _telemetryService.Object,
                cancellationToken: CancellationToken.None);

            return (MemoryStorage)_storageFactory.Create(registrationKey.ToString());
        }

        private void Verify(
            MemoryStorage registrationStorage,
            IReadOnlyList<ExpectedPage> expectedPages,
            int partitionSize,
            int packageCountThreshold = _defaultPackageCountThreshold,
            bool filterOutDeprecation = false)
        {
            var expectedStorageContentCount = expectedPages.SelectMany(page => page.Details).Count();

            expectedStorageContentCount += expectedStorageContentCount / packageCountThreshold;

            ++expectedStorageContentCount; // index

            Assert.Equal(expectedStorageContentCount, registrationStorage.Content.Count);

            var firstPage = expectedPages.First();
            var packageId = firstPage.Details.First().Id.ToLowerInvariant();
            var indexUri = GetRegistrationPackageIndexUri(_storageFactory.BaseAddress, packageId);
            var index = GetStorageContent<RegistrationIndex>(registrationStorage, indexUri);

            VerifyRegistrationIndex(
                registrationStorage,
                index,
                indexUri,
                packageId,
                expectedPages,
                partitionSize,
                packageCountThreshold,
                filterOutDeprecation);
        }

        private void VerifyRegistrationIndex(
            MemoryStorage registrationStorage,
            RegistrationIndex index,
            Uri indexUri,
            string packageId,
            IReadOnlyList<ExpectedPage> expectedPages,
            int partitionSize,
            int packageCountThreshold,
            bool filterOutDeprecation)
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
                var expectedPage = expectedPages[i];
                var expectFullPage = i < index.Count - 1;

                if (expectFullPage)
                {
                    Assert.Equal(partitionSize, page.Count);
                }
                else
                {
                    Assert.InRange(page.Count, low: 1, high: partitionSize);
                }

                if (page.Count == packageCountThreshold)
                {
                    VerifyRegistrationPageReference(
                        registrationStorage,
                        page,
                        packageId,
                        expectedPage,
                        index.CommitId,
                        index.CommitTimeStamp,
                        filterOutDeprecation);
                }
                else
                {
                    var pageUri = GetRegistrationPageUri(packageId, expectedPage);

                    VerifyRegistrationPage(
                        registrationStorage,
                        page,
                        pageUri,
                        packageId,
                        expectedPage,
                        index.CommitId,
                        index.CommitTimeStamp,
                        filterOutDeprecation);
                }
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
                new JProperty(CatalogConstants.Reasons,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
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

        private void VerifyRegistrationPage(
            MemoryStorage registrationStorage,
            RegistrationPage page,
            Uri pageUri,
            string packageId,
            ExpectedPage expectedPage,
            string commitId,
            string commitTimeStamp,
            bool filterOutDeprecation)
        {
            var packageIndexUri = GetRegistrationPackageIndexUri(registrationStorage.BaseAddress);

            Assert.Equal(pageUri.AbsoluteUri, page.IdKeyword);
            Assert.Equal(CatalogConstants.CatalogCatalogPage, page.TypeKeyword);
            Assert.Equal(commitId, page.CommitId);
            Assert.Equal(commitTimeStamp, page.CommitTimeStamp);
            Assert.Equal(expectedPage.LowerVersion, page.Lower);
            Assert.Equal(expectedPage.UpperVersion, page.Upper);
            Assert.Equal(page.Count, page.Items.Length);
            Assert.Equal(packageIndexUri.AbsoluteUri, page.Parent);

            for (var i = 0; i < page.Count; ++i)
            {
                var item = page.Items[i];
                var packageDetails = expectedPage.Details[i];

                VerifyRegistrationPackage(
                    registrationStorage, 
                    item, 
                    packageDetails, 
                    commitId, 
                    commitTimeStamp, 
                    filterOutDeprecation);
            }
        }

        private void VerifyRegistrationPageReference(
            MemoryStorage registrationStorage,
            RegistrationPage page,
            string packageId,
            ExpectedPage expectedPage,
            string commitId,
            string commitTimeStamp,
            bool filterOutDeprecation)
        {
            var pageUri = GetRegistrationPageReferenceUri(packageId, expectedPage);

            Assert.Equal(pageUri.AbsoluteUri, page.IdKeyword);
            Assert.Equal(CatalogConstants.CatalogCatalogPage, page.TypeKeyword);
            Assert.Equal(commitId, page.CommitId);
            Assert.Equal(commitTimeStamp, page.CommitTimeStamp);
            Assert.Equal(expectedPage.LowerVersion, page.Lower);
            Assert.Equal(expectedPage.UpperVersion, page.Upper);

            Assert.Null(page.Items);
            Assert.Null(page.Parent);

            var independentPage = GetStorageContent<RegistrationIndependentPage>(registrationStorage, pageUri);

            VerifyRegistrationPage(
                registrationStorage,
                independentPage,
                pageUri,
                packageId,
                expectedPage,
                commitId,
                commitTimeStamp,
                filterOutDeprecation);

            JObject expectedContext = GetExpectedIndexOrPageContext();

            Assert.Equal(expectedContext.ToString(), independentPage.ContextKeyword.ToString());
        }

        private void VerifyRegistrationPackage(
            MemoryStorage registrationStorage,
            RegistrationPackage package,
            CatalogIndependentPackageDetails packageDetails,
            string commitId,
            string commitTimeStamp,
            bool filterOutDeprecation)
        {
            var packageId = packageDetails.Id.ToLowerInvariant();
            var packageVersion = packageDetails.Version.ToLowerInvariant();
            var packageVersionUri = GetRegistrationPackageVersionUri(packageId, packageVersion);
            var packageContentUri = GetPackageContentUri(_contentBaseAddress, packageId, packageVersion);

            Assert.Equal(packageVersionUri.AbsoluteUri, package.IdKeyword);
            Assert.Equal(CatalogConstants.Package, package.TypeKeyword);
            Assert.Equal(commitId, package.CommitId);
            Assert.Equal(commitTimeStamp, package.CommitTimeStamp);
            Assert.Equal(packageDetails.IdKeyword, package.CatalogEntry.IdKeyword);
            Assert.Equal(CatalogConstants.PackageDetails, package.CatalogEntry.TypeKeyword);
            Assert.Equal(packageDetails.Authors, package.CatalogEntry.Authors);
            Assert.Equal(packageDetails.Description, package.CatalogEntry.Description);
            Assert.Empty(package.CatalogEntry.IconUrl);
            Assert.Equal(packageDetails.Id, package.CatalogEntry.Id);
            Assert.Empty(package.CatalogEntry.Language);
            Assert.Empty(package.CatalogEntry.LicenseUrl);
            Assert.Equal(packageDetails.Listed, package.CatalogEntry.Listed);
            Assert.Empty(package.CatalogEntry.MinClientVersion);
            Assert.Equal(packageContentUri.AbsoluteUri, package.CatalogEntry.PackageContent);
            Assert.Empty(package.CatalogEntry.ProjectUrl);
            Assert.Equal(GetRegistrationDateTime(packageDetails.Published), package.CatalogEntry.Published);
            Assert.Equal(packageDetails.RequireLicenseAcceptance, package.CatalogEntry.RequireLicenseAcceptance);
            Assert.Empty(package.CatalogEntry.Summary);
            Assert.Equal(new[] { string.Empty }, package.CatalogEntry.Tags);
            Assert.Empty(package.CatalogEntry.Title);
            Assert.Equal(packageDetails.Version, package.CatalogEntry.Version);

            var actualDeprecation = package.CatalogEntry.Deprecation;
            var expectedDeprecation = packageDetails.Deprecation;
            if (filterOutDeprecation || expectedDeprecation == null)
            {
                Assert.Null(actualDeprecation);
            }
            else
            {
                Assert.NotNull(actualDeprecation);

                Assert.Equal(expectedDeprecation.Reasons.OrderBy(r => r), actualDeprecation.Reasons.OrderBy(r => r));
                Assert.Equal(expectedDeprecation.Message, actualDeprecation.Message);

                var actualDeprecationAltPackage = actualDeprecation.AlternatePackage;
                var expectedDeprecationAltPackage = expectedDeprecation.AlternatePackage;
                if (expectedDeprecationAltPackage == null)
                {
                    Assert.Null(actualDeprecationAltPackage);
                }
                else
                {
                    Assert.NotNull(actualDeprecationAltPackage);

                    Assert.Equal(expectedDeprecationAltPackage.Id, actualDeprecationAltPackage.Id);
                    Assert.Equal(expectedDeprecationAltPackage.Range, actualDeprecationAltPackage.Range);
                }
            }

            var independentPackageUri = GetRegistrationPackageVersionUri(packageId, packageVersion);
            var independentPackage = GetStorageContent<RegistrationIndependentPackage>(
                registrationStorage,
                independentPackageUri);

            VerifyRegistrationIndependentPackage(
                registrationStorage,
                independentPackage,
                independentPackageUri,
                packageDetails,
                packageId,
                packageVersion);
        }

        private void VerifyRegistrationIndependentPackage(
            MemoryStorage registrationStorage,
            RegistrationIndependentPackage package,
            Uri packageUri,
            CatalogIndependentPackageDetails packageDetails,
            string packageId,
            string packageVersion)
        {
            var packageContentUri = GetPackageContentUri(_contentBaseAddress, packageId, packageVersion);
            var packageIndexUri = GetRegistrationPackageIndexUri(registrationStorage.BaseAddress);

            Assert.Equal(packageUri.AbsoluteUri, package.IdKeyword);
            Assert.Equal(
                new[] { CatalogConstants.Package, CatalogConstants.NuGetCatalogSchemaPermalinkUri },
                package.TypeKeyword);
            Assert.Equal(packageDetails.Listed, package.Listed);
            Assert.Equal(packageContentUri.AbsoluteUri, package.PackageContent);
            Assert.Equal(GetRegistrationDateTime(packageDetails.Published), package.Published);
            Assert.Equal(packageIndexUri.AbsoluteUri, package.Registration);

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

            Assert.Equal(expectedContext.ToString(), package.ContextKeyword.ToString());
        }

        private static JObject GetExpectedIndexOrPageContext()
        {
            return new JObject(
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
                new JProperty(CatalogConstants.Reasons,
                    new JObject(
                        new JProperty(CatalogConstants.ContainerKeyword, CatalogConstants.SetKeyword))),
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
        }

        private Uri GetRegistrationPageUri(string packageId, ExpectedPage expectedPage)
        {
            return new Uri(GetRegistrationPackageIndexUri(_storageFactory.BaseAddress, packageId),
                $"#page/{expectedPage.LowerVersion}/{expectedPage.UpperVersion}");
        }

        private Uri GetRegistrationPageReferenceUri(string packageId, ExpectedPage expectedPage)
        {
            return new Uri($"{_storageFactory.BaseAddress.AbsoluteUri}{packageId}/page"
                + $"/{expectedPage.LowerVersion}/{expectedPage.UpperVersion}.json");
        }

        private Uri GetRegistrationPackageVersionUri(string packageId, string packageVersion)
        {
            return new Uri($"{_storageFactory.BaseAddress.AbsoluteUri}{packageId}/{packageVersion}.json");
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
    }
}
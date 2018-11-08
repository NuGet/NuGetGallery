// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGet.Versioning;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class IndexActionBuilderFacts
    {
        public class AddNewPackageRegistration : BaseFacts
        {
            [Fact]
            public void UsesLatestVersionMetadataForSearchIndex()
            {
                var package1 = new TestPackage("1.0.0") { Description = "This is version 1.0.0." };
                var package2 = new TestPackage("2.0.0-alpha") { Description = "This is version 2.0.0." };
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { package1, package2 });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Upload, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Upload, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Upload, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.Equal(IndexActionType.Upload, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
                var doc0 = Assert.IsType<SearchDocument.Full>(actions.Search[0].Document);
                Assert.Equal(package1.Version, doc0.OriginalVersion);
                Assert.Equal(package1.Description, doc0.Description);
                var doc1 = Assert.IsType<SearchDocument.Full>(actions.Search[1].Document);
                Assert.Equal(package2.Version, doc1.OriginalVersion);
                Assert.Equal(package2.Description, doc1.Description);
                var doc2 = Assert.IsType<SearchDocument.Full>(actions.Search[2].Document);
                Assert.Equal(package1.Version, doc2.OriginalVersion);
                Assert.Equal(package1.Description, doc2.Description);
                var doc3 = Assert.IsType<SearchDocument.Full>(actions.Search[3].Document);
                Assert.Equal(package2.Version, doc3.OriginalVersion);
                Assert.Equal(package2.Description, doc3.Description);
            }

            [Fact]
            public void SplitsTags()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[]
                    {
                        new TestPackage("1.0.0")
                        {
                            Tags = "foo; BAR |     Baz"
                        }
                    });

                var actions = _target.AddNewPackageRegistration(input);

                var expected = new[] { "foo", "BAR", "Baz" };
                var search = Assert.IsType<SearchDocument.Full>(actions.Search[0].Document);
                var hijack = Assert.IsType<HijackDocument.Full>(actions.Hijack[0].Document);
                Assert.Equal(expected, search.Tags);
                Assert.Equal(expected, hijack.Tags);
            }

            [Fact]
            public void Uses1900ForPublishedWhenUnlisted()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[]
                    {
                        new TestPackage("1.0.0")
                        {
                            Listed = false,
                            Published = new DateTime(2018, 11, 7, 12, 27, 35),
                        }
                    });

                var actions = _target.AddNewPackageRegistration(input);

                var expected = DateTimeOffset.Parse("1900-01-01Z");
                var search = Assert.IsType<KeyedDocument>(actions.Search[0].Document);
                var hijack = Assert.IsType<HijackDocument.Full>(actions.Hijack[0].Document);
                Assert.Equal(expected, hijack.Published);
            }

            [Fact]
            public void UsesIdForNullTitleInHijackIndex()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[]
                    {
                        new TestPackage("1.0.0")
                        {
                            Listed = false,
                            Title = null,
                            Published = new DateTime(2018, 11, 7, 12, 27, 35),
                        }
                    });

                var actions = _target.AddNewPackageRegistration(input);

                var hijack = Assert.IsType<HijackDocument.Full>(actions.Hijack[0].Document);
                Assert.Equal(input.PackageId, hijack.SortableTitle);
            }

            [Fact]
            public async Task SetsCorrectDocumentMetadata()
            {
                // Arrange
                var package = new Package
                {
                    FlattenedAuthors = "Microsoft",
                    Copyright = "© Microsoft Corporation. All rights reserved.",
                    Created = new DateTime(2017, 1, 1),
                    Description = "Description.",
                    FlattenedDependencies = "Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client",
                    Hash = "oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ==",
                    HashAlgorithm = "SHA512",
                    IconUrl = "http://go.microsoft.com/fwlink/?LinkID=288890",
                    IsPrerelease = true,
                    Language = "en-US",
                    LastEdited = new DateTime(2017, 1, 2),
                    LicenseUrl = "http://go.microsoft.com/fwlink/?LinkId=331471",
                    Listed = true,
                    MinClientVersion = "2.12",
                    NormalizedVersion = "7.1.2-alpha",
                    PackageFileSize = 3039254,
                    ProjectUrl = "https://github.com/Azure/azure-storage-net",
                    Published = new DateTime(2017, 1, 3),
                    ReleaseNotes = "Release notes.",
                    RequiresLicenseAcceptance = true,
                    Summary = "Summary.",
                    Tags = "Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial",
                    Title = "Windows Azure Storage",
                    Version = "7.1.2.0-alpha+git",
                };

                var input = new NewPackageRegistration(
                    "WindowsAzure.Storage",
                    1001,
                    new[] { "Microsoft", "azure-sdk" },
                    new[] { new TestPackage("0.0.1"), package });

                // Act
                var actions = _target.AddNewPackageRegistration(input);

                // Assert
                var search = Assert.IsType<SearchDocument.Full>(actions.Search[1].Document);
                var expectedSearchJson = @"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""owners"": [
        ""azure-sdk"",
        ""Microsoft""
      ],
      ""fullVersion"": ""7.1.2-alpha+git"",
      ""lastEdited"": ""2017-01-01T00:00:00+00:00"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""versions"": [
        ""0.0.1"",
        ""7.1.2-alpha+git""
      ],
      ""authors"": ""Microsoft"",
      ""copyright"": ""© Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00+00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha+git"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrerelease""
    }
  ]
}";
                var actualSearchJson = await SerializeToJsonAsync(search);
                Assert.Equal(expectedSearchJson, actualSearchJson);

                var hijack = Assert.IsType<HijackDocument.Full>(actions.Hijack[0].Document);
                var expectedHijackJson = @"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""lastEdited"": ""2017-01-01T00:00:00+00:00"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""sortableTitle"": ""Windows Azure Storage"",
      ""isLatestStableSemVer1"": false,
      ""isLatestSemVer1"": true,
      ""isLatestStableSemVer2"": false,
      ""isLatestSemVer2"": true,
      ""authors"": ""Microsoft"",
      ""copyright"": ""© Microsoft Corporation. All rights reserved."",
      ""created"": ""2017-01-01T00:00:00+00:00"",
      ""description"": ""Description."",
      ""fileSize"": 3039254,
      ""flattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""hashAlgorithm"": ""SHA512"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""language"": ""en-US"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha+git"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""summary"": ""Summary."",
      ""tags"": [
        ""Microsoft"",
        ""Azure"",
        ""Storage"",
        ""Table"",
        ""Blob"",
        ""File"",
        ""Queue"",
        ""Scalable"",
        ""windowsazureofficial""
      ],
      ""title"": ""Windows Azure Storage"",
      ""key"": ""windowsazure_storage_7_1_2-alpha-d2luZG93c2F6dXJlLnN0b3JhZ2UvNy4xLjItYWxwaGE1""
    }
  ]
}";
                var actualHijackJson = await SerializeToJsonAsync(hijack);
                Assert.Equal(expectedHijackJson, actualHijackJson);
            }

            [Fact]
            public void ReturnsDeleteSearchActionsForAllUnlisted()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") { Listed = false } });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Delete, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Delete, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Delete, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.Equal(IndexActionType.Delete, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
            }

            [Fact]
            public void UsesSemVerLevelToIndicateSemVer2()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") { SemVerLevelKey = 2 } });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Delete, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Delete, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.Equal(IndexActionType.Upload, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.IsType<SearchDocument.Full>(actions.Search[2].Document);
                Assert.Equal(IndexActionType.Upload, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
                Assert.IsType<SearchDocument.Full>(actions.Search[3].Document);
            }

            [Fact]
            public void UsesVersionToIndicateIsPrerelease()
            {
                var input = new NewPackageRegistration(
                    "NuGet.Versioning",
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0-alpha") });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(IndexActionType.Delete, actions.Search[0].ActionType); // SearchFilters.Default
                Assert.Equal(IndexActionType.Upload, actions.Search[1].ActionType); // SearchFilters.IncludePrerelease
                Assert.IsType<SearchDocument.Full>(actions.Search[1].Document);
                Assert.Equal(IndexActionType.Delete, actions.Search[2].ActionType); // SearchFilters.IncludeSemVer2
                Assert.Equal(IndexActionType.Upload, actions.Search[3].ActionType); // SearchFilters.IncludePrereleaseAndSemVer2
                Assert.IsType<SearchDocument.Full>(actions.Search[3].Document);
            }

            [Theory]
            [InlineData("NuGet.Versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("nuget.versioning", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("NUGET.VERSIONING", "nuget_versioning-bnVnZXQudmVyc2lvbmluZw2")]
            [InlineData("_", "Xw2")]
            [InlineData("foo-bar", "foo-bar-Zm9vLWJhcg2")]
            [InlineData("İzmir", "zmir-xLB6bWly0")]
            [InlineData("İİzmir", "zmir-xLDEsHptaXI1")]
            [InlineData("zİİmir", "z__mir-esSwxLBtaXI1")]
            [InlineData("zmirİ", "zmir_-em1pcsSw0")]
            [InlineData("zmirİİ", "zmir__-em1pcsSwxLA1")]
            [InlineData("惡", "5oOh0")]
            public void EncodesSearchDocumentKey(string id, string expected)
            {
                var input = new NewPackageRegistration(
                    id,
                    1001,
                    new string[0],
                    new[] { new TestPackage("1.0.0") });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(4, actions.Search.Count);
                Assert.Equal(expected + "-Default", actions.Search[0].Document.Key);
                Assert.Equal(expected + "-IncludePrerelease", actions.Search[1].Document.Key);
                Assert.Equal(expected + "-IncludeSemVer2", actions.Search[2].Document.Key);
                Assert.Equal(expected + "-IncludePrereleaseAndSemVer2", actions.Search[3].Document.Key);
            }

            [Theory]
            [InlineData("NuGet.Versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("nuget.versioning", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("NUGET.VERSIONING", "1.0.0", "nuget_versioning_1_0_0-bnVnZXQudmVyc2lvbmluZy8xLjAuMA2")]
            [InlineData("_", "1.0.0", "1_0_0-Xy8xLjAuMA2")]
            [InlineData("foo-bar", "1.0.0", "foo-bar_1_0_0-Zm9vLWJhci8xLjAuMA2")]
            [InlineData("İzmir", "1.0.0", "zmir_1_0_0-xLB6bWlyLzEuMC4w0")]
            [InlineData("İİzmir", "1.0.0", "zmir_1_0_0-xLDEsHptaXIvMS4wLjA1")]
            [InlineData("zİİmir", "1.0.0", "z__mir_1_0_0-esSwxLBtaXIvMS4wLjA1")]
            [InlineData("zmirİ", "1.0.0", "zmir__1_0_0-em1pcsSwLzEuMC4w0")]
            [InlineData("zmirİİ", "1.0.0", "zmir___1_0_0-em1pcsSwxLAvMS4wLjA1")]
            [InlineData("惡", "1.0.0", "1_0_0-5oOhLzEuMC4w0")]
            [InlineData("jQuery", "1.0.0-alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-Alpha", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0.0-ALPHA", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "01.0.0-ALPHA", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA+githash", "jquery_1_0_0-alpha-anF1ZXJ5LzEuMC4wLWFscGhh0")]
            [InlineData("jQuery", "1.0.0-ALPHA.1+githash", "jquery_1_0_0-alpha_1-anF1ZXJ5LzEuMC4wLWFscGhhLjE1")]
            [InlineData("jQuery", "1.0.0-ALPHA.1", "jquery_1_0_0-alpha_1-anF1ZXJ5LzEuMC4wLWFscGhhLjE1")]
            public void EncodesHijackDocumentKey(string id, string version, string expected)
            {
                var input = new NewPackageRegistration(
                    id,
                    1001,
                    new string[0],
                    new[] { new TestPackage(version) });

                var actions = _target.AddNewPackageRegistration(input);

                Assert.Equal(1, actions.Hijack.Count);
                Assert.Equal(expected, actions.Hijack[0].Document.Key);
            }
        }

        private static async Task<string> SerializeToJsonAsync<T>(T obj) where T : class
        {
            using (var testHandler = new TestHttpClientHandler())
            using (var serviceClient = new SearchServiceClient(
                "unit-test-service",
                new SearchCredentials("unit-test-api-key"),
                testHandler))
            {
                var indexClient = serviceClient.Indexes.GetClient("unit-test-index");
                await indexClient.Documents.IndexAsync(IndexBatch.Upload(new[] { obj }));
                return testHandler.LastRequestBody;
            }
        }

        private class TestHttpClientHandler : HttpClientHandler
        {
            public string LastRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Content != null)
                {
                    LastRequestBody = await request.Content.ReadAsStringAsync();
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(LastRequestBody ?? string.Empty),
                };
            }
        }

        /// <summary>
        /// Provides a convenience constructor that initialized version-related properties in a consistent manner.
        /// </summary>
        public class TestPackage : Package
        {
            public TestPackage(string version)
            {
                var parsedVersion = NuGetVersion.Parse(version);
                Version = version;
                NormalizedVersion = parsedVersion.ToNormalizedString();
                IsPrerelease = parsedVersion.IsPrerelease;
            }
        }

        public abstract class BaseFacts
        {
            protected readonly IndexActionBuilder _target;

            public BaseFacts()
            {
                _target = new IndexActionBuilder();
            }
        }
    }
}

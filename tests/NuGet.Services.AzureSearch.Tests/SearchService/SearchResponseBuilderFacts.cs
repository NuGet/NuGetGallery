// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.AzureSearch.Support;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Services.AzureSearch.SearchService
{
    public class SearchResponseBuilderFacts
    {
        public class V2FromHijack : BaseFacts
        {
            [Fact]
            public void ShowsUnlisted()
            {
                _hijackResult.Results[0].Document.Listed = false;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.False(response.Data[0].Listed);
            }

            [Theory]
            [InlineData(false, true, true, false, false)]
            [InlineData(true, false, false, true, true)]
            public void UsesCorrectSetOfLatestBooleans(
                bool includeSemVer2,
                bool isLatestStableSemVer1,
                bool isLatestSemVer1,
                bool isLatestStableSemVer2,
                bool isLatestSemVer2)
            {
                _v2Request.IncludeSemVer2 = includeSemVer2;
                var doc = _hijackResult.Results[0].Document;
                doc.IsLatestStableSemVer1 = isLatestStableSemVer1;
                doc.IsLatestSemVer1 = isLatestSemVer1;
                doc.IsLatestStableSemVer2 = isLatestStableSemVer2;
                doc.IsLatestSemVer2 = isLatestSemVer2;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.True(response.Data[0].IsLatestStable);
                Assert.True(response.Data[0].IsLatest);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdForMissingTitle(string title)
            {
                _hijackResult.Results[0].Document.Title = title;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.Equal(Data.PackageId, response.Data[0].Title);
            }

            [Fact]
            public void CoalescesSomeNullFields()
            {
                var doc = _hijackResult.Results[0].Document;
                doc.OriginalVersion = null;
                doc.Description = null;
                doc.Summary = null;
                doc.Authors = null;
                doc.Tags = null;
                doc.RequiresLicenseAcceptance = null;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                var result = response.Data[0];
                Assert.NotNull(result.Version);
                Assert.Equal(doc.NormalizedVersion, result.Version);
                Assert.Equal(string.Empty, result.Description);
                Assert.Equal(string.Empty, result.Summary);
                Assert.Equal(string.Empty, result.Authors);
                Assert.Equal(string.Empty, result.Tags);
                Assert.False(result.RequiresLicenseAcceptance);
            }

            [Fact]
            public void UsesVersionSpecificDownloadCount()
            {
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "7.1.2-alpha")).Returns(4);

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.Equal(4, response.Data[0].DownloadCount);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v2Request.ShowDebug = true;
                var docResult = _hijackResult.Results[0];

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""IgnoreFilter"": false,
    ""CountOnly"": false,
    ""SortBy"": ""Popularity"",
    ""LuceneQuery"": false,
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""hijack-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": """ + JsonCasing(useNewtonsoftJson, "S") + @"imple"",
    ""SearchMode"": """ + JsonCasing(useNewtonsoftJson, "A") + @"ny""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Loaded"": ""2019-01-03T11:00:00+00:00"",
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-a" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-b" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""PopularityTransfers"": {
      ""LastModified"": ""2019-01-03T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:45"",
      ""FileSize"": 9876,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-c" + JsonQuote(useNewtonsoftJson) + @"""
    }
  }
}", actualJson);
                Assert.Same(docResult, response.Data[0].Debug);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""totalHits"": 1,
  ""data"": [
    {
      ""PackageRegistration"": {
        ""Id"": ""WindowsAzure.Storage"",
        ""DownloadCount"": 1001,
        ""Verified"": true,
        ""Owners"": [],
        ""PopularityTransfers"": [
          ""transfer1"",
          ""transfer2""
        ]
      },
      ""Version"": ""7.1.2.0-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
      ""NormalizedVersion"": ""7.1.2-alpha"",
      ""Title"": ""Windows Azure Storage"",
      ""Description"": ""Description."",
      ""Summary"": ""Summary."",
      ""Authors"": ""Microsoft"",
      ""Copyright"": """ + JsonCopyright(useNewtonsoftJson) + @" Microsoft Corporation. All rights reserved."",
      ""Language"": ""en-US"",
      ""Tags"": ""Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial"",
      ""ReleaseNotes"": ""Release notes."",
      ""ProjectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""IconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""IsLatestStable"": false,
      ""IsLatest"": true,
      ""Listed"": true,
      ""Created"": ""2017-01-01T00:00:00+00:00"",
      ""Published"": ""2017-01-03T00:00:00+00:00"",
      ""LastUpdated"": ""2017-01-03T00:00:00+00:00"",
      ""LastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""DownloadCount"": 23,
      ""FlattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""Dependencies"": [],
      ""SupportedFrameworks"": [],
      ""MinClientVersion"": ""2.12"",
      ""Hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33" + JsonPlus(useNewtonsoftJson) + @"Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""HashAlgorithm"": ""SHA512"",
      ""PackageFileSize"": 3039254,
      ""LicenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""RequiresLicenseAcceptance"": true
    }
  ]
}", actualJson);
            }

            [Fact]
            public void UsesFlatContainerUrlWhenConfigured()
            {
                _config.AllIconsInFlatContainer = true;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.Equal(Data.FlatContainerIconUrl, response.Data[0].IconUrl);
            }

            [Fact]
            public void LeavesNullIconUrlWithFlatContainerIconsButNullOriginalIconUrl()
            {
                _config.AllIconsInFlatContainer = true;
                _hijackResult.Results[0].Document.IconUrl = null;

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.Null(response.Data[0].IconUrl);
            }

            [Fact]
            public void GetsPopularityTransfer()
            {
                _auxiliaryData
                    .Setup(x => x.GetPopularityTransfers(It.IsAny<string>()))
                    .Returns(new[] { "foo", "bar" });

                var response = Target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                var transfers = response.Data[0].PackageRegistration.PopularityTransfers;
                Assert.Equal(2, transfers.Length);
                Assert.Equal("foo", transfers[0]);
                Assert.Equal("bar", transfers[1]);
            }
        }

        public class V2FromSearch : BaseFacts
        {
            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdForMissingTitle(string title)
            {
                _searchResult.Results[0].Document.Title = title;

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(Data.PackageId, response.Data[0].Title);
            }

            [Fact]
            public void CoalescesSomeNullFields()
            {
                var doc = _searchResult.Results[0].Document;
                doc.OriginalVersion = null;
                doc.Description = null;
                doc.Summary = null;
                doc.Authors = null;
                doc.Tags = null;
                doc.RequiresLicenseAcceptance = null;

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var result = response.Data[0];
                Assert.NotNull(result.Version);
                Assert.Equal(doc.NormalizedVersion, result.Version);
                Assert.Equal(string.Empty, result.Description);
                Assert.Equal(string.Empty, result.Summary);
                Assert.Equal(string.Empty, result.Authors);
                Assert.Equal(string.Empty, result.Tags);
                Assert.False(result.RequiresLicenseAcceptance);
            }

            [Fact]
            public void UsesVersionSpecificDownloadCount()
            {
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "7.1.2-alpha")).Returns(4);

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(4, response.Data[0].DownloadCount);
            }

            [Fact]
            public void SortDownloadsAscUsingAuxilaryFile()
            {
                var mockAuxilaryDataDownloads = new Dictionary<string, long>();
                for (int i = 0; i < _fiveSearchResults.Count; ++i)
                {
                    var newDoc = _fiveSearchResults.Results[i];
                    newDoc.Document.PackageId += i;
                    newDoc.Document.TotalDownloadCount += i;
                    newDoc.Document.Title += i;

                    // Set download count in reverse order on the Auxilary file
                    mockAuxilaryDataDownloads.Add(newDoc.Document.PackageId, _fiveSearchResults.Count.Value - i);
                }
                
                var expectedOrder = _fiveSearchResults
                    .Results
                    .OrderBy(x => mockAuxilaryDataDownloads[x.Document.PackageId])
                    .ThenBy(x => x.Document.Created)
                    .Select(x => x.Document.PackageId);

                _auxiliaryData
                    .Setup(x => x.GetTotalDownloadCount(It.IsAny<string>()))
                    .Returns((string packageId) =>
                    {
                        return mockAuxilaryDataDownloads[packageId];
                    });

                // Apply the order by ASCENDING TotalDownloadCount and search
                _searchParameters.OrderBy = new List<string> { IndexFields.Search.TotalDownloadCount + " asc" };
                _v2Request.SortBy = V2SortBy.TotalDownloadsAsc;
                var responseAsc = Target.V2FromSearch(
                   _v2Request,
                   _text,
                   _searchParameters,
                   _fiveSearchResults,
                   _duration);
                
                Assert.Equal(responseAsc.Data.Select(x => x.PackageRegistration.Id), expectedOrder);
            }

            [Fact]
            public void SortDownloadsDescUsingAuxilaryFile()
            {
                var mockAuxilaryDataDownloads = new Dictionary<string, long>();
                for (int i = 0; i < _fiveSearchResults.Count; ++i)
                {
                    var newDoc = _fiveSearchResults.Results[i];
                    newDoc.Document.PackageId += i;
                    newDoc.Document.TotalDownloadCount += i;
                    newDoc.Document.Title += i;

                    // Set download count in reverse order on the Auxilary file
                    mockAuxilaryDataDownloads.Add(newDoc.Document.PackageId, _fiveSearchResults.Count.Value - i);
                }

                var expectedOrder = _fiveSearchResults
                    .Results
                    .OrderByDescending(x => mockAuxilaryDataDownloads[x.Document.PackageId])
                    .ThenByDescending(x => x.Document.Created)
                    .Select(x => x.Document.PackageId);

                _auxiliaryData
                    .Setup(x => x.GetTotalDownloadCount(It.IsAny<string>()))
                    .Returns((string packageId) =>
                    {
                        return mockAuxilaryDataDownloads[packageId];
                    });

                // Apply the order by ASCENDING TotalDownloadCount and search
                _searchParameters.OrderBy = new List<string>
                { 
                    IndexFields.Search.TotalDownloadCount + " desc" 
                };
                _v2Request.SortBy = V2SortBy.TotalDownloadsDesc;

                var responseDesc = Target.V2FromSearch(
                   _v2Request,
                   _text,
                   _searchParameters,
                   _fiveSearchResults,
                   _duration);

                Assert.Equal(responseDesc.Data.Select(x => x.PackageRegistration.Id), expectedOrder);
            }
            
            [Fact]
            public void GetsPopularityTransfer()
            {
                _auxiliaryData
                    .Setup(x => x.GetPopularityTransfers(It.IsAny<string>()))
                    .Returns(new[] { "foo", "bar" });

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var transfers = response.Data[0].PackageRegistration.PopularityTransfers;
                Assert.Equal(2, transfers.Length);
                Assert.Equal("foo", transfers[0]);
                Assert.Equal("bar", transfers[1]);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v2Request.ShowDebug = true;
                var docResult = _searchResult.Results[0];

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""IgnoreFilter"": false,
    ""CountOnly"": false,
    ""SortBy"": ""Popularity"",
    ""LuceneQuery"": false,
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": """ + JsonCasing(useNewtonsoftJson, "S") + @"imple"",
    ""SearchMode"": """ + JsonCasing(useNewtonsoftJson, "A") + @"ny""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Loaded"": ""2019-01-03T11:00:00+00:00"",
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-a" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-b" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""PopularityTransfers"": {
      ""LastModified"": ""2019-01-03T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:45"",
      ""FileSize"": 9876,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-c" + JsonQuote(useNewtonsoftJson) + @"""
    }
  }
}", actualJson);
                Assert.Same(docResult, response.Data[0].Debug);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""totalHits"": 1,
  ""data"": [
    {
      ""PackageRegistration"": {
        ""Id"": ""WindowsAzure.Storage"",
        ""DownloadCount"": 1001,
        ""Verified"": true,
        ""Owners"": [
          ""Microsoft"",
          ""azure-sdk""
        ],
        ""PopularityTransfers"": [
          ""transfer1"",
          ""transfer2""
        ]
      },
      ""Version"": ""7.1.2.0-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
      ""NormalizedVersion"": ""7.1.2-alpha"",
      ""Title"": ""Windows Azure Storage"",
      ""Description"": ""Description."",
      ""Summary"": ""Summary."",
      ""Authors"": ""Microsoft"",
      ""Copyright"": """ + JsonCopyright(useNewtonsoftJson) + @" Microsoft Corporation. All rights reserved."",
      ""Language"": ""en-US"",
      ""Tags"": ""Microsoft Azure Storage Table Blob File Queue Scalable windowsazureofficial"",
      ""ReleaseNotes"": ""Release notes."",
      ""ProjectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""IconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""IsLatestStable"": false,
      ""IsLatest"": true,
      ""Listed"": true,
      ""Created"": ""2017-01-01T00:00:00+00:00"",
      ""Published"": ""2017-01-03T00:00:00+00:00"",
      ""LastUpdated"": ""2017-01-03T00:00:00+00:00"",
      ""LastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""DownloadCount"": 23,
      ""FlattenedDependencies"": ""Microsoft.Data.OData:5.6.4:net40-client|Newtonsoft.Json:6.0.8:net40-client"",
      ""Dependencies"": [],
      ""SupportedFrameworks"": [],
      ""MinClientVersion"": ""2.12"",
      ""Hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33" + JsonPlus(useNewtonsoftJson) + @"Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""HashAlgorithm"": ""SHA512"",
      ""PackageFileSize"": 3039254,
      ""LicenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""RequiresLicenseAcceptance"": true
    }
  ]
}", actualJson);
            }

            [Fact]
            public void UsesFlatContainerUrlWhenConfigured()
            {
                _config.AllIconsInFlatContainer = true;

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(Data.FlatContainerIconUrl, response.Data[0].IconUrl);
            }

            [Fact]
            public void LeavesNullIconUrlWithFlatContainerIconsButNullOriginalIconUrl()
            {
                _config.AllIconsInFlatContainer = true;
                _searchResult.Results[0].Document.IconUrl = null;

                var response = Target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Null(response.Data[0].IconUrl);
            }
        }

        public class V3FromSearch : BaseFacts
        {
            [Theory]
            [InlineData(false, "https://example/reg/")]
            [InlineData(true, "https://example/reg-gz-semver2/")]
            public void UsesProperSemVer2Url(bool includeSemVer2, string registrationsBaseUrl)
            {
                _v3Request.IncludeSemVer2 = includeSemVer2;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(response.Context.Base, registrationsBaseUrl);
                Assert.Equal(response.Data[0].AtId, registrationsBaseUrl + "windowsazure.storage/index.json");
                Assert.Equal(response.Data[0].Registration, registrationsBaseUrl + "windowsazure.storage/index.json");
                Assert.All(
                    response.Data[0].Versions,
                    x =>
                    {
                        var lowerVersion = NuGetVersion.Parse(x.Version).ToNormalizedString().ToLowerInvariant();
                        Assert.Equal(x.AtId, registrationsBaseUrl + "windowsazure.storage/" + lowerVersion + ".json");
                    });
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdForMissingTitle(string title)
            {
                _searchResult.Results[0].Document.Title = title;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(Data.PackageId, response.Data[0].Title);
            }

            [Fact]
            public void CoalescesSomeNullFields()
            {
                var doc = _searchResult.Results[0].Document;
                doc.Description = null;
                doc.Summary = null;
                doc.Tags = null;
                doc.Authors = null;
                doc.Owners = null;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var result = response.Data[0];
                Assert.Equal(string.Empty, result.Description);
                Assert.Equal(string.Empty, result.Summary);
                Assert.Empty(result.Tags);
                Assert.Equal(string.Empty, Assert.Single(result.Authors));
                Assert.Empty(result.Owners);
            }

            [Fact]
            public void UsesVersionSpecificDownloadCount()
            {
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "1.0.0")).Returns(1);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "2.0.0")).Returns(2);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "3.0.0-alpha.1")).Returns(3);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "7.1.2-alpha")).Returns(4);

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var versions = response.Data[0].Versions;
                Assert.Equal(1, versions[0].Downloads);
                Assert.Equal(2, versions[1].Downloads);
                Assert.Equal(3, versions[2].Downloads);
                Assert.Equal(4, versions[3].Downloads);
            }

            [Fact]
            public void AllowsNullPackageTypes()
            {
                var docResult = _searchResult.Results[0];
                docResult.Document.FilterablePackageTypes = null;
                docResult.Document.PackageTypes = null;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Null(response.Data[0].PackageTypes);
            }

            [Fact]
            public void AllowsEmptyPackageTypes()
            {
                var docResult = _searchResult.Results[0];
                docResult.Document.FilterablePackageTypes = new string[0];
                docResult.Document.PackageTypes = new string[0];

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Null(response.Data[0].PackageTypes);
            }

            [Fact]
            public void UsesOnlyTheDisplayPackageTypes()
            {
                var docResult = _searchResult.Results[0];
                docResult.Document.FilterablePackageTypes = new[] { "dependency", "dotnettool" };
                docResult.Document.PackageTypes = null;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Null(response.Data[0].PackageTypes);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v3Request.ShowDebug = true;
                var docResult = _searchResult.Results[0];

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Same(docResult, response.Data[0].Debug);

                Assert.NotNull(response.Debug);
                var rootDebugJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": """ + JsonCasing(useNewtonsoftJson, "S") + @"imple"",
    ""SearchMode"": """ + JsonCasing(useNewtonsoftJson, "A") + @"ny""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Loaded"": ""2019-01-03T11:00:00+00:00"",
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-a" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-b" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""PopularityTransfers"": {
      ""LastModified"": ""2019-01-03T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:45"",
      ""FileSize"": 9876,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-c" + JsonQuote(useNewtonsoftJson) + @"""
    }
  }
}", rootDebugJson);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var docResult = _searchResult.Results[0];
                docResult.Document.FilterablePackageTypes = new[] { "dependency", "dotnettool" };
                docResult.Document.PackageTypes = new[] { "Dependency", "DotnetTool" };

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""@base"": ""https://example/reg-gz-semver2/""
  },
  ""totalHits"": 1,
  ""data"": [
    {
      ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/index.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://example/reg-gz-semver2/windowsazure.storage/index.json"",
      ""id"": ""WindowsAzure.Storage"",
      ""version"": ""7.1.2-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
      ""description"": ""Description."",
      ""summary"": ""Summary."",
      ""title"": ""Windows Azure Storage"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
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
      ""authors"": [
        ""Microsoft""
      ],
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""totalDownloads"": 1001,
      ""verified"": true,
      ""packageTypes"": [
        {
          ""name"": ""Dependency""
        },
        {
          ""name"": ""DotnetTool""
        }
      ],
      ""versions"": [
        {
          ""version"": ""1.0.0"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/1.0.0.json""
        },
        {
          ""version"": ""2.0.0" + JsonPlus(useNewtonsoftJson) + @"git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/2.0.0.json""
        },
        {
          ""version"": ""3.0.0-alpha.1"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/3.0.0-alpha.1.json""
        },
        {
          ""version"": ""7.1.2-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/7.1.2-alpha.json""
        }
      ]
    }
  ]
}", actualJson);
            }

            [Fact]
            public void UsesFlatContainerUrlWhenConfigured()
            {
                _config.AllIconsInFlatContainer = true;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(Data.FlatContainerIconUrl, response.Data[0].IconUrl);
            }

            [Fact]
            public void LeavesNullIconUrlWithFlatContainerIconsButNullOriginalIconUrl()
            {
                _config.AllIconsInFlatContainer = true;
                _searchResult.Results[0].Document.IconUrl = null;

                var response = Target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Null(response.Data[0].IconUrl);
            }
        }

        public class V3FromSearchDocument : BaseFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v3Request.ShowDebug = true;
                var doc = _searchResult.Results[0].Document;

                var response = Target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                var debugDoc = Assert.IsType<DebugDocumentResult>(response.Data[0].Debug);
                Assert.Same(doc, debugDoc.Document);

                Assert.NotNull(response.Debug);
                var rootDebugJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Get"",
  ""DocumentKey"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2"",
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Loaded"": ""2019-01-03T11:00:00+00:00"",
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-a" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-b" + JsonQuote(useNewtonsoftJson) + @"""
    },
    ""PopularityTransfers"": {
      ""LastModified"": ""2019-01-03T11:00:00+00:00"",
      ""LoadDuration"": ""00:00:45"",
      ""FileSize"": 9876,
      ""ETag"": """ + JsonQuote(useNewtonsoftJson) + @"etag-c" + JsonQuote(useNewtonsoftJson) + @"""
    }
  }
}", rootDebugJson);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var doc = _searchResult.Results[0].Document;

                var response = Target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""@base"": ""https://example/reg-gz-semver2/""
  },
  ""totalHits"": 1,
  ""data"": [
    {
      ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/index.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://example/reg-gz-semver2/windowsazure.storage/index.json"",
      ""id"": ""WindowsAzure.Storage"",
      ""version"": ""7.1.2-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
      ""description"": ""Description."",
      ""summary"": ""Summary."",
      ""title"": ""Windows Azure Storage"",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288890"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
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
      ""authors"": [
        ""Microsoft""
      ],
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""totalDownloads"": 1001,
      ""verified"": true,
      ""packageTypes"": [
        {
          ""name"": ""Dependency""
        }
      ],
      ""versions"": [
        {
          ""version"": ""1.0.0"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/1.0.0.json""
        },
        {
          ""version"": ""2.0.0" + JsonPlus(useNewtonsoftJson) + @"git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/2.0.0.json""
        },
        {
          ""version"": ""3.0.0-alpha.1"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/3.0.0-alpha.1.json""
        },
        {
          ""version"": ""7.1.2-alpha" + JsonPlus(useNewtonsoftJson) + @"git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/7.1.2-alpha.json""
        }
      ]
    }
  ]
}", actualJson);
            }

            [Fact]
            public void UsesFlatContainerUrlWhenConfigured()
            {
                _config.AllIconsInFlatContainer = true;
                var doc = _searchResult.Results[0].Document;

                var response = Target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                Assert.Equal(Data.FlatContainerIconUrl, response.Data[0].IconUrl);
            }

            [Fact]
            public void LeavesNullIconUrlWithFlatContainerIconsButNullOriginalIconUrl()
            {
                _config.AllIconsInFlatContainer = true;
                var doc = _searchResult.Results[0].Document;
                doc.IconUrl = null;

                var response = Target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                Assert.Null(response.Data[0].IconUrl);
            }
        }

        public class AutocompleteFromSearch : BaseFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _autocompleteRequest.ShowDebug = true;

                var response = Target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Type"": ""PackageIds"",
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": """ + JsonCasing(useNewtonsoftJson, "S") + @"imple"",
    ""SearchMode"": """ + JsonCasing(useNewtonsoftJson, "A") + @"ny""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000""
}", actualJson);
            }

            [Fact]
            public void ReturnsPackageIds()
            {
                _autocompleteRequest.Type = AutocompleteRequestType.PackageIds;

                var response = Target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response);
                Assert.Single(response.Data);
                Assert.Equal("WindowsAzure.Storage", response.Data[0]);
            }

            [Fact]
            public void ReturnsEmptyPackageVersions()
            {
                _autocompleteRequest.Type = AutocompleteRequestType.PackageVersions;

                var response = Target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _emptySearchResult,
                    _duration);

                Assert.NotNull(response);
                Assert.Empty(response.Data);
            }

            [Fact]
            public void ReturnsPackageVersions()
            {
                _autocompleteRequest.Type = AutocompleteRequestType.PackageVersions;

                var response = Target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response);
                Assert.Equal(4, response.Data.Count);
                Assert.Equal("1.0.0", response.Data[0]);
                Assert.Equal("2.0.0+git", response.Data[1]);
                Assert.Equal("3.0.0-alpha.1", response.Data[2]);
                Assert.Equal("7.1.2-alpha+git", response.Data[3]);
            }

            [Fact]
            public void PackageVersionsThrowsIfMultipleResults()
            {
                _autocompleteRequest.Type = AutocompleteRequestType.PackageVersions;

                var exception = Assert.Throws<ArgumentException>(() => Target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _manySearchResults,
                    _duration));

                Assert.Equal("result", exception.ParamName);
                Assert.Contains("Package version autocomplete queries should have a single document result", exception.Message);
            }
        }

        public class EmptyV3 : BaseFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var response = Target.EmptyV3(_v3Request);

                var actualJson =  GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#"",
    ""@base"": ""https://example/reg-gz-semver2/""
  },
  ""totalHits"": 0,
  ""data"": []
}", actualJson);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v3Request.ShowDebug = true;

                var response = Target.EmptyV3(_v3Request);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexOperationType"": ""Empty""
}", actualJson);
            }
        }

        public class EmptyV2 : BaseFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var response = Target.EmptyV2(_v2Request);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""totalHits"": 0,
  ""data"": []
}", actualJson);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _v2Request.ShowDebug = true;

                var response = Target.EmptyV2(_v2Request);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""IgnoreFilter"": false,
    ""CountOnly"": false,
    ""SortBy"": ""Popularity"",
    ""LuceneQuery"": false,
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexOperationType"": ""Empty""
}", actualJson);
            }
        }

        public class EmptyAutocomplete : BaseFacts
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void ProducesExpectedResponse(bool useNewtonsoftJson)
            {
                var response = Target.EmptyAutocomplete(_autocompleteRequest);

                var actualJson = GetActualJson(response, useNewtonsoftJson);
                Assert.Equal(@"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 0,
  ""data"": []
}", actualJson);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void CanIncludeDebugInformation(bool useNewtonsoftJson)
            {
                _autocompleteRequest.ShowDebug = true;

                var response = Target.EmptyAutocomplete(_autocompleteRequest);

                Assert.NotNull(response.Debug);
                var actualJson = GetActualJson(response.Debug, useNewtonsoftJson);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Type"": ""PackageIds"",
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""IncludeTestData"": true,
    ""ShowDebug"": true
  },
  ""IndexOperationType"": ""Empty""
}", actualJson);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly Mock<IAuxiliaryData> _auxiliaryData;
            protected readonly SearchServiceConfiguration _config;
            protected readonly Mock<IOptionsSnapshot<SearchServiceConfiguration>> _options;
            protected readonly V2SearchRequest _v2Request;
            protected readonly V3SearchRequest _v3Request;
            protected readonly AutocompleteRequest _autocompleteRequest;
            protected readonly SearchParameters _searchParameters;
            protected readonly string _text;
            protected readonly TimeSpan _duration;
            protected readonly DocumentSearchResult<SearchDocument.Full> _searchResult;
            protected readonly DocumentSearchResult<SearchDocument.Full> _fiveSearchResults;
            protected readonly DocumentSearchResult<SearchDocument.Full> _emptySearchResult;
            protected readonly DocumentSearchResult<SearchDocument.Full> _manySearchResults;
            protected readonly DocumentSearchResult<HijackDocument.Full> _hijackResult;
            protected readonly AuxiliaryFilesMetadata _auxiliaryMetadata;

            public SearchResponseBuilder Target => new SearchResponseBuilder(
                new Lazy<IAuxiliaryData>(() => _auxiliaryData.Object),
                _options.Object);

            public static IEnumerable<object[]> MissingTitles = new[]
            {
                new object[] { null },
                new object[] { string.Empty },
                new object[] { " " },
                new object[] { " \t"},
            };

            public BaseFacts()
            {
                _auxiliaryData = new Mock<IAuxiliaryData>();
                _config = new SearchServiceConfiguration();
                _options = new Mock<IOptionsSnapshot<SearchServiceConfiguration>>();
                _options.Setup(x => x.Value).Returns(() => _config);
                _auxiliaryMetadata = new AuxiliaryFilesMetadata(
                    new DateTimeOffset(2019, 1, 3, 11, 0, 0, TimeSpan.Zero),
                    new AuxiliaryFileMetadata(
                        new DateTimeOffset(2019, 1, 1, 11, 0, 0, TimeSpan.Zero),
                        TimeSpan.FromSeconds(15),
                        1234,
                        "\"etag-a\""),
                    new AuxiliaryFileMetadata(
                        new DateTimeOffset(2019, 1, 2, 11, 0, 0, TimeSpan.Zero),
                        TimeSpan.FromSeconds(30),
                        5678,
                        "\"etag-b\""),
                    new AuxiliaryFileMetadata(
                        new DateTimeOffset(2019, 1, 3, 11, 0, 0, TimeSpan.Zero),
                        TimeSpan.FromSeconds(45),
                        9876,
                        "\"etag-c\""));

                _config.SearchIndexName = "search-index";
                _config.HijackIndexName = "hijack-index";
                _config.SemVer1RegistrationsBaseUrl = "https://example/reg/";
                _config.SemVer2RegistrationsBaseUrl = "https://example/reg-gz-semver2/";
                _config.FlatContainerBaseUrl = Data.FlatContainerBaseUrl;
                _config.FlatContainerContainerName = Data.FlatContainerContainerName;

                _auxiliaryData
                    .Setup(x => x.GetTotalDownloadCount(It.IsAny<string>()))
                    .Returns(Data.TotalDownloadCount);
                _auxiliaryData
                    .Setup(x => x.GetDownloadCount(It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(23);
                _auxiliaryData
                    .Setup(x => x.IsVerified(It.IsAny<string>()))
                    .Returns(true);
                _auxiliaryData
                    .Setup(x => x.Metadata)
                    .Returns(() => _auxiliaryMetadata);
                _auxiliaryData
                    .Setup(x => x.GetPopularityTransfers(It.IsAny<string>()))
                    .Returns(() => new[] { "transfer1", "transfer2" });

                _v2Request = new V2SearchRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true,
                    IncludeTestData = true,
                };
                _v3Request = new V3SearchRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true,
                    IncludeTestData = true,
                };
                _autocompleteRequest = new AutocompleteRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true,
                    IncludeTestData = true,
                };
                _searchParameters = new SearchParameters();
                _text = "azure storage sdk";
                _duration = TimeSpan.FromMilliseconds(250);
                _emptySearchResult = new DocumentSearchResult<SearchDocument.Full>
                {
                    Count = 0,
                    Results = new List<SearchResult<SearchDocument.Full>>(),
                };
                _searchResult = new DocumentSearchResult<SearchDocument.Full>
                {
                    Count = 1,
                    Results = new List<SearchResult<SearchDocument.Full>>
                    {
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                    },
                };
                _fiveSearchResults = new DocumentSearchResult<SearchDocument.Full>
                {
                    Count = 5,
                    Results = new List<SearchResult<SearchDocument.Full>>
                    {
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                    },
                };
                _manySearchResults = new DocumentSearchResult<SearchDocument.Full>
                {
                    Count = 2,
                    Results = new List<SearchResult<SearchDocument.Full>>
                    {
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                        new SearchResult<SearchDocument.Full>
                        {
                            Document = Data.SearchDocument,
                        },
                    },
                };
                _hijackResult = new DocumentSearchResult<HijackDocument.Full>
                {
                    Count = 1,
                    Results = new List<SearchResult<HijackDocument.Full>>
                    {
                        new SearchResult<HijackDocument.Full>
                        {
                            Document = Data.HijackDocument,
                        },
                    },
                };
            }

            protected string JsonCasing(bool useNewtonsoftJson, string letter) => (useNewtonsoftJson ? letter.ToLowerInvariant() : letter.ToUpperInvariant());
            protected string JsonQuote(bool useNewtonsoftJson) => useNewtonsoftJson ? "\\\"" : "\\u0022";
            protected string JsonPlus(bool useNewtonsoftJson) => useNewtonsoftJson ? "+" : "\\u002B";
            protected string JsonCopyright(bool useNewtonsoftJson) => useNewtonsoftJson ? "©" : "\\u00A9";

            protected string GetActualJson(object response, bool useNewtonsoftJson)
            {
                if (useNewtonsoftJson)
                {
                    return Newtonsoft.Json.JsonConvert.SerializeObject(response, new Newtonsoft.Json.JsonSerializerSettings
                    {
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
                        Converters =
                        {
                            new Newtonsoft.Json.Converters.StringEnumConverter(),
                        },
                        Formatting = Newtonsoft.Json.Formatting.Indented,
                    });
                }
                else
                {
                    return System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
                    {
                        IgnoreNullValues = true,
                        Converters =
                        {
                            new System.Text.Json.Serialization.JsonStringEnumConverter(),
                            new TimeSpanConverter(),
                        },
                        WriteIndented = true,
                    });
                }
            }
        }
    }
}

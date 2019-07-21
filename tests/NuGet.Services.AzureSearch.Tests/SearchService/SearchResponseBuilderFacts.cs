// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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

                var response = _target.V2FromHijack(
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

                var response = _target.V2FromHijack(
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

                var response = _target.V2FromHijack(
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

                var response = _target.V2FromHijack(
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

                var response = _target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.Equal(4, response.Data[0].DownloadCount);
            }

            [Fact]
            public void CanIncludeDebugInformation()
            {
                _v2Request.ShowDebug = true;
                var docResult = _hijackResult.Results[0];

                var response = _target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = JsonConvert.SerializeObject(response.Debug, _jsonSerializerSettings);
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
    ""ShowDebug"": true
  },
  ""IndexName"": ""hijack-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": ""simple"",
    ""SearchMode"": ""any""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""Loaded"": ""2019-01-01T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": ""\""etag-a\""""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""Loaded"": ""2019-01-02T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": ""\""etag-b\""""
    }
  }
}", actualJson);
                Assert.Same(docResult, response.Data[0].Debug);
            }

            [Fact]
            public void ProducesExpectedResponse()
            {
                var response = _target.V2FromHijack(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _hijackResult,
                    _duration);

                var actualJson = JsonConvert.SerializeObject(response, _jsonSerializerSettings);
                Assert.Equal(@"{
  ""totalHits"": 1,
  ""data"": [
    {
      ""PackageRegistration"": {
        ""Id"": ""WindowsAzure.Storage"",
        ""DownloadCount"": 1001,
        ""Verified"": true,
        ""Owners"": []
      },
      ""Version"": ""7.1.2.0-alpha+git"",
      ""NormalizedVersion"": ""7.1.2-alpha"",
      ""Title"": ""Windows Azure Storage"",
      ""Description"": ""Description."",
      ""Summary"": ""Summary."",
      ""Authors"": ""Microsoft"",
      ""Copyright"": ""© Microsoft Corporation. All rights reserved."",
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
      ""Hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""HashAlgorithm"": ""SHA512"",
      ""PackageFileSize"": 3039254,
      ""LicenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""RequiresLicenseAcceptance"": true
    }
  ]
}", actualJson);
            }
        }

        public class V2FromSearch : BaseFacts
        {
            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdForMissingTitle(string title)
            {
                _searchResult.Results[0].Document.Title = title;

                var response = _target.V2FromSearch(
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

                var response = _target.V2FromSearch(
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

                var response = _target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Equal(4, response.Data[0].DownloadCount);
            }

            [Fact]
            public void CanIncludeDebugInformation()
            {
                _v2Request.ShowDebug = true;
                var docResult = _searchResult.Results[0];

                var response = _target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = JsonConvert.SerializeObject(response.Debug, _jsonSerializerSettings);
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
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": ""simple"",
    ""SearchMode"": ""any""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""Loaded"": ""2019-01-01T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": ""\""etag-a\""""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""Loaded"": ""2019-01-02T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": ""\""etag-b\""""
    }
  }
}", actualJson);
                Assert.Same(docResult, response.Data[0].Debug);
            }

            [Fact]
            public void ProducesExpectedResponse()
            {
                var response = _target.V2FromSearch(
                    _v2Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var actualJson = JsonConvert.SerializeObject(response, _jsonSerializerSettings);
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
        ]
      },
      ""Version"": ""7.1.2.0-alpha+git"",
      ""NormalizedVersion"": ""7.1.2-alpha"",
      ""Title"": ""Windows Azure Storage"",
      ""Description"": ""Description."",
      ""Summary"": ""Summary."",
      ""Authors"": ""Microsoft"",
      ""Copyright"": ""© Microsoft Corporation. All rights reserved."",
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
      ""Hash"": ""oMs9XKzRTsbnIpITcqZ5XAv1h2z6oyJ33+Z/PJx36iVikge/8wm5AORqAv7soKND3v5/0QWW9PQ0ktQuQu9aQQ=="",
      ""HashAlgorithm"": ""SHA512"",
      ""PackageFileSize"": 3039254,
      ""LicenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""RequiresLicenseAcceptance"": true
    }
  ]
}", actualJson);
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

                var response = _target.V3FromSearch(
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

                var response = _target.V3FromSearch(
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

                var response = _target.V3FromSearch(
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
            }

            [Fact]
            public void UsesVersionSpecificDownloadCount()
            {
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "1.0.0")).Returns(1);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "2.0.0")).Returns(2);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "3.0.0-alpha.1")).Returns(3);
                _auxiliaryData.Setup(x => x.GetDownloadCount(It.IsAny<string>(), "7.1.2-alpha")).Returns(4);

                var response = _target.V3FromSearch(
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
            public void CanIncludeDebugInformation()
            {
                _v3Request.ShowDebug = true;
                var docResult = _searchResult.Results[0];

                var response = _target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.Same(docResult, response.Data[0].Debug);

                Assert.NotNull(response.Debug);
                var rootDebugJson = JsonConvert.SerializeObject(response.Debug, _jsonSerializerSettings);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": ""simple"",
    ""SearchMode"": ""any""
  },
  ""SearchText"": ""azure storage sdk"",
  ""DocumentSearchResult"": {
    ""Count"": 1
  },
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""Loaded"": ""2019-01-01T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": ""\""etag-a\""""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""Loaded"": ""2019-01-02T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": ""\""etag-b\""""
    }
  }
}", rootDebugJson);
            }

            [Fact]
            public void ProducesExpectedResponse()
            {
                var response = _target.V3FromSearch(
                    _v3Request,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                var actualJson = JsonConvert.SerializeObject(response, _jsonSerializerSettings);
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
      ""version"": ""7.1.2-alpha+git"",
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
      ""totalDownloads"": 1001,
      ""verified"": true,
      ""versions"": [
        {
          ""version"": ""1.0.0"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/1.0.0.json""
        },
        {
          ""version"": ""2.0.0+git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/2.0.0.json""
        },
        {
          ""version"": ""3.0.0-alpha.1"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/3.0.0-alpha.1.json""
        },
        {
          ""version"": ""7.1.2-alpha+git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/7.1.2-alpha.json""
        }
      ]
    }
  ]
}", actualJson);
            }
        }

        public class V3FromSearchDocument : BaseFacts
        {
            [Fact]
            public void CanIncludeDebugInformation()
            {
                _v3Request.ShowDebug = true;
                var doc = _searchResult.Results[0].Document;

                var response = _target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                var debugDoc = Assert.IsType<DebugDocumentResult>(response.Data[0].Debug);
                Assert.Same(doc, debugDoc.Document);

                Assert.NotNull(response.Debug);
                var rootDebugJson = JsonConvert.SerializeObject(response.Debug, _jsonSerializerSettings);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Get"",
  ""DocumentKey"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2"",
  ""QueryDuration"": ""00:00:00.2500000"",
  ""AuxiliaryFilesMetadata"": {
    ""Downloads"": {
      ""LastModified"": ""2019-01-01T11:00:00+00:00"",
      ""Loaded"": ""2019-01-01T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:15"",
      ""FileSize"": 1234,
      ""ETag"": ""\""etag-a\""""
    },
    ""VerifiedPackages"": {
      ""LastModified"": ""2019-01-02T11:00:00+00:00"",
      ""Loaded"": ""2019-01-02T12:00:00+00:00"",
      ""LoadDuration"": ""00:00:30"",
      ""FileSize"": 5678,
      ""ETag"": ""\""etag-b\""""
    }
  }
}", rootDebugJson);
            }

            [Fact]
            public void ProducesExpectedResponse()
            {
                var doc = _searchResult.Results[0].Document;

                var response = _target.V3FromSearchDocument(
                    _v3Request,
                    doc.Key,
                    doc,
                    _duration);

                var actualJson = JsonConvert.SerializeObject(response, _jsonSerializerSettings);
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
      ""version"": ""7.1.2-alpha+git"",
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
      ""totalDownloads"": 1001,
      ""verified"": true,
      ""versions"": [
        {
          ""version"": ""1.0.0"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/1.0.0.json""
        },
        {
          ""version"": ""2.0.0+git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/2.0.0.json""
        },
        {
          ""version"": ""3.0.0-alpha.1"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/3.0.0-alpha.1.json""
        },
        {
          ""version"": ""7.1.2-alpha+git"",
          ""downloads"": 23,
          ""@id"": ""https://example/reg-gz-semver2/windowsazure.storage/7.1.2-alpha.json""
        }
      ]
    }
  ]
}", actualJson);
            }
        }

        public class AutocompleteFromSearch : BaseFacts
        {
            [Fact]
            public void CanIncludeDebugInformation()
            {
                _autocompleteRequest.ShowDebug = true;

                var response = _target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _searchResult,
                    _duration);

                Assert.NotNull(response.Debug);
                var actualJson = JsonConvert.SerializeObject(response.Debug, _jsonSerializerSettings);
                Assert.Equal(@"{
  ""SearchRequest"": {
    ""Type"": ""PackageIds"",
    ""Skip"": 0,
    ""Take"": 0,
    ""IncludePrerelease"": true,
    ""IncludeSemVer2"": true,
    ""ShowDebug"": true
  },
  ""IndexName"": ""search-index"",
  ""IndexOperationType"": ""Search"",
  ""SearchParameters"": {
    ""IncludeTotalResultCount"": false,
    ""QueryType"": ""simple"",
    ""SearchMode"": ""any""
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

                var response = _target.AutocompleteFromSearch(
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

                var response = _target.AutocompleteFromSearch(
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

                var response = _target.AutocompleteFromSearch(
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

                var exception = Assert.Throws<ArgumentException>(() => _target.AutocompleteFromSearch(
                    _autocompleteRequest,
                    _text,
                    _searchParameters,
                    _manySearchResults,
                    _duration));

                Assert.Equal("result", exception.ParamName);
                Assert.Contains("Package version autocomplete queries should have a single document result", exception.Message);
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
            protected readonly DocumentSearchResult<SearchDocument.Full> _emptySearchResult;
            protected readonly DocumentSearchResult<SearchDocument.Full> _manySearchResults;
            protected readonly DocumentSearchResult<HijackDocument.Full> _hijackResult;
            protected readonly JsonSerializerSettings _jsonSerializerSettings;
            protected readonly SearchResponseBuilder _target;
            protected readonly AuxiliaryFilesMetadata _auxiliaryMetadata;

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
                    new AuxiliaryFileMetadata(
                        new DateTimeOffset(2019, 1, 1, 11, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2019, 1, 1, 12, 0, 0, TimeSpan.Zero),
                        TimeSpan.FromSeconds(15),
                        1234,
                        "\"etag-a\""),
                    new AuxiliaryFileMetadata(
                        new DateTimeOffset(2019, 1, 2, 11, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2019, 1, 2, 12, 0, 0, TimeSpan.Zero),
                        TimeSpan.FromSeconds(30),
                        5678,
                        "\"etag-b\""));

                _config.SearchIndexName = "search-index";
                _config.HijackIndexName = "hijack-index";
                _config.SemVer1RegistrationsBaseUrl = "https://example/reg/";
                _config.SemVer2RegistrationsBaseUrl = "https://example/reg-gz-semver2/";

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

                _v2Request = new V2SearchRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true,
                };
                _v3Request = new V3SearchRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true
                };
                _autocompleteRequest = new AutocompleteRequest
                {
                    IncludePrerelease = true,
                    IncludeSemVer2 = true,
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

                _jsonSerializerSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters =
                    {
                        new StringEnumConverter(),
                    },
                    Formatting = Formatting.Indented,
                };

                _target = new SearchResponseBuilder(
                    new Lazy<IAuxiliaryData>(() => _auxiliaryData.Object),
                    _options.Object);
            }
        }

    }
}

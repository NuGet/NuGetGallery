// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.Support;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilderFacts
    {
        public class Keyed : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Keyed(Data.PackageId, _searchFilters);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateVersionList : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.UpdateVersionList(Data.PackageId, _searchFilters, _versions);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-IncludePrereleaseAndSemVer2""
    }
  ]
}", json);
            }
        }

        public class UpdateLatest : BaseFacts
        {
            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.UpdateLatest(
                    searchFilters,
                    _versions,
                    Data.NormalizedVersion,
                    Data.FullVersion,
                    Data.Leaf);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""searchFilters"": """ + expected + @""",
      ""fullVersion"": ""7.1.2-alpha+git"",
      ""lastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""semVerLevel"": 2,
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
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }
        }

        public class Full : BaseFacts
        {
            [Theory]
            [MemberData(nameof(AllSearchFilters))]
            public async Task SetsExpectedProperties(SearchFilters searchFilters, string expected)
            {
                var document = _target.Full(
                    Data.PackageId,
                    searchFilters,
                    _versions,
                    Data.FullVersion,
                    Data.PackageEntity,
                    _owners,
                    _totalDownloadCount);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""totalDownloadCount"": 1001,
      ""owners"": [
        ""Microsoft"",
        ""azure-sdk""
      ],
      ""searchFilters"": """ + expected + @""",
      ""fullVersion"": ""7.1.2-alpha+git"",
      ""lastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""versions"": [
        ""1.0.0"",
        ""2.0.0+git"",
        ""3.0.0-alpha.1"",
        ""7.1.2-alpha+git""
      ],
      ""semVerLevel"": 2,
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
      ""key"": ""windowsazure_storage-d2luZG93c2F6dXJlLnN0b3JhZ2U1-" + expected + @"""
    }
  ]
}", json);
            }

            [Fact]
            public void SplitsTags()
            {
                var package = Data.PackageEntity;
                package.Tags = "foo; BAR |     Baz";
                var document = _target.Full(
                    Data.PackageId,
                    _searchFilters,
                    _versions,
                    Data.FullVersion,
                    package,
                    _owners,
                    _totalDownloadCount);

                Assert.Equal(new[] { "foo", "BAR", "Baz" }, document.Tags);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly SearchFilters _searchFilters;
            protected readonly string[] _versions;
            protected readonly string[] _owners;
            protected readonly int _totalDownloadCount;
            protected readonly SearchDocumentBuilder _target;

            public static IEnumerable<object[]> AllSearchFilters => new[]
            {
                new object[] { SearchFilters.Default, "Default" },
                new object[] { SearchFilters.IncludePrerelease, "IncludePrerelease" },
                new object[] { SearchFilters.IncludeSemVer2, "IncludeSemVer2" },
                new object[] { SearchFilters.IncludePrereleaseAndSemVer2, "IncludePrereleaseAndSemVer2" },
            };

            [Fact]
            public void AllSearchFiltersAreCovered()
            {
                var testedSearchFilters = AllSearchFilters.Select(x => (SearchFilters)x[0]).ToList();
                var allSearchFilters = Enum.GetValues(typeof(SearchFilters)).Cast<SearchFilters>().ToList();

                Assert.Empty(testedSearchFilters.Except(allSearchFilters));
                Assert.Empty(allSearchFilters.Except(testedSearchFilters));
            }

            public BaseFacts()
            {
                _searchFilters = SearchFilters.IncludePrereleaseAndSemVer2;
                _versions = new[] { "1.0.0", "2.0.0+git", "3.0.0-alpha.1", Data.FullVersion };
                _owners = new[] { "Microsoft", "azure-sdk" };
                _totalDownloadCount = 1001;

                _target = new SearchDocumentBuilder();
            }
        }
    }
}

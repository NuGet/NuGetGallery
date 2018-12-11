// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.AzureSearch.Support;
using Xunit;

namespace NuGet.Services.AzureSearch
{
    public class HijackDocumentBuilderFacts
    {
        public class Keyed : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Keyed(Data.PackageId, Data.NormalizedVersion);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""key"": ""windowsazure_storage_7_1_2-alpha-d2luZG93c2F6dXJlLnN0b3JhZ2UvNy4xLjItYWxwaGE1""
    }
  ]
}", json);
            }
        }

        public class Latest : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Latest(Data.PackageId, Data.NormalizedVersion, _changes);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(@"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""isLatestStableSemVer1"": false,
      ""isLatestSemVer1"": true,
      ""isLatestStableSemVer2"": false,
      ""isLatestSemVer2"": true,
      ""key"": ""windowsazure_storage_7_1_2-alpha-d2luZG93c2F6dXJlLnN0b3JhZ2UvNy4xLjItYWxwaGE1""
    }
  ]
}", json);
            }
        }

        public class FullFromPackageEntity : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Full(Data.PackageId, _changes, Data.PackageEntity);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(_fullJson, json);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForSortableTitle(string title)
            {
                var package = Data.PackageEntity;
                package.Title = title;

                var document = _target.Full(Data.PackageId, _changes, package);

                Assert.Equal(Data.PackageId, document.SortableTitle);
            }

            [Fact]
            public void Uses1900ForPublishedWhenUnlisted()
            {
                var package = Data.PackageEntity;
                package.Listed = false;

                var document = _target.Full(Data.PackageId, _changes, package);

                Assert.Equal(DateTimeOffset.Parse("1900-01-01Z"), document.Published);
            }

            [Fact]
            public void SplitsTags()
            {
                var package = Data.PackageEntity;
                package.Tags = "foo; BAR |     Baz";

                var document = _target.Full(Data.PackageId, _changes, package);

                Assert.Equal(new[] { "foo", "BAR", "Baz" }, document.Tags);
            }

            [Theory]
            [InlineData(null)]
            [InlineData(2)]
            public void UsesSemVerLevelToIndicateSemVer2(int? semVerLevelKey)
            {
                var package = Data.PackageEntity;
                package.SemVerLevelKey = semVerLevelKey;

                var document = _target.Full(Data.PackageId, _changes, package);

                Assert.Equal(semVerLevelKey, document.SemVerLevel);
            }

            /// <summary>
            /// The caller is expected to verify consistency.
            /// </summary>
            [Fact]
            public void DoesNotUseVersionToIndicateIsPrerelease()
            {
                var package = Data.PackageEntity;
                package.IsPrerelease = false;
                package.Version = "2.0.0-alpha";
                package.NormalizedVersion = "2.0.0-alpha";

                var document = _target.Full(Data.PackageId, _changes, package);

                Assert.False(document.Prerelease);
            }
        }

        public class FullFromPackageDetailsCatalogLeaf : BaseFacts
        {
            [Fact]
            public async Task SetsExpectedProperties()
            {
                var document = _target.Full(Data.NormalizedVersion, _changes, Data.Leaf);

                var json = await SerializationUtilities.SerializeToJsonAsync(document);
                Assert.Equal(_fullJson, json);
            }

            [Theory]
            [MemberData(nameof(MissingTitles))]
            public void UsesIdWhenMissingForSortableTitle(string title)
            {
                var leaf = Data.Leaf;
                leaf.Title = title;

                var document = _target.Full(Data.NormalizedVersion, _changes, leaf);

                Assert.Equal(Data.PackageId, document.SortableTitle);
            }

            [Fact]
            public void DefaultsRequiresLicenseAcceptanceToFalse()
            {
                var leaf = Data.Leaf;
                leaf.RequireLicenseAgreement = null;

                var document = _target.Full(Data.NormalizedVersion, _changes, leaf);

                Assert.False(document.RequiresLicenseAcceptance);
            }
        }

        public abstract class BaseFacts
        {
            protected readonly HijackDocumentChanges _changes;
            protected readonly string _fullJson;
            protected readonly HijackDocumentBuilder _target;

            public static IEnumerable<object[]> MissingTitles = new[]
            {
                new object[] { null },
                new object[] { string.Empty },
                new object[] { " " },
                new object[] { " \t"},
            };

            public BaseFacts()
            {
                _changes = new HijackDocumentChanges(
                    delete: false,
                    updateMetadata: true,
                    latestStableSemVer1: false,
                    latestSemVer1: true,
                    latestStableSemVer2: false,
                    latestSemVer2: true);
                _fullJson = @"{
  ""value"": [
    {
      ""@search.action"": ""upload"",
      ""isLatestStableSemVer1"": false,
      ""isLatestSemVer1"": true,
      ""isLatestStableSemVer2"": false,
      ""isLatestSemVer2"": true,
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
      ""lastEdited"": ""2017-01-02T00:00:00+00:00"",
      ""licenseUrl"": ""http://go.microsoft.com/fwlink/?LinkId=331471"",
      ""minClientVersion"": ""2.12"",
      ""normalizedVersion"": ""7.1.2-alpha"",
      ""originalVersion"": ""7.1.2.0-alpha+git"",
      ""packageId"": ""WindowsAzure.Storage"",
      ""prerelease"": true,
      ""projectUrl"": ""https://github.com/Azure/azure-storage-net"",
      ""published"": ""2017-01-03T00:00:00+00:00"",
      ""releaseNotes"": ""Release notes."",
      ""requiresLicenseAcceptance"": true,
      ""sortableTitle"": ""Windows Azure Storage"",
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

                _target = new HijackDocumentBuilder();
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogIndexEntryTests
    {
        private static readonly JsonSerializerSettings _settings;

        private readonly string _commitId;
        private readonly DateTime _commitTimeStamp;
        private const string _packageId = "a";
        private readonly NuGetVersion _packageVersion;
        private readonly PackageIdentity _packageIdentity;
        private readonly Uri _uri;

        static CatalogIndexEntryTests()
        {
            _settings = new JsonSerializerSettings();

            _settings.Converters.Add(new NuGetVersionConverter());
            _settings.Converters.Add(new StringEnumConverter());
        }

        public CatalogIndexEntryTests()
        {
            _commitId = Guid.NewGuid().ToString();
            _commitTimeStamp = DateTime.UtcNow;
            _packageVersion = new NuGetVersion("1.2.3");
            _uri = new Uri("https://nuget.test/a");
            _packageIdentity = new PackageIdentity(_packageId, _packageVersion);
        }

        [Fact]
        public void Constructor_WhenUriIsNull_Throws()
        {
            const Uri uri = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogIndexEntry(
                    uri,
                    CatalogConstants.NuGetPackageDetails,
                    _commitId,
                    _commitTimeStamp,
                    _packageIdentity));

            Assert.Equal("uri", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenTypeIsNullEmptyOrWhitespace_Throws(string type)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogIndexEntry(
                    _uri,
                    type,
                    _commitId,
                    _commitTimeStamp,
                    _packageIdentity));

            Assert.Equal("type", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenTypesIsNull_Throws()
        {
            string[] types = null;

            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogIndexEntry(
                    _uri,
                    types,
                    _commitId,
                    _commitTimeStamp,
                    _packageIdentity));

            Assert.Equal("types", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenTypesIsEmpty_Throws()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogIndexEntry(
                    _uri,
                    Array.Empty<string>(),
                    _commitId,
                    _commitTimeStamp,
                    _packageIdentity));

            Assert.Equal("types", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenCommitIdIsNullEmptyOrWhitespace_Throws(string commitId)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogIndexEntry(
                    _uri,
                    CatalogConstants.NuGetPackageDelete,
                    commitId,
                    _commitTimeStamp,
                    _packageIdentity));

            Assert.Equal("commitId", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenPackageIdentityIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogIndexEntry(
                    _uri,
                    CatalogConstants.NuGetPackageDelete,
                    _commitId,
                    _commitTimeStamp,
                    packageIdentity: null));

            Assert.Equal("packageIdentity", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_ReturnsInstance()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        [Fact]
        public void Create_WhenCommitItemIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CatalogIndexEntry.Create(commitItem: null));

            Assert.Equal("commitItem", exception.ParamName);
        }

        [Fact]
        public void Create_WhenArgumentIsValid_ReturnsInstance()
        {
            var contextJObject = TestUtility.CreateCatalogContextJObject();
            var commitItemJObject = TestUtility.CreateCatalogCommitItemJObject(_commitTimeStamp, _packageIdentity);
            var commitItem = CatalogCommitItem.Create(contextJObject, commitItemJObject);

            var entry = CatalogIndexEntry.Create(commitItem);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.Equal(commitItemJObject[CatalogConstants.CommitId].ToString(), entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp.ToUniversalTime());
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        [Fact]
        public void CompareTo_WhenOtherIsNull_Throws()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);

            var exception = Assert.Throws<ArgumentNullException>(() => entry.CompareTo(other: null));

            Assert.Equal("other", exception.ParamName);
        }

        [Fact]
        public void CompareTo_WhenCommitTimeStampsAreNotEqual_ReturnsNonZero()
        {
            var now = DateTime.UtcNow;
            var olderEntry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                now.AddHours(-1),
                _packageIdentity);
            var newerEntry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                now,
                _packageIdentity);

            Assert.Equal(-1, olderEntry.CompareTo(newerEntry));
            Assert.Equal(1, newerEntry.CompareTo(olderEntry));
        }

        [Fact]
        public void CompareTo_WhenCommitTimeStampsAreEqual_ReturnsZero()
        {
            var entry0 = new CatalogIndexEntry(
                new Uri("https://nuget.test/a"),
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);
            var entry1 = new CatalogIndexEntry(
                new Uri("https://nuget.test/b"),
                CatalogConstants.NuGetPackageDelete,
                Guid.NewGuid().ToString(),
                _commitTimeStamp,
                new PackageIdentity(id: "b", version: new NuGetVersion("4.5.6")));

            Assert.Equal(0, entry0.CompareTo(entry0));
            Assert.Equal(0, entry0.CompareTo(entry1));
            Assert.Equal(0, entry1.CompareTo(entry0));
        }

        [Fact]
        public void IsDelete_WhenTypeIsNotPackageDelete_ReturnsFalse()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);

            Assert.False(entry.IsDelete);
        }

        [Fact]
        public void IsDelete_WhenTypeIsPackageDelete_ReturnsTrue()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDelete,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);

            Assert.True(entry.IsDelete);
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageIdentity);

            var jObject = CreateCatalogIndexJObject(CatalogConstants.NuGetPackageDetails);

            var expectedResult = jObject.ToString(Formatting.None, _settings.Converters.ToArray());
            var actualResult = JsonConvert.SerializeObject(entry, Formatting.None, _settings);

            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData(CatalogConstants.IdKeyword)]
        [InlineData(CatalogConstants.TypeKeyword)]
        [InlineData(CatalogConstants.CommitId)]
        [InlineData(CatalogConstants.CommitTimeStamp)]
        [InlineData(CatalogConstants.NuGetId)]
        [InlineData(CatalogConstants.NuGetVersion)]
        public void JsonDeserialization_WhenRequiredPropertyIsMissing_Throws(string propertyToRemove)
        {
            var jObject = CreateCatalogIndexJObject(CatalogConstants.NuGetPackageDetails);

            jObject.Remove(propertyToRemove);

            var json = jObject.ToString(Formatting.None, _settings.Converters.ToArray());

            var exception = Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<CatalogIndexEntry>(json, _settings));

            Assert.StartsWith($"Required property '{propertyToRemove}' not found in JSON.", exception.Message);
        }

        [Fact]
        public void JsonDeserialization_WhenTypeIsPackageDetails_ReturnsCorrectObject()
        {
            var jObject = CreateCatalogIndexJObject(CatalogConstants.NuGetPackageDetails);
            var json = jObject.ToString(Formatting.None, _settings.Converters.ToArray());

            var entry = JsonConvert.DeserializeObject<CatalogIndexEntry>(json, _settings);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.False(entry.IsDelete);
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        [Fact]
        public void JsonDeserialization_WhenTypeIsPackageDelete_ReturnsCorrectObject()
        {
            var jObject = CreateCatalogIndexJObject(CatalogConstants.NuGetPackageDelete);
            var json = jObject.ToString(Formatting.None, _settings.Converters.ToArray());

            var entry = JsonConvert.DeserializeObject<CatalogIndexEntry>(json, _settings);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDelete, entry.Types.Single());
            Assert.True(entry.IsDelete);
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        private JObject CreateCatalogIndexJObject(string type)
        {
            return new JObject(
                new JProperty(CatalogConstants.IdKeyword, _uri),
                new JProperty(CatalogConstants.TypeKeyword, type),
                new JProperty(CatalogConstants.CommitId, _commitId),
                new JProperty(CatalogConstants.CommitTimeStamp, _commitTimeStamp.ToString(CatalogConstants.CommitTimeStampFormat)),
                new JProperty(CatalogConstants.NuGetId, _packageId),
                new JProperty(CatalogConstants.NuGetVersion, _packageVersion.ToNormalizedString()));
        }
    }
}
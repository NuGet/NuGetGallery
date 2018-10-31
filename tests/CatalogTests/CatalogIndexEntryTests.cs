// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NgTests;
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
        private const string _packageId = "A";
        private readonly NuGetVersion _packageVersion;
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
                    _packageId,
                    _packageVersion));

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
                    _packageId,
                    _packageVersion));

            Assert.Equal("type", exception.ParamName);
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
                    _packageId,
                    _packageVersion));

            Assert.Equal("commitId", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenIdIsNullEmptyOrWhitespace_Throws(string id)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogIndexEntry(
                    _uri,
                    CatalogConstants.NuGetPackageDelete,
                    _commitId,
                    _commitTimeStamp,
                    id,
                    _packageVersion));

            Assert.Equal("id", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenVersionIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogIndexEntry(
                    _uri,
                    CatalogConstants.NuGetPackageDetails,
                    _commitId,
                    _commitTimeStamp,
                    _packageId,
                    version: null));

            Assert.Equal("version", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_ReturnsInstance()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageId,
                _packageVersion);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        [Fact]
        public void Create_WhenTokenIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CatalogIndexEntry.Create(token: null));

            Assert.Equal("token", exception.ParamName);
        }

        [Fact]
        public void Create_WhenArgumentIsValid_ReturnsInstance()
        {
            var jObject = CreateCatalogIndexJObject();

            var entry = CatalogIndexEntry.Create(jObject);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        [Theory]
        [InlineData("2018-10-15T01:12:40.1234567Z")]
        [InlineData("2018-10-15T01:12:40.123456Z")]
        [InlineData("2018-10-15T01:12:40.12345Z")]
        [InlineData("2018-10-15T01:12:40.1234Z")]
        [InlineData("2018-10-15T01:12:40.123Z")]
        [InlineData("2018-10-15T01:12:40.12Z")]
        [InlineData("2018-10-15T01:12:40.1Z")]
        public void Create_WhenCommitTimeStampVariesInSignificantDigits_DeserializesCorrectly(string commitTimeStamp)
        {
            var jObject = new JObject(
                new JProperty(CatalogConstants.IdKeyword, _uri),
                new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.NuGetPackageDetails),
                new JProperty(CatalogConstants.CommitId, _commitId),
                new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp),
                new JProperty(CatalogConstants.NuGetId, _packageId),
                new JProperty(CatalogConstants.NuGetVersion, _packageVersion.ToNormalizedString()));

            var entry = CatalogIndexEntry.Create(jObject);

            var expectedCommitTimeStamp = DateTime.ParseExact(
                commitTimeStamp,
                CatalogConstants.CommitTimeStampFormat,
                DateTimeFormatInfo.CurrentInfo,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            Assert.Equal(expectedCommitTimeStamp, entry.CommitTimeStamp);
        }

        [Fact]
        public void CompareTo_WhenOtherIsNull_Throws()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageId,
                _packageVersion);

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
                _packageId,
                _packageVersion);
            var newerEntry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                now,
                _packageId,
                _packageVersion);

            Assert.Equal(-1, olderEntry.CompareTo(newerEntry));
            Assert.Equal(1, newerEntry.CompareTo(olderEntry));
        }

        [Fact]
        public void CompareTo_WhenCommitTimeStampsAreEqual_ReturnsZero()
        {
            var entry1 = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageId,
                _packageVersion);
            var entry2 = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageId,
                _packageVersion);

            Assert.Equal(0, entry1.CompareTo(entry2));
            Assert.Equal(0, entry2.CompareTo(entry1));
        }

        [Fact]
        public void IsDelete_WhenTypeIsNotPackageDelete_ReturnsFalse()
        {
            var entry = new CatalogIndexEntry(
                _uri,
                CatalogConstants.NuGetPackageDetails,
                _commitId,
                _commitTimeStamp,
                _packageId,
                _packageVersion);

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
                _packageId,
                _packageVersion);

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
                _packageId,
                _packageVersion);

            var jObject = CreateCatalogIndexJObject();

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
            var jObject = CreateCatalogIndexJObject();

            jObject.Remove(propertyToRemove);

            var json = jObject.ToString(Formatting.None, _settings.Converters.ToArray());

            var exception = Assert.Throws<JsonSerializationException>(
                () => JsonConvert.DeserializeObject<CatalogIndexEntry>(json, _settings));

            Assert.StartsWith($"Required property '{propertyToRemove}' not found in JSON.", exception.Message);
        }

        [Fact]
        public void JsonDeserialization_ReturnsCorrectObject()
        {
            var jObject = CreateCatalogIndexJObject();
            var json = jObject.ToString(Formatting.None, _settings.Converters.ToArray());

            var entry = JsonConvert.DeserializeObject<CatalogIndexEntry>(json, _settings);

            Assert.Equal(_uri.AbsoluteUri, entry.Uri.AbsoluteUri);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, entry.Types.Single());
            Assert.Equal(_commitId, entry.CommitId);
            Assert.Equal(_commitTimeStamp, entry.CommitTimeStamp);
            Assert.Equal(_packageId, entry.Id);
            Assert.Equal(_packageVersion, entry.Version);
        }

        private JObject CreateCatalogIndexJObject(string commitTimeStamp = null)
        {
            return new JObject(
                new JProperty(CatalogConstants.IdKeyword, _uri),
                new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.NuGetPackageDetails),
                new JProperty(CatalogConstants.CommitId, _commitId),
                new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp ?? _commitTimeStamp.ToString(CatalogConstants.CommitTimeStampFormat)),
                new JProperty(CatalogConstants.NuGetId, _packageId),
                new JProperty(CatalogConstants.NuGetVersion, _packageVersion.ToNormalizedString()));
        }
    }
}
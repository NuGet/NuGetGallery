// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NgTests;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitItemTests
    {
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity(id: "A", version: new NuGetVersion("1.0.0"));
        private readonly DateTime _now = DateTime.UtcNow;
        private readonly JObject _context;
        private readonly JObject _commitItem;

        public CatalogCommitItemTests()
        {
            _context = TestUtility.CreateCatalogContextJObject();
            _commitItem = TestUtility.CreateCatalogCommitItemJObject(_now, _packageIdentity);
        }

        [Fact]
        public void Create_WhenContextIsNull_Throws()
        {
            const JObject context = null;

            var exception = Assert.Throws<ArgumentNullException>(() => CatalogCommitItem.Create(context, _commitItem));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void Create_WhenCommitItemIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CatalogCommitItem.Create(_context, commitItem: null));

            Assert.Equal("commitItem", exception.ParamName);
        }

        [Fact]
        public void Create_WhenTypeIsEmpty_Throws()
        {
            _commitItem[CatalogConstants.TypeKeyword] = new JArray();

            var exception = Assert.Throws<ArgumentException>(() => CatalogCommitItem.Create(_context, _commitItem));

            Assert.Equal("commitItem", exception.ParamName);
            Assert.StartsWith($"The value of property '{CatalogConstants.TypeKeyword}' must be non-null and non-empty.", exception.Message);
        }

        [Fact]
        public void Create_WhenArgumentsAreValid_ReturnsInstance()
        {
            var commitItem = CatalogCommitItem.Create(_context, _commitItem);

            Assert.Equal($"https://nuget.test/{_packageIdentity.Id}", commitItem.Uri.AbsoluteUri);
            Assert.Equal(_now, commitItem.CommitTimeStamp.ToUniversalTime());
            Assert.True(Guid.TryParse(commitItem.CommitId, out var commitId));
            Assert.Equal(_packageIdentity, commitItem.PackageIdentity);
            Assert.Equal(CatalogConstants.NuGetPackageDetails, commitItem.Types.Single());
            Assert.Equal(Schema.DataTypes.PackageDetails.AbsoluteUri, commitItem.TypeUris.Single().AbsoluteUri);
        }

        [Fact]
        public void IsRegistrationLevelItem_WhenContextIsNull_Throws()
        {
            const JObject context = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitItem.IsRegistrationLevelItem(context, _commitItem));

            Assert.Equal("context", exception.ParamName);
        }

        [Fact]
        public void IsRegistrationLevelItem_WhenCommitItemIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => CatalogCommitItem.IsRegistrationLevelItem(_context, commitItem: null));

            Assert.Equal("commitItem", exception.ParamName);
        }

        [Fact]
        public void IsRegistrationLevelItem_WhenItemIsVersionLevel_ReturnsFalse()
        {
            Assert.False(CatalogCommitItem.IsRegistrationLevelItem(_context, _commitItem));
        }

        [Fact]
        public void IsRegistrationLevelItem_WhenItemIsRegistrationLevel_ReturnsTrue()
        {
            var commitItem = CreateRegistrationLevelCommitItemJObject();

            Assert.True(CatalogCommitItem.IsRegistrationLevelItem(_context, commitItem));
        }

        [Fact]
        public void IsRegistrationLevelItem_WhenItemIsRegistrationLevel_AvoidsCreateWhichRequiresVersion()
        {
            // A registration-level (ID-level) item is version-less, so CatalogCommitItem.Create cannot
            // represent it. IsRegistrationLevelItem lets version-level readers skip it before calling Create.
            var commitItem = CreateRegistrationLevelCommitItemJObject();

            Assert.Throws<ArgumentException>(() => CatalogCommitItem.Create(_context, commitItem));
            Assert.True(CatalogCommitItem.IsRegistrationLevelItem(_context, commitItem));
        }

        private JObject CreateRegistrationLevelCommitItemJObject()
        {
            // Mirrors PackageSponsorshipDetailsCatalogItem's version-less page item: it carries @type and
            // nuget:id but intentionally no nuget:version.
            return new JObject(
                new JProperty(CatalogConstants.IdKeyword, $"https://nuget.test/{_packageIdentity.Id}"),
                new JProperty(CatalogConstants.TypeKeyword, CatalogConstants.NuGetPackageSponsorshipDetails),
                new JProperty(CatalogConstants.CommitTimeStamp, _now.ToString("O")),
                new JProperty(CatalogConstants.CommitId, Guid.NewGuid().ToString()),
                new JProperty(CatalogConstants.NuGetId, _packageIdentity.Id));
        }

        [Fact]
        public void CompareTo_WhenObjIsNull_Throws()
        {
            var commitItem = CatalogCommitItem.Create(_context, _commitItem);

            var exception = Assert.Throws<ArgumentException>(() => commitItem.CompareTo(obj: null));

            Assert.Equal("obj", exception.ParamName);
        }

        [Fact]
        public void CompareTo_WhenObjIsNotCatalogCommit_Throws()
        {
            var commitItem = CatalogCommitItem.Create(_context, _commitItem);

            var exception = Assert.Throws<ArgumentException>(() => commitItem.CompareTo(new object()));

            Assert.Equal("obj", exception.ParamName);
        }

        [Fact]
        public void CompareTo_WhenArgumentIsValid_ReturnsValue()
        {
            var commitTimeStamp1 = DateTime.UtcNow;
            var commitTimeStamp2 = DateTime.UtcNow.AddMinutes(1);
            var commitItem0 = TestUtility.CreateCatalogCommitItem(commitTimeStamp1, _packageIdentity);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(commitTimeStamp2, _packageIdentity);

            Assert.Equal(0, commitItem0.CompareTo(commitItem0));
            Assert.Equal(-1, commitItem0.CompareTo(commitItem1));
            Assert.Equal(1, commitItem1.CompareTo(commitItem0));
        }
    }
}
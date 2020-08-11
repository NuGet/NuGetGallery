// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json.Linq;
using NgTests;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitTests
    {
        [Fact]
        public void Create_WhenCommitIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => CatalogCommit.Create(commit: null));

            Assert.Equal("commit", exception.ParamName);
        }

        [Fact]
        public void Create_WhenArgumentIsValid_ReturnsInstance()
        {
            var idKeyword = "https://nuget.test/a";
            var commitTimeStamp = DateTime.UtcNow.ToString("O");
            var jObject = new JObject(
                new JProperty(CatalogConstants.IdKeyword, idKeyword),
                new JProperty(CatalogConstants.CommitTimeStamp, commitTimeStamp));

            var commit = CatalogCommit.Create(jObject);

            Assert.Equal(idKeyword, commit.Uri.AbsoluteUri);
            Assert.Equal(commitTimeStamp, commit.CommitTimeStamp.ToUniversalTime().ToString("O"));
        }

        [Fact]
        public void CompareTo_WhenObjIsNull_Throws()
        {
            var commit = CreateCatalogCommit();

            var exception = Assert.Throws<ArgumentException>(() => commit.CompareTo(obj: null));

            Assert.Equal("obj", exception.ParamName);
        }

        [Fact]
        public void CompareTo_WhenObjIsNotCatalogCommit_Throws()
        {
            var commit = CreateCatalogCommit();

            var exception = Assert.Throws<ArgumentException>(() => commit.CompareTo(new object()));

            Assert.Equal("obj", exception.ParamName);
        }

        [Fact]
        public void CompareTo_WhenObjIsCatalogCommit_ReturnsValue()
        {
            var jObject0 = new JObject(
                new JProperty(CatalogConstants.IdKeyword, "https://nuget.test/a"),
                new JProperty(CatalogConstants.CommitTimeStamp, DateTime.UtcNow.ToString("O")));

            var jObject1 = new JObject(
                new JProperty(CatalogConstants.IdKeyword, "https://nuget.test/b"),
                new JProperty(CatalogConstants.CommitTimeStamp, DateTime.UtcNow.AddHours(1).ToString("O")));

            var commit0 = CatalogCommit.Create(jObject0);
            var commit1 = CatalogCommit.Create(jObject1);

            Assert.Equal(-1, commit0.CompareTo(commit1));
            Assert.Equal(0, commit0.CompareTo(commit0));
            Assert.Equal(1, commit1.CompareTo(commit0));
        }

        private static CatalogCommit CreateCatalogCommit()
        {
            var jObject = new JObject(
                new JProperty(CatalogConstants.IdKeyword, "https://nuget.test/a"),
                new JProperty(CatalogConstants.CommitTimeStamp, DateTime.UtcNow.ToString("O")));

            return CatalogCommit.Create(jObject);
        }
    }
}
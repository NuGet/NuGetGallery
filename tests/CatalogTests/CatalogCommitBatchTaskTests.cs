// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitBatchTaskTests
    {
        private readonly DateTime _minCommitTimeStamp = DateTime.UtcNow;
        private const string _key = "a";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_WhenKeyIsNullEmptyOrWhitespace_Throws(string key)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitBatchTask(_minCommitTimeStamp, key));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentsAreValid_ReturnsInstance()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);

            Assert.Equal(_minCommitTimeStamp, commitBatchTask.MinCommitTimeStamp);
            Assert.Equal(_key, commitBatchTask.Key);
            Assert.Null(commitBatchTask.Task);
        }

        [Fact]
        public void GetHashCode_Always_ReturnsKeyHashCode()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);

            Assert.Equal(_key.GetHashCode(), commitBatchTask.GetHashCode());
        }

        [Fact]
        public void Equals_WhenObjectIsNull_ReturnsFalse()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);

            Assert.False(commitBatchTask.Equals(obj: null));
            Assert.False(commitBatchTask.Equals(other: null));
        }

        [Fact]
        public void Equals_WhenObjectIsNotCatalogCommitBatchTask_ReturnsFalse()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);

            Assert.False(commitBatchTask.Equals(obj: new object()));
        }

        [Fact]
        public void Equals_WhenObjectIsSameInstance_ReturnsTrue()
        {
            var commitBatchTask = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);

            Assert.True(commitBatchTask.Equals(obj: commitBatchTask));
            Assert.True(commitBatchTask.Equals(other: commitBatchTask));
        }

        [Fact]
        public void Equals_WhenObjectHasSamePackageId_ReturnsTrue()
        {
            var commitBatchTask0 = new CatalogCommitBatchTask(_minCommitTimeStamp, _key);
            var commitBatchTask1 = new CatalogCommitBatchTask(_minCommitTimeStamp.AddMinutes(1), _key);

            Assert.True(commitBatchTask0.Equals(obj: commitBatchTask1));
            Assert.True(commitBatchTask1.Equals(obj: commitBatchTask0));
            Assert.True(commitBatchTask0.Equals(other: commitBatchTask1));
            Assert.True(commitBatchTask1.Equals(other: commitBatchTask0));
        }

        [Fact]
        public void Equals_WhenObjectHasDifferentPackageId_ReturnsFalse()
        {
            var commitBatchTask0 = new CatalogCommitBatchTask(_minCommitTimeStamp, key: "a");
            var commitBatchTask1 = new CatalogCommitBatchTask(_minCommitTimeStamp, key: "b");

            Assert.False(commitBatchTask0.Equals(obj: commitBatchTask1));
            Assert.False(commitBatchTask1.Equals(obj: commitBatchTask0));
            Assert.False(commitBatchTask0.Equals(other: commitBatchTask1));
            Assert.False(commitBatchTask1.Equals(other: commitBatchTask0));
        }
    }
}
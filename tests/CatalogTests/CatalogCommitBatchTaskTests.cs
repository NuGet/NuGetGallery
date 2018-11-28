// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NgTests.Infrastructure;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitBatchTaskTests
    {
        private readonly DateTime _minCommitTimeStamp = DateTime.UtcNow;
        private static readonly PackageIdentity _packageIdentity = new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0"));
        private readonly CatalogCommitItemBatch _commitItemBatch;
        private readonly CatalogCommitItemBatchTask _commitItemBatchTask;

        public CatalogCommitBatchTaskTests()
        {
            _commitItemBatch = CreateCatalogCommitItemBatch(_packageIdentity.Id);
            _commitItemBatchTask = new CatalogCommitItemBatchTask(_commitItemBatch, Task.CompletedTask);
        }

        [Fact]
        public void Constructor_WhenBatchIsNull_Throws()
        {
            const CatalogCommitItemBatch batch = null;

            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogCommitItemBatchTask(batch, Task.CompletedTask));

            Assert.Equal("batch", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenBatchKeyIsNull_Throws()
        {
            var commitItemBatch = CreateCatalogCommitItemBatch(key: null);

            var exception = Assert.Throws<ArgumentException>(
                () => new CatalogCommitItemBatchTask(commitItemBatch, Task.CompletedTask));

            Assert.Equal("batch.Key", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenTaskIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new CatalogCommitItemBatchTask(_commitItemBatch, task: null));

            Assert.Equal("task", exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenArgumentIsValid_ReturnsInstance()
        {
            Assert.Same(_commitItemBatch, _commitItemBatchTask.Batch);
            Assert.NotNull(_commitItemBatchTask.Task);
        }

        [Fact]
        public void GetHashCode_Always_ReturnsBatchKeyHashCode()
        {
            Assert.Equal(_commitItemBatch.Key.GetHashCode(), _commitItemBatchTask.GetHashCode());
        }

        [Fact]
        public void Equals_WhenObjectIsNull_ReturnsFalse()
        {
            Assert.False(_commitItemBatchTask.Equals(obj: null));
            Assert.False(_commitItemBatchTask.Equals(other: null));
        }

        [Fact]
        public void Equals_WhenObjectIsNotCatalogCommitBatchTask_ReturnsFalse()
        {
            Assert.False(_commitItemBatchTask.Equals(obj: new object()));
        }

        [Fact]
        public void Equals_WhenObjectIsSameInstance_ReturnsTrue()
        {
            Assert.True(_commitItemBatchTask.Equals(obj: _commitItemBatchTask));
            Assert.True(_commitItemBatchTask.Equals(other: _commitItemBatchTask));
        }

        [Fact]
        public void Equals_WhenObjectBatchHasSameKey_ReturnsTrue()
        {
            var commitTimeStamp0 = DateTime.UtcNow;
            var commitItem0 = TestUtility.CreateCatalogCommitItem(commitTimeStamp0, _packageIdentity);
            var commitItemBatch0 = new CatalogCommitItemBatch(new[] { commitItem0 }, _packageIdentity.Id);
            var commitItemBatchTask0 = new CatalogCommitItemBatchTask(commitItemBatch0, Task.CompletedTask);
            var commitTimeStamp1 = commitTimeStamp0.AddMinutes(1);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(commitTimeStamp1, _packageIdentity);
            var commitItemBatch1 = new CatalogCommitItemBatch(new[] { commitItem1 }, _packageIdentity.Id);
            var commitItemBatchTask1 = new CatalogCommitItemBatchTask(commitItemBatch1, Task.CompletedTask);

            Assert.True(commitItemBatchTask0.Equals(obj: commitItemBatchTask1));
            Assert.True(commitItemBatchTask1.Equals(obj: commitItemBatchTask0));
            Assert.True(commitItemBatchTask0.Equals(other: commitItemBatchTask1));
            Assert.True(commitItemBatchTask1.Equals(other: commitItemBatchTask0));
        }

        [Fact]
        public void Equals_WhenObjectBatchHasDifferentKey_ReturnsFalse()
        {
            var commitTimeStamp0 = DateTime.UtcNow;
            var commitItem0 = TestUtility.CreateCatalogCommitItem(
                commitTimeStamp0,
                new PackageIdentity(id: "a", version: new NuGetVersion("1.0.0")));
            var commitItemBatch0 = new CatalogCommitItemBatch(new[] { commitItem0 }, key: "a");
            var commitItemBatchTask0 = new CatalogCommitItemBatchTask(commitItemBatch0, Task.CompletedTask);
            var commitTimeStamp1 = commitTimeStamp0.AddMinutes(1);
            var commitItem1 = TestUtility.CreateCatalogCommitItem(
                commitTimeStamp1,
                new PackageIdentity(id: "b", version: new NuGetVersion("1.0.0")));
            var commitItemBatch1 = new CatalogCommitItemBatch(new[] { commitItem1 }, key: "b");
            var commitItemBatchTask1 = new CatalogCommitItemBatchTask(commitItemBatch1, Task.CompletedTask);

            Assert.False(commitItemBatchTask0.Equals(obj: commitItemBatchTask1));
            Assert.False(commitItemBatchTask1.Equals(obj: commitItemBatchTask0));
            Assert.False(commitItemBatchTask0.Equals(other: commitItemBatchTask1));
            Assert.False(commitItemBatchTask1.Equals(other: commitItemBatchTask0));
        }

        private static CatalogCommitItemBatch CreateCatalogCommitItemBatch(string key)
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commitItem = TestUtility.CreateCatalogCommitItem(commitTimeStamp, _packageIdentity);

            return new CatalogCommitItemBatch(new[] { commitItem }, key);
        }
    }
}
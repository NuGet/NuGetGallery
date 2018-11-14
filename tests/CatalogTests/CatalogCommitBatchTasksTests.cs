// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog;
using Xunit;

namespace CatalogTests
{
    public class CatalogCommitBatchTasksTests
    {
        [Fact]
        public void Constructor_Always_ReturnsInstance()
        {
            var commitTimeStamp = DateTime.UtcNow;
            var commitBatchTasks = new CatalogCommitBatchTasks(commitTimeStamp);

            Assert.Equal(commitTimeStamp, commitBatchTasks.CommitTimeStamp);
            Assert.Empty(commitBatchTasks.BatchTasks);
        }
    }
}
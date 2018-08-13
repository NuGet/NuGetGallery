// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;
using IPackageIdGroup = System.Linq.IGrouping<string, Stats.AggregateCdnDownloadsInGallery.DownloadCountData>;

namespace Stats.AggregateCdnDownloadsInGallery
{
    public class AggregateCdnDownloadsJobTests
    {

        public class PopGroupBatch
        {
            [Theory]
            [InlineData(-1, 1)]
            [InlineData(0, 1)]
            [InlineData(1, 1)]
            [InlineData(2, 1)]
            [InlineData(3, 1)]
            [InlineData(4, 1)]
            [InlineData(5, 1)]
            [InlineData(6, 1)]
            [InlineData(7, 1)]
            [InlineData(8, 1)]
            [InlineData(9, 2)]
            [InlineData(10, 2)]
            [InlineData(11, 3)]
            [InlineData(12, 3)]
            [InlineData(13, 3)]
            [InlineData(14, 3)]
            [InlineData(15, 4)]
            [InlineData(16, 4)]
            [InlineData(17, 4)]
            public void ReturnsCorrectNumberOfPackageRegistrationGroups(int batchSize, int expectedGroupCount)
            {
                // Arrange
                var data = new Stack<IPackageIdGroup>(new[]
                {
                    new DownloadCountData { PackageId = "A", PackageVersion = "4.2.0" },
                    new DownloadCountData { PackageId = "A", PackageVersion = "4.3.0" },
                    new DownloadCountData { PackageId = "A", PackageVersion = "4.4.0" },
                    new DownloadCountData { PackageId = "A", PackageVersion = "4.5.0" },
                    new DownloadCountData { PackageId = "A", PackageVersion = "4.6.0" }, // 5 versions, 1 ID (6 total records so far)

                    new DownloadCountData { PackageId = "B", PackageVersion = "4.5.0" },
                    new DownloadCountData { PackageId = "B", PackageVersion = "4.6.0" }, // 2 versions, 1 ID (9 total records so far)

                    new DownloadCountData { PackageId = "C", PackageVersion = "4.6.0" }, // 1 version, 1 ID (11 total records)

                    new DownloadCountData { PackageId = "D", PackageVersion = "4.4.0" },
                    new DownloadCountData { PackageId = "D", PackageVersion = "4.5.0" },
                    new DownloadCountData { PackageId = "D", PackageVersion = "4.6.0" }, // 3 versions, 1 ID (15 total records)
                }.GroupBy(x => x.PackageId).OrderByDescending(x => x.Key));

                // Act
                var groupBatch = AggregateCdnDownloadsJob.PopGroupBatch(data, batchSize);

                // Assert
                Assert.Equal(expectedGroupCount, groupBatch.Count);
            }
        }
    }
}

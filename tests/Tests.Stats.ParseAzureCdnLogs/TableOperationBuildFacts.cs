// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Stats.ParseAzureCdnLogs;
using Xunit;

namespace Tests.Stats.ParseAzureCdnLogs
{
    public class TableOperationBuildFacts
    {
        public class TheCreateInsertOperationMethod
        {
            [Fact]
            public void SetsRowKey()
            {
                var entity = new CdnLogEntry { EdgeServerTimeDelivered = DateTime.UtcNow };

                Assert.Null(entity.RowKey);
                TableOperationBuilder.CreateInsertOperation(entity);
                Assert.NotNull(entity.RowKey);
            }

            [Fact]
            public void SetsPartitionKey()
            {
                var entity = new CdnLogEntry { EdgeServerTimeDelivered = DateTime.UtcNow };

                Assert.Null(entity.PartitionKey);
                TableOperationBuilder.CreateInsertOperation(entity);
                Assert.NotNull(entity.PartitionKey);
            }
        }

        public class TheGetOptimalInsertBatchOperationsMethod
        {
            [Fact]
            public void SplitsBatchOperationsByMaxBatchSize()
            {
                var entities = new List<CdnLogEntry>();
                for (int i = 0; i < 120; i++)
                {
                    var tableEntity = new CdnLogEntry { EdgeServerTimeDelivered = DateTime.UtcNow };
                    entities.Add(tableEntity);
                }

                var optimalBatchOperations = TableOperationBuilder.GetOptimalInsertBatchOperations(entities);

                Assert.Equal(2, optimalBatchOperations.Count());

                var first = optimalBatchOperations.First();
                var second = optimalBatchOperations.Last();

                Assert.Equal(100, first.Count);
                Assert.Equal(20, second.Count);
            }

            [Fact]
            public void SplitsBatchOperationsByPartitionKey()
            {
                var entities = new List<CdnLogEntry>();
                for (int i = 0; i < 120; i++)
                {
                    var tableEntity = new CdnLogEntry { EdgeServerTimeDelivered = i % 2 == 0 ? DateTime.UtcNow : DateTime.UtcNow.AddDays(-1) };
                    entities.Add(tableEntity);
                }

                var optimalBatchOperations = TableOperationBuilder.GetOptimalInsertBatchOperations(entities);

                Assert.Equal(2, optimalBatchOperations.Count());

                var first = optimalBatchOperations.First();
                var second = optimalBatchOperations.Last();

                Assert.Equal(60, first.Count);
                Assert.Equal(60, second.Count);
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Stats.AzureCdnLogs.Common;
using Stats.ParseAzureCdnLogs;
using Xunit;

namespace Tests.Stats.ParseAzureCdnLogs
{
    public class TableOperationBuildFacts
    {
        public class TheGetOptimalInsertBatchOperationsMethod
        {
            [Fact]
            public void SplitsBatchOperationsByMaxBatchSize()
            {
                var entities = new List<CdnLogEntry>();
                for (int i = 0; i < 120; i++)
                {
                    var edgeServerTimeDelivered = DateTime.UtcNow;
                    var tableEntity = new CdnLogEntry { EdgeServerTimeDelivered = edgeServerTimeDelivered };
                    // reverse chronological order of log entries
                    tableEntity.RowKey = RowKeyBuilder.CreateReverseChronological(tableEntity.EdgeServerTimeDelivered);

                    // parition by date
                    tableEntity.PartitionKey = tableEntity.EdgeServerTimeDelivered.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

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

                    // reverse chronological order of log entries
                    tableEntity.RowKey = RowKeyBuilder.CreateReverseChronological(tableEntity.EdgeServerTimeDelivered);

                    // parition by date
                    tableEntity.PartitionKey = tableEntity.EdgeServerTimeDelivered.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

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
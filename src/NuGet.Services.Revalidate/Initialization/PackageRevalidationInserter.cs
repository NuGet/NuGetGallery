// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public class PackageRevalidationInserter : IPackageRevalidationInserter
    {
        private const string TableName = "[dbo].[PackageRevalidations]";

        private const string PackageIdColumn = "PackageId";
        private const string PackageNormalizedVersionColumn = "PackageNormalizedVersion";
        private const string EnqueuedColumn = "Enqueued";
        private const string ValidationTrackingIdColumn = "ValidationTrackingId";
        private const string CompletedColumn = "Completed";

        private readonly ISqlConnectionFactory<ValidationDbConfiguration> _connectionFactory;
        private readonly ILogger<PackageRevalidationInserter> _logger;

        public PackageRevalidationInserter(
            ISqlConnectionFactory<ValidationDbConfiguration> connectionFactory,
            ILogger<PackageRevalidationInserter> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations)
        {
            _logger.LogDebug("Persisting package revalidations to database...");

            var table = PrepareTable(revalidations);

            using (var connection = await _connectionFactory.OpenAsync())
            {
                var bulkCopy = new SqlBulkCopy(
                    connection,
                    SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.UseInternalTransaction,
                    externalTransaction: null);

                foreach (DataColumn column in table.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                bulkCopy.DestinationTableName = TableName;
                bulkCopy.WriteToServer(table);
            }

            _logger.LogDebug("Finished persisting package revalidations to database...");
        }

        private DataTable PrepareTable(IReadOnlyList<PackageRevalidation> revalidations)
        {
            // Prepare the table.
            var table = new DataTable();

            table.Columns.Add(PackageIdColumn, typeof(string));
            table.Columns.Add(PackageNormalizedVersionColumn, typeof(string));
            table.Columns.Add(CompletedColumn, typeof(bool));

            var enqueued = table.Columns.Add(EnqueuedColumn, typeof(DateTime));
            var trackingId = table.Columns.Add(ValidationTrackingIdColumn, typeof(Guid));

            enqueued.AllowDBNull = true;
            trackingId.AllowDBNull = true;

            // Populate the table.
            foreach (var revalidation in revalidations)
            {
                var row = table.NewRow();

                row[PackageIdColumn] = revalidation.PackageId;
                row[PackageNormalizedVersionColumn] = revalidation.PackageNormalizedVersion;
                row[EnqueuedColumn] = ((object)revalidation.Enqueued) ?? DBNull.Value;
                row[ValidationTrackingIdColumn] = ((object)revalidation.ValidationTrackingId) ?? DBNull.Value;
                row[CompletedColumn] = revalidation.Completed;

                table.Rows.Add(row);
            }

            return table;
        }
    }
}

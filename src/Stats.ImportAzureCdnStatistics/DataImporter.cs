// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Stats.ImportAzureCdnStatistics
{
    internal class DataImporter
    {
        public enum Table
        {
            Fact_Dist_Download,
            Fact_Download
        }

        private readonly Func<Task<SqlConnection>> _openStatisticsSqlConnectionAsync;
        private const string _sqlSelectTop1FromTable = "SELECT TOP 1 * FROM [dbo].[{0}]";

        public DataImporter(Func<Task<SqlConnection>> openStatisticsSqlConnectionAsync)
        {
            _openStatisticsSqlConnectionAsync = openStatisticsSqlConnectionAsync;
        }

        public async Task<DataTable> GetDataTableAsync(Table table)
        {
            var tableName = table.ToString();
            var dataTable = new DataTable();
            var query = string.Format(_sqlSelectTop1FromTable, tableName);

            using (var connection = await _openStatisticsSqlConnectionAsync())
            {
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                var tableAdapter = new SqlDataAdapter(query, connection)
                {
                    MissingSchemaAction = MissingSchemaAction.Add
                };
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities
                tableAdapter.Fill(dataTable);
            }

            dataTable.Rows.Clear();

            // Remove Id column from in-memory data table.
            // These are auto-generated on the database upon insert.
            if (dataTable.Columns.Contains("Id"))
            {
                dataTable.PrimaryKey = null;
                dataTable.Columns.Remove("Id");
            }

            // Remove Timestamp column from in-memory data table.
            // These are auto-generated on the database upon insert.
            if (dataTable.Columns.Contains("Timestamp"))
            {
                dataTable.Columns.Remove("Timestamp");
            }

            // Remove Fact_EdgeServer_IpAddress_Id column from in-memory data table if it exists.
            if (dataTable.Columns.Contains("Fact_EdgeServer_IpAddress_Id"))
            {
                dataTable.Columns.Remove("Fact_EdgeServer_IpAddress_Id");
            }

            dataTable.TableName = $"dbo.{tableName}";
            return dataTable;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Stats.ImportAzureCdnStatistics
{
    internal class DataImporter
    {
        private readonly SqlConnectionStringBuilder _targetDatabase;
        private const string _sqlSelectTop1FromTable = "SELECT TOP 1 * FROM [dbo].[{0}]";

        public DataImporter(SqlConnectionStringBuilder targetDatabase)
        {
            _targetDatabase = targetDatabase;
        }

        public async Task<DataTable> GetDataTableAsync(string tableName)
        {
            var dataTable = new DataTable();
            var query = string.Format(_sqlSelectTop1FromTable, tableName);

            using (var connection = await _targetDatabase.ConnectTo())
            {
                var tableAdapter = new SqlDataAdapter(query, connection)
                {
                    MissingSchemaAction = MissingSchemaAction.AddWithKey
                };
                tableAdapter.Fill(dataTable);
            }

            dataTable.Rows.Clear();

            return dataTable;
        }
    }
}
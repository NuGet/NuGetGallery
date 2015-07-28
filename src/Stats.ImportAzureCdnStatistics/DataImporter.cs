// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.SqlClient;

namespace Stats.ImportAzureCdnStatistics
{
    internal class DataImporter
    {
        private const string _sqlSelectTop1FromTable = "SELECT TOP 1 * FROM [dbo].[{0}]";

        public static DataTable GetDataTable(string tableName, SqlConnection connection)
        {
            var dataTable = new DataTable();
            var query = string.Format(_sqlSelectTop1FromTable, tableName);
            var tableAdapter = new SqlDataAdapter(query, connection)
            {
                MissingSchemaAction = MissingSchemaAction.AddWithKey
            };
            tableAdapter.Fill(dataTable);

            dataTable.Rows.Clear();

            return dataTable;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Stats.ImportAzureCdnStatistics
{
    internal class DataImporter
    {
        private const string _sqlSelectTop1FromTable = "SELECT TOP 1 * FROM [dbo].[{0}]";

        public static async Task<DataTable> GetSqlTableAsync(string tableName, SqlConnection connection)
        {
            var dataTable = new DataTable();
            var query = string.Format(_sqlSelectTop1FromTable, tableName);

            using (var command = connection.CreateCommand())
            {
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                dataTable.Load(await command.ExecuteReaderAsync());
            }

            dataTable.Clear();

            return dataTable;
        }
    }
}
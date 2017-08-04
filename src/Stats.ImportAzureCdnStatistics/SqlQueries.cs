// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.SqlClient;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class SqlQueries
    {
        private const string _sqlGetAllTimeDimensions = "SELECT [Id], [HourOfDay] FROM [dbo].[Dimension_Time]";

        private const string _timeFormat = "yyyy-MM-dd";
        private const string _minParameterName = "@Min";
        private const string _maxParameterName = "@Max";
        private const string _sqlGetDateDimensions = "SELECT [Id], [Date] FROM [dbo].[Dimension_Date] WHERE [Date] >= " + _minParameterName + " AND [Date] <= " + _maxParameterName;

        public static SqlCommand GetAllTimeDimensions(SqlConnection connection, int commandTimeout)
        {
            var command = connection.CreateCommand();
            command.CommandText = _sqlGetAllTimeDimensions;
            command.CommandTimeout = commandTimeout;
            command.CommandType = CommandType.Text;

            return command;
        }

        public static SqlCommand GetDateDimensions(SqlConnection connection, int commandTimeout, DateTime min, DateTime max)
        {
            var command = connection.CreateCommand();
            command.CommandText = _sqlGetDateDimensions;

            command.Parameters.AddWithValue(_minParameterName, min.ToString(_timeFormat));
            command.Parameters.AddWithValue(_maxParameterName, max.ToString(_timeFormat));

            command.CommandTimeout = commandTimeout;
            command.CommandType = CommandType.Text;

            return command;
        }
    }
}
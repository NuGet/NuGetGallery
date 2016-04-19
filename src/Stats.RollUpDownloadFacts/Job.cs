// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;
using NuGet.Jobs;

namespace Stats.RollUpDownloadFacts
{
    public class Job
        : JobBase
    {
        private static int _minAgeInDays = 90;
        private static SqlConnectionStringBuilder _targetDatabase;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                _minAgeInDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, "MinAgeInDays", "90").Value;
                Trace.TraceInformation("Min age in days: " + _minAgeInDays);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }

        public override async Task<bool> Run()
        {
            try
            {
                using (var connection = await _targetDatabase.ConnectTo())
                {
                    connection.InfoMessage -= OnSqlConnectionInfoMessage;
                    connection.InfoMessage += OnSqlConnectionInfoMessage;

                    var sqlCommand = new SqlCommand("[dbo].[RollUpDownloadFacts]", connection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    sqlCommand.CommandTimeout = 23 * 60 * 60;
                    sqlCommand.Parameters.Add(new SqlParameter("MinAgeInDays", _minAgeInDays));

                    await sqlCommand.ExecuteScalarAsync();
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }

        private static void OnSqlConnectionInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            Trace.TraceInformation(e.Message);
        }
    }
}
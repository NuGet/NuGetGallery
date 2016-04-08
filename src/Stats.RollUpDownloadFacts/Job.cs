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
                    var sqlCommand = new SqlCommand("[dbo].[RollUpDownloadFacts]", connection);
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    sqlCommand.CommandTimeout = 23 * 60 * 60;
                    sqlCommand.Parameters.Add(new SqlParameter("MinAgeInDays", _minAgeInDays));

                    using (var dataReader = await sqlCommand.ExecuteReaderAsync())
                    {
                        while (await dataReader.ReadAsync())
                        {
                            var deletedDownloadFactRecords = dataReader.GetInt32(0);
                            var deletedProjectTypeLinks = dataReader.GetInt32(1);
                            var insertedDownloadFacts = dataReader.GetInt32(2);
                            var totalDownloadCount = dataReader.GetInt32(3);
                            var errorMessage = dataReader.GetString(4);

                            if (!string.IsNullOrEmpty(errorMessage))
                            {
                                Trace.TraceError(errorMessage);
                            }
                            else
                            {
                                Trace.TraceInformation("Total downloads " + totalDownloadCount
                                                       + ", deleted facts " + deletedDownloadFactRecords
                                                       + ", deleted links " + deletedProjectTypeLinks
                                                       + ", inserted facts " + insertedDownloadFacts);
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
                return false;
            }
        }
    }
}
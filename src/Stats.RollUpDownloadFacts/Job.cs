// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs;
using Stats.AzureCdnLogs.Common;

namespace Stats.RollUpDownloadFacts
{
    public class Job
        : JobBase
    {
        private const string _startTemplateRecordsDeletion = "Package Dimension ID ";
        private const string _endTemplateDimProjectTypeDeletion = " records from [dbo].[Fact_Download_Dimension_ProjectType]";
        private const string _endTemplateFactDownloadDeletion = " records from [dbo].[Fact_Download]";
        private static int _minAgeInDays = 90;
        private static SqlConnectionStringBuilder _targetDatabase;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = JobConfigurationManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

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
            if (string.IsNullOrEmpty(e.Message))
            {
                return;
            }

            ApplicationInsights.TrackTrace(e.Message);

            if (e.Message.StartsWith(_startTemplateRecordsDeletion) &&
                e.Message.EndsWith(_endTemplateDimProjectTypeDeletion))
            {
                var parts = e.Message
                    .Replace(_startTemplateRecordsDeletion, string.Empty)
                    .Replace(_endTemplateDimProjectTypeDeletion, string.Empty)
                    .Split(' ');

                var value = double.Parse(parts.Last());
                var packageDimensionId = parts.First().Replace(":", string.Empty);

                ApplicationInsights.TrackRollUpMetric("ProjectType Links Deleted", value, packageDimensionId);
            }
            else if (e.Message.StartsWith(_startTemplateRecordsDeletion) &&
                     e.Message.EndsWith(_endTemplateFactDownloadDeletion))
            {
                var parts = e.Message
                    .Replace(_startTemplateRecordsDeletion, string.Empty)
                    .Replace(_endTemplateFactDownloadDeletion, string.Empty)
                    .Split(' ');

                var value = double.Parse(parts.Last());
                var packageDimensionId = parts.First().Replace(":", string.Empty);

                ApplicationInsights.TrackRollUpMetric("Download Facts Deleted", value, packageDimensionId);
            }

            Trace.TraceInformation(e.Message);
        }
    }
}
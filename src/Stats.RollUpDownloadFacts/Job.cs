// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Logging;

namespace Stats.RollUpDownloadFacts
{
    public class Job
        : JobBase
    {
        private const string _startTemplateRecordsDeletion = "Package Dimension ID ";
        private const string _endTemplateDimProjectTypeDeletion = " records from [dbo].[Fact_Download_Dimension_ProjectType]";
        private const string _endTemplateFactDownloadDeletion = " records from [dbo].[Fact_Download]";
        private const int DefaultMinAgeInDays = 90;
        private static int _minAgeInDays;
        private static SqlConnectionStringBuilder _targetDatabase;
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            try
            {
                var instrumentationKey = jobArgsDictionary.GetOrNull(JobArgumentNames.InstrumentationKey);
                ApplicationInsights.Initialize(instrumentationKey);

                _loggerFactory = LoggingSetup.CreateLoggerFactory();
                _logger = _loggerFactory.CreateLogger<Job>();

                var databaseConnectionString = jobArgsDictionary[JobArgumentNames.StatisticsDatabase];
                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                _minAgeInDays = jobArgsDictionary.GetOrNull<int>(JobArgumentNames.MinAgeInDays) ?? DefaultMinAgeInDays;
                Trace.TraceInformation("Min age in days: " + _minAgeInDays);

                return true;
            }
            catch (Exception exception)
            {
                _logger.LogCritical("Job failed to initialize. {Exception}", exception);
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
                _logger.LogCritical("Job run failed. {Exception}", exception);

                return false;
            }
        }

        private void OnSqlConnectionInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Message))
            {
                return;
            }

            _logger.LogInformation(e.Message);

            if (e.Message.StartsWith(_startTemplateRecordsDeletion) &&
                e.Message.EndsWith(_endTemplateDimProjectTypeDeletion))
            {
                var parts = e.Message
                    .Replace(_startTemplateRecordsDeletion, string.Empty)
                    .Replace(_endTemplateDimProjectTypeDeletion, string.Empty)
                    .Split(' ');

                var value = double.Parse(parts.Last());
                var packageDimensionId = parts.First().Replace(":", string.Empty);

                ApplicationInsightsHelper.TrackRollUpMetric("ProjectType Links Deleted", value, packageDimensionId);
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

                ApplicationInsightsHelper.TrackRollUpMetric("Download Facts Deleted", value, packageDimensionId);
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace Stats.RollUpDownloadFacts
{
    public class Job
        : JobBase
    {
        private const string _startTemplateRecordsDeletion = "Package Dimension ID ";
        private const string _endTemplateFactDownloadDeletion = " records from [dbo].[Fact_Download]";
        private const int DefaultMinAgeInDays = 43;
        private static int _minAgeInDays;
        private static SqlConnectionStringBuilder _targetDatabase;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var databaseConnectionString = JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatisticsDatabase);
            _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

            _minAgeInDays = JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.MinAgeInDays) ?? DefaultMinAgeInDays;
            Logger.LogInformation("Min age in days: {MinAgeInDays}", _minAgeInDays);
        }

        public override async Task Run()
        {
            using (var connection = await _targetDatabase.ConnectTo())
            {
                connection.InfoMessage -= OnSqlConnectionInfoMessage;
                connection.InfoMessage += OnSqlConnectionInfoMessage;

                var sqlCommand = new SqlCommand("[dbo].[RollUpDownloadFacts]", connection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.CommandTimeout = 23 * 60 * 60;
                sqlCommand.Parameters.AddWithValue("MinAgeInDays", _minAgeInDays);

                await sqlCommand.ExecuteScalarAsync();
            }
        }

        private void OnSqlConnectionInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Message))
            {
                return;
            }

            Logger.LogInformation("SqlConnection info message: {Message}", e.Message);

            if (e.Message.StartsWith(_startTemplateRecordsDeletion) &&
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
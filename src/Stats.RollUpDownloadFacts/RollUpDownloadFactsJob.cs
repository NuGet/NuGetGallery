// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace Stats.RollUpDownloadFacts
{
    public class RollUpDownloadFactsJob : JsonConfigurationJob
    {
        private const string _startTemplateRecordsDeletion = "Package Dimension ID ";
        private const string _endTemplateFactDownloadDeletion = " records from [dbo].[Fact_Download]";
        private const int DefaultMinAgeInDays = 43;

        private RollUpDownloadFactsConfiguration _configuration;
        private ApplicationInsightsHelper _applicationInsightsHelper;
        private int _minAgeInDays;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<RollUpDownloadFactsConfiguration>>().Value;

            _minAgeInDays = _configuration.MinAgeInDays ?? DefaultMinAgeInDays;
            Logger.LogInformation("Min age in days: {MinAgeInDays}", _minAgeInDays);
            _applicationInsightsHelper = new ApplicationInsightsHelper(ApplicationInsightsConfiguration.TelemetryConfiguration);
        }

        public override async Task Run()
        {
            using (var connection = await OpenSqlConnectionAsync<StatisticsDbConfiguration>())
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

                _applicationInsightsHelper.TrackRollUpMetric("Download Facts Deleted", value, packageDimensionId);
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<RollUpDownloadFactsConfiguration>(services, configurationRoot);
        }
    }
}
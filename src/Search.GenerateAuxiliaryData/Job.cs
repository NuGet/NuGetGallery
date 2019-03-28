// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace Search.GenerateAuxiliaryData
{
    public class Job : JsonConfigurationJob
    {
        private const string DefaultContainerName = "ng-search-data";

        private const string ScriptOwners = "SqlScripts.Owners.sql";
        private const string OutputNameOwners = "owners.json";
        private const string Col0Owners = "Id";
        private const string Col1Owners = "UserName";

        private const string ScriptRankingsTotal = "SqlScripts.Rankings.sql";
        private const string OutputNameRankings = "rankings.v1.json";

        private const string ScriptVerifiedPackages = "SqlScripts.VerifiedPackages.sql";
        private const string OutputNameVerifiedPackages = "verifiedPackages.json";

        private const string StatisticsReportName = "downloads.v1.json";

        private List<Exporter> _exportersToRun;

        private InitializationConfiguration Configuration { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            Configuration = _serviceProvider.GetRequiredService<IOptionsSnapshot<InitializationConfiguration>>().Value;

            var destinationContainer = CloudStorageAccount.Parse(Configuration.PrimaryDestination)
                .CreateCloudBlobClient()
                .GetContainerReference(Configuration.DestinationContainerName ?? DefaultContainerName);

            var statisticsContainer = CloudStorageAccount.Parse(Configuration.AzureCdnCloudStorageAccount)
                .CreateCloudBlobClient()
                .GetContainerReference(Configuration.AzureCdnCloudStorageContainerName);

            _exportersToRun = new List<Exporter> {
                new VerifiedPackagesExporter(
                    LoggerFactory.CreateLogger<VerifiedPackagesExporter>(),
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    destinationContainer,
                    ScriptVerifiedPackages,
                    OutputNameVerifiedPackages,
                    Configuration.SqlCommandTimeout),

                new NestedJArrayExporter(
                    LoggerFactory.CreateLogger<NestedJArrayExporter>(),
                    OpenSqlConnectionAsync<GalleryDbConfiguration>,
                    destinationContainer,
                    ScriptOwners,
                    OutputNameOwners,
                    Col0Owners,
                    Col1Owners,
                    Configuration.SqlCommandTimeout),

                new RankingsExporter(
                    LoggerFactory.CreateLogger<RankingsExporter>(),
                    OpenSqlConnectionAsync<StatisticsDbConfiguration>,
                    destinationContainer,
                    ScriptRankingsTotal,
                    OutputNameRankings,
                    Configuration.SqlCommandTimeout),

                new BlobStorageExporter(
                    LoggerFactory.CreateLogger<BlobStorageExporter>(),
                    statisticsContainer,
                    StatisticsReportName,
                    destinationContainer,
                    StatisticsReportName)
            };
        }

        public override async Task Run()
        {
            var failedExporters = new List<string>();

            foreach (Exporter exporter in _exportersToRun)
            {
                try
                {
                    await exporter.ExportAsync();
                }
                catch (Exception e)
                {
                    var exporterName = exporter.GetType().Name;
                    Logger.LogError("SQL exporter '{ExporterName}' failed: {Exception}", exporterName, e);
                    failedExporters.Add(exporterName);
                }
            }
            
            if (failedExporters.Any())
            {
                throw new ExporterException($"{failedExporters.Count()} tasks failed: {string.Join(", ", failedExporters)}");
            }
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<InitializationConfiguration>(services, configurationRoot);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using NuGet.Services.Incidents;
using StatusAggregator.Parse;
using StatusAggregator.Table;

namespace StatusAggregator
{
    public class Job : JobBase
    {
        public IServiceProvider _serviceProvider;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var serviceCollection = new ServiceCollection();

            AddLogging(serviceCollection);
            AddConfiguration(serviceCollection, jobArgsDictionary);
            AddStorage(serviceCollection);
            AddServices(serviceCollection);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        public override Task Run()
        {
            return _serviceProvider
                .GetRequiredService<StatusAggregator>()
                .Run();
        }

        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ICursor, Cursor>();
            serviceCollection.AddSingleton<IIncidentApiClient, IncidentApiClient>();
            serviceCollection.AddTransient<IMessageUpdater, MessageUpdater>();
            serviceCollection.AddTransient<IEventUpdater, EventUpdater>();
            serviceCollection.AddTransient<IIncidentFactory, IncidentFactory>();
            AddParsing(serviceCollection);
            serviceCollection.AddTransient<IIncidentUpdater, IncidentUpdater>();
            serviceCollection.AddTransient<IStatusUpdater, StatusUpdater>();
            serviceCollection.AddTransient<IStatusExporter, StatusExporter>();
            serviceCollection.AddTransient<StatusAggregator>();
        }

        private static void AddParsing(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IIncidentParsingFilter, SeverityFilter>();
            serviceCollection.AddTransient<IIncidentParsingFilter, EnvironmentFilter>();

            serviceCollection.AddTransient<IIncidentParser, OutdatedSearchServiceInstanceIncidentParser>();
            serviceCollection.AddTransient<IIncidentParser, PingdomIncidentParser>();
            serviceCollection.AddTransient<IIncidentParser, ValidationDurationIncidentParser>();

            serviceCollection.AddTransient<IAggregateIncidentParser, AggregateIncidentParser>();
        }

        private static void AddStorage(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(
                serviceProvider =>
                {
                    var configuration = serviceProvider.GetRequiredService<StatusAggregatorConfiguration>();
                    return CloudStorageAccount.Parse(configuration.StorageAccount);
                });

            serviceCollection.AddSingleton<ITableWrapper>(
                serviceProvider =>
                {
                    var storageAccount = serviceProvider.GetRequiredService<CloudStorageAccount>();
                    var configuration = serviceProvider.GetRequiredService<StatusAggregatorConfiguration>();
                    return new TableWrapper(storageAccount, configuration.TableName);
                });

            serviceCollection.AddSingleton(
                serviceProvider =>
                {
                    var storageAccount = serviceProvider.GetRequiredService<CloudStorageAccount>();
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    var configuration = serviceProvider.GetRequiredService<StatusAggregatorConfiguration>();
                    return blobClient.GetContainerReference(configuration.ContainerName);
                });
        }

        private const int _defaultEventStartMessageDelayMinutes = 15;
        private const int _defaultEventEndDelayMinutes = 10;
        private const int _defaultEventVisibilityPeriod = 10;

        private static void AddConfiguration(IServiceCollection serviceCollection, IDictionary<string, string> jobArgsDictionary)
        {
            var configuration = new StatusAggregatorConfiguration()
            {
                StorageAccount = 
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusStorageAccount),
                ContainerName = 
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusContainerName),
                TableName = 
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusTableName),
                Environments = 
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusEnvironment)
                    .Split(';'),
                MaximumSeverity = 
                    JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.StatusMaximumSeverity) 
                    ?? int.MaxValue,
                TeamId = 
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiTeamId),
                EventStartMessageDelayMinutes = 
                    JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.StatusEventStartMessageDelayMinutes) 
                    ?? _defaultEventStartMessageDelayMinutes,
                EventEndDelayMinutes = 
                    JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.StatusEventEndDelayMinutes) 
                    ?? _defaultEventEndDelayMinutes,
                EventVisibilityPeriodDays = 
                    JobConfigurationManager.TryGetIntArgument(jobArgsDictionary, JobArgumentNames.StatusEventVisibilityPeriodDays) 
                    ?? _defaultEventVisibilityPeriod,
            };
            
            serviceCollection.AddSingleton(configuration);

            var incidentApiConfiguration = new IncidentApiConfiguration()
            {
                BaseUri = 
                    new Uri(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiBaseUri)),
                Certificate = 
                    GetCertificateFromJson(JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusIncidentApiCertificate))
            };

            serviceCollection.AddSingleton(incidentApiConfiguration);
        }

        private void AddLogging(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton(LoggerFactory);
            serviceCollection.AddLogging();
        }

        private static X509Certificate2 GetCertificateFromJson(string certJson)
        {
            var certJObject = JObject.Parse(certJson);

            var certData = certJObject["Data"].Value<string>();
            var certPassword = certJObject["Password"].Value<string>();

            var certBytes = Convert.FromBase64String(certData);
            return new X509Certificate2(certBytes, certPassword);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Manual;
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
            AddServices(serviceCollection);

            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(serviceCollection);

            AddStorage(containerBuilder);

            _serviceProvider = new AutofacServiceProvider(containerBuilder.Build());
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
            AddManualStatusChangeHandling(serviceCollection);
            serviceCollection.AddTransient<IStatusUpdater, StatusUpdater>();
            serviceCollection.AddTransient<IStatusExporter, StatusExporter>();
            serviceCollection.AddTransient<StatusAggregator>();
        }

        private static void AddManualStatusChangeHandling(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IManualStatusChangeHandler<AddStatusEventManualChangeEntity>, AddStatusEventManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler<EditStatusEventManualChangeEntity>, EditStatusEventManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler<DeleteStatusEventManualChangeEntity>, DeleteStatusEventManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler<AddStatusMessageManualChangeEntity>, AddStatusMessageManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler<EditStatusMessageManualChangeEntity>, EditStatusMessageManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler<DeleteStatusMessageManualChangeEntity>, DeleteStatusMessageManualChangeHandler>();
            serviceCollection.AddTransient<IManualStatusChangeHandler, ManualStatusChangeHandler>();
        }

        private static void AddParsing(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IIncidentParsingFilter, SeverityFilter>();
            serviceCollection.AddTransient<IIncidentParsingFilter, EnvironmentFilter>();

            serviceCollection.AddTransient<IIncidentParser, OutdatedSearchServiceInstanceIncidentParser>();
            serviceCollection.AddTransient<IIncidentParser, PingdomIncidentParser>();
            serviceCollection.AddTransient<IIncidentParser, ValidationDurationIncidentParser>();
            serviceCollection.AddTransient<IIncidentParser, TrafficManagerEndpointStatusIncidentParser>();

            serviceCollection.AddTransient<IAggregateIncidentParser, AggregateIncidentParser>();
        }

        private const string StorageAccountNameParameter = "name";

        private const string PrimaryStorageAccountName = "Primary";
        private const string SecondaryStorageAccountName = "Secondary";

        private static void AddStorage(ContainerBuilder containerBuilder)
        {
            var statusStorageConnectionBuilders = new StatusStorageConnectionBuilder[]
            {
                new StatusStorageConnectionBuilder(PrimaryStorageAccountName, configuration => configuration.StorageAccount),
                new StatusStorageConnectionBuilder(SecondaryStorageAccountName, configuration => configuration.StorageAccountSecondary)
            };
            
            // Add all storages to the container by name.
            foreach (var statusStorageConnectionBuilder in 
                // Register the primary storage last, so it will be the default and will be used unless a specific storage is referenced.
                statusStorageConnectionBuilders.OrderBy(b => b.Name == PrimaryStorageAccountName))
            {
                var name = statusStorageConnectionBuilder.Name;
                
                containerBuilder
                    .Register(ctx => GetCloudStorageAccount(ctx, statusStorageConnectionBuilder))
                    .As<CloudStorageAccount>()
                    .Named<CloudStorageAccount>(name);

                containerBuilder
                    .Register(ctx =>
                    {
                        var storageAccount = ctx.ResolveNamed<CloudStorageAccount>(name);
                        return GetTableWrapper(ctx, storageAccount);
                    })
                    .As<ITableWrapper>()
                    .Named<ITableWrapper>(name);

                containerBuilder
                    .Register(ctx =>
                    {
                        var storageAccount = ctx.ResolveNamed<CloudStorageAccount>(name);
                        return GetCloudBlobContainer(ctx, storageAccount);
                    })
                    .As<CloudBlobContainer>()
                    .Named<CloudBlobContainer>(name);

                // We need to listen to manual status change updates from each storage.
                containerBuilder
                    .RegisterType<ManualStatusChangeUpdater>()
                    .WithParameter(new NamedParameter(StorageAccountNameParameter, name))
                    .WithParameter(new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ITableWrapper),
                        (pi, ctx) => ctx.ResolveNamed<ITableWrapper>(name)))
                    .As<IManualStatusChangeUpdater>()
                    .Named<IManualStatusChangeUpdater>(name);
            }
        }

        private static CloudStorageAccount GetCloudStorageAccount(IComponentContext ctx, StatusStorageConnectionBuilder statusStorageConnectionBuilder)
        {
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            return CloudStorageAccount.Parse(statusStorageConnectionBuilder.GetConnectionString(configuration));
        }

        private static ITableWrapper GetTableWrapper(IComponentContext ctx, CloudStorageAccount storageAccount)
        {
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            return new TableWrapper(storageAccount, configuration.TableName);
        }

        private static CloudBlobContainer GetCloudBlobContainer(IComponentContext ctx, CloudStorageAccount storageAccount)
        {
            var blobClient = storageAccount.CreateCloudBlobClient();
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            return blobClient.GetContainerReference(configuration.ContainerName);
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
                StorageAccountSecondary =
                    JobConfigurationManager.GetArgument(jobArgsDictionary, JobArgumentNames.StatusStorageAccountSecondary),
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

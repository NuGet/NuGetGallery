// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs;
using NuGet.Services.Incidents;
using NuGet.Services.Status.Table;
using NuGet.Services.Status.Table.Manual;
using StatusAggregator.Collector;
using StatusAggregator.Container;
using StatusAggregator.Export;
using StatusAggregator.Factory;
using StatusAggregator.Manual;
using StatusAggregator.Messages;
using StatusAggregator.Parse;
using StatusAggregator.Table;
using StatusAggregator.Update;

namespace StatusAggregator
{
    public class Job : JsonConfigurationJob
    {
        private const string StatusAggregatorSectionName = "StatusAggregator";
        private const string IncidentApiSectionName = "IncidentApi";

        private const string StorageAccountNameParameter = "name";

        private const string PrimaryStorageAccountName = "Primary";
        private const string SecondaryStorageAccountName = "Secondary";

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            AddStorage(containerBuilder);
            AddFactoriesAndUpdaters(containerBuilder);
            AddIncidentRegexParser(containerBuilder);
            AddExporters(containerBuilder);
            AddEntityCollector(containerBuilder);
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            AddConfiguration(services, configurationRoot);
            AddServices(services);
        }

        public override Task Run()
        {
            return _serviceProvider
                .GetRequiredService<StatusAggregator>()
                .Run(DateTime.UtcNow);
        }

        private static void AddServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ICursor, Cursor>();
            serviceCollection.AddSingleton<IIncidentApiClient, IncidentApiClient>();
            AddParsing(serviceCollection);
            serviceCollection.AddTransient<IEntityCollectorProcessor, IncidentEntityCollectorProcessor>();
            AddManualStatusChangeHandling(serviceCollection);
            AddMessaging(serviceCollection);
            serviceCollection.AddTransient<IComponentFactory, NuGetServiceComponentFactory>();
            serviceCollection.AddTransient<IStatusUpdater, StatusUpdater>();
            serviceCollection.AddTransient<StatusAggregator>();
        }

        private static void AddMessaging(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMessageContentBuilder, MessageContentBuilder>();
            serviceCollection.AddTransient<IMessageFactory, MessageFactory>();
            serviceCollection.AddTransient<IMessageChangeEventIterator, MessageChangeEventIterator>();
            serviceCollection.AddTransient<IMessageChangeEventProcessor, MessageChangeEventProcessor>();
            serviceCollection.AddTransient<IIncidentGroupMessageFilter, IncidentGroupMessageFilter>();
            serviceCollection.AddTransient<IMessageChangeEventProvider, MessageChangeEventProvider>();
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
            serviceCollection.AddTransient<IIncidentRegexParsingFilter, SeverityRegexParsingFilter>();
            serviceCollection.AddTransient<IIncidentRegexParsingFilter, EnvironmentRegexParsingFilter>();

            serviceCollection.AddTransient<IIncidentRegexParsingHandler, AIAvailabilityIncidentRegexParsingHandler>();
            serviceCollection.AddTransient<IIncidentRegexParsingHandler, OutdatedSearchServiceInstanceIncidentRegexParsingHandler>();
            serviceCollection.AddTransient<IIncidentRegexParsingHandler, PingdomIncidentRegexParsingHandler>();
            serviceCollection.AddTransient<IIncidentRegexParsingHandler, ValidationDurationIncidentRegexParsingHandler>();
            serviceCollection.AddTransient<IIncidentRegexParsingHandler, TrafficManagerEndpointStatusIncidentRegexParsingHandler>();

            serviceCollection.AddTransient<IAggregateIncidentParser, AggregateIncidentParser>();
        }

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
                    .Register(ctx => GetTableServiceClient(ctx, statusStorageConnectionBuilder))
                    .As<TableServiceClient>()
                    .Named<TableServiceClient>(name);

                containerBuilder
                    .Register(ctx =>
                    {
                        var tableServiceClient = ctx.ResolveNamed<TableServiceClient>(name);
                        return GetTableWrapper(ctx, tableServiceClient);
                    })
                    .As<ITableWrapper>()
                    .Named<ITableWrapper>(name);

                containerBuilder
                    .Register(ctx => GetBlobServiceClient(ctx, statusStorageConnectionBuilder))
                    .As<BlobServiceClient>()
                    .Named<BlobServiceClient>(name);

                containerBuilder
                    .Register(ctx =>
                    {
                        var blobServiceClient = ctx.ResolveNamed<BlobServiceClient>(name);
                        return GetContainerWrapper(ctx, blobServiceClient);
                    })
                    .As<IContainerWrapper>()
                    .Named<IContainerWrapper>(name);

                // We need to listen to manual status change updates from each storage.
                containerBuilder
                    .RegisterType<ManualStatusChangeCollectorProcessor>()
                    .WithParameter(new NamedParameter(StorageAccountNameParameter, name))
                    .WithParameter(new ResolvedParameter(
                        (pi, ctx) => pi.ParameterType == typeof(ITableWrapper),
                        (pi, ctx) => ctx.ResolveNamed<ITableWrapper>(name)))
                    .As<IEntityCollectorProcessor>();
            }
        }

        private static string GetConnectionString(IComponentContext ctx, StatusStorageConnectionBuilder statusStorageConnectionBuilder)
        {
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            var connectionString = statusStorageConnectionBuilder.GetConnectionString(configuration);

            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            connectionString = connectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");
            return connectionString;
        }

        private static TableServiceClient GetTableServiceClient(IComponentContext ctx, StatusStorageConnectionBuilder statusStorageConnectionBuilder)
        {
            string connectionString = GetConnectionString(ctx, statusStorageConnectionBuilder);
            return new TableServiceClient(connectionString);
        }

        private static ITableWrapper GetTableWrapper(IComponentContext ctx, TableServiceClient tableServiceClient)
        {
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            return new TableWrapper(tableServiceClient, configuration.TableName);
        }

        private static BlobServiceClient GetBlobServiceClient(IComponentContext ctx, StatusStorageConnectionBuilder statusStorageConnectionBuilder)
        {
            string connectionString = GetConnectionString(ctx, statusStorageConnectionBuilder);
            return new BlobServiceClient(connectionString);
        }

        private static IContainerWrapper GetContainerWrapper(IComponentContext ctx, BlobServiceClient blobServiceClient)
        {
            var configuration = ctx.Resolve<StatusAggregatorConfiguration>();
            var container = blobServiceClient.GetBlobContainerClient(configuration.ContainerName);
            return new ContainerWrapper(container);
        }

        private static void AddFactoriesAndUpdaters(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterType<AggregationStrategy<IncidentEntity, IncidentGroupEntity>>()
                .As<IAggregationStrategy<IncidentGroupEntity>>();

            containerBuilder
                .RegisterType<AggregationStrategy<IncidentGroupEntity, EventEntity>>()
                .As<IAggregationStrategy<EventEntity>>();

            containerBuilder
                .RegisterType<IncidentAffectedComponentPathProvider>()
                .As<IAffectedComponentPathProvider<IncidentEntity>>()
                .As<IAffectedComponentPathProvider<IncidentGroupEntity>>();

            containerBuilder
                .RegisterType<EventAffectedComponentPathProvider>()
                .As<IAffectedComponentPathProvider<EventEntity>>();

            containerBuilder
                .RegisterType<AggregationProvider<IncidentEntity, IncidentGroupEntity>>()
                .As<IAggregationProvider<IncidentEntity, IncidentGroupEntity>>();

            containerBuilder
                .RegisterType<AggregationProvider<IncidentGroupEntity, EventEntity>>()
                .As<IAggregationProvider<IncidentGroupEntity, EventEntity>>();

            containerBuilder
                .RegisterType<IncidentFactory>()
                .As<IComponentAffectingEntityFactory<IncidentEntity>>();

            containerBuilder
                .RegisterType<IncidentGroupFactory>()
                .As<IComponentAffectingEntityFactory<IncidentGroupEntity>>();

            containerBuilder
                .RegisterType<EventFactory>()
                .As<IComponentAffectingEntityFactory<EventEntity>>();

            containerBuilder
                .RegisterType<IncidentUpdater>()
                .As<IComponentAffectingEntityUpdater<IncidentEntity>>();

            containerBuilder
                .RegisterType<AggregationEntityUpdater<IncidentEntity, IncidentGroupEntity>>()
                .As<IComponentAffectingEntityUpdater<IncidentGroupEntity>>();
            
            AddEventUpdater(containerBuilder);

            containerBuilder
                .RegisterType<ActiveEventEntityUpdater>()
                .As<IActiveEventEntityUpdater>();
        }

        private static void AddEventUpdater(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterType<AggregationEntityUpdater<IncidentGroupEntity, EventEntity>>()
                .AsSelf();

            containerBuilder
                .RegisterType<EventMessagingUpdater>()
                .AsSelf();

            containerBuilder
                .RegisterType<EventUpdater>()
                .As<IComponentAffectingEntityUpdater<EventEntity>>();
        }

        private static void AddIncidentRegexParser(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterAdapter<IIncidentRegexParsingHandler, IIncidentParser>(
                    (ctx, handler) =>
                    {
                        return new IncidentRegexParser(
                            handler,
                            ctx.Resolve<ILogger<IncidentRegexParser>>());
                    });
        }

        private static void AddEntityCollector(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterAdapter<IEntityCollectorProcessor, IEntityCollector>(
                    (ctx, processor) =>
                    {
                        return new EntityCollector(
                            ctx.Resolve<ICursor>(),
                            processor);
                    });
        }

        private static void AddExporters(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterType<ComponentExporter>()
                .As<IComponentExporter>();

            containerBuilder
                .RegisterType<EventExporter>()
                .As<IEventExporter>();

            containerBuilder
                .RegisterType<EventsExporter>()
                .As<IEventsExporter>();

            containerBuilder
                .RegisterType<StatusSerializer>()
                .As<IStatusSerializer>();

            containerBuilder
                .RegisterType<StatusExporter>()
                .As<IStatusExporter>();
        }

        private void AddConfiguration(IServiceCollection serviceCollection, IConfigurationRoot root)
        {
            serviceCollection.Configure<StatusAggregatorConfiguration>(root.GetSection(StatusAggregatorSectionName));
            serviceCollection.AddSingleton(x => x.GetRequiredService<IOptionsSnapshot<StatusAggregatorConfiguration>>().Value);

            serviceCollection.Configure<RawIncidentApiConfiguration>(root.GetSection(IncidentApiSectionName));
            serviceCollection.AddSingleton(x =>
            {
                var raw = x.GetRequiredService<IOptionsSnapshot<RawIncidentApiConfiguration>>();
                return new IncidentApiConfiguration
                {
                    BaseUri = new Uri(raw.Value.BaseUri),
                    Certificate = GetCertificateFromConfiguration(raw.Value.Certificate, Logger)
                };
            });
        }

        public static X509Certificate2 GetCertificateFromConfiguration(string certSecret, ILogger logger)
        {
            // Certificates are persisted in two different ways in KeyVault.
            // Try both before failing.
            logger.LogInformation("Parsing certificate from configuration.");
            X509Certificate2 certificate;
            try
            {
                // Legacy KeyVault certificates are stored as JSON objects with Base64 data and a password.
                logger.LogInformation("Attempting to parse certificate as JSON.");
                var certJObject = JObject.Parse(certSecret);

                var certData = certJObject["Data"].Value<string>();
                var certPassword = certJObject["Password"].Value<string>();

                var certBytes = Convert.FromBase64String(certData);
                certificate = new X509Certificate2(certBytes, certPassword);
            }
            catch (JsonReaderException)
            {
                // New KeyVault certificates are stored as Base64 strings and have no password.
                logger.LogInformation("Failed to parse certificate as JSON. Attempting to parse certificate as Base64.");
                var certBytes = Convert.FromBase64String(certSecret);
                certificate = new X509Certificate2(certBytes);
            }

            logger.LogInformation("Successfully parsed certificate with SHA-1 thumbprint {Thumbprint}", certificate.Thumbprint);
            return certificate;
        }
    }
}

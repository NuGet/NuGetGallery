// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Registration;
using NuGet.Services.Storage;
using Serilog.Events;

namespace NuGet.Services.V3PerPackage
{
    public class Program
    {
        private const int WorkerCount = 16;
        private const int MessageCount = 8192;
        private const int BatchSize = 20;

        private const string ConfigurationSectionName = "V3PerPackage";
        private const string UnexpectedExceptionMessage = "The process threw an unexpected exception.";
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        public static void Main(string[] args)
        {
            ILogger<Program> logger = null;
            try
            {
                var serviceProvider = InitializeAndGetServiceProvider();
                logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                var main = new CommandLineApplication();
                main.HelpOption();

                main.Command(
                    "process",
                    command =>
                    {
                        command.Description = $"Process {MessageCount} messages using {WorkerCount} parallel workers.";
                        command.HelpOption();
                        command.OnExecute(async () =>
                        {
                            using (serviceProvider.GetRequiredService<ControlledDisposeHttpClientHandler>().GetDisposable())
                            {
                                var perProcessContext = serviceProvider.GetRequiredService<PerProcessContext>();
                                var perProcessProcessor = serviceProvider.GetRequiredService<PerProcessProcessor>();
                                await perProcessProcessor.ProcessAsync(perProcessContext);
                                await perProcessProcessor.CleanUpAsync(perProcessContext);
                            }
                        });
                    });

                main.Command(
                    "enqueue",
                    command =>
                    {
                        command.Description = "Enqueue messages to be processed by reading the catalog.";
                        command.HelpOption();
                        var restartOption = command.Option(
                            "--restart",
                            "Restart the enqueueing from the beginning of the catalog.",
                            CommandOptionType.NoValue);

                        command.OnExecute(async () =>
                        {
                            var implementation = serviceProvider.GetRequiredService<EnqueueCommand>();

                            await implementation.ExecuteAsync(restartOption.HasValue());
                        });
                    });

                main.Command(
                    "cleanup",
                    command =>
                    {
                        command.Description = "Clean up blobs and containers left over by crashed processes.";
                        command.HelpOption();

                        command.OnExecute(async () =>
                        {
                            var implementation = serviceProvider.GetRequiredService<CleanUpCommand>();

                            await implementation.ExecuteAsync();
                        });
                    });

                main.Execute(args);
            }
            catch (Exception ex)
            {
                if (logger == null)
                {
                    Console.Error.WriteLine(UnexpectedExceptionMessage);
                    Console.Error.WriteLine(ex);
                }
                else
                {
                    logger.LogError(0, ex, UnexpectedExceptionMessage);
                }
            }

            Trace.Close();
            TelemetryConfiguration.Active.TelemetryChannel.Flush();
        }

        private static IServiceProvider InitializeAndGetServiceProvider()
        {
            ServicePointManager.DefaultConnectionLimit = 64;
            ServicePointManager.SecurityProtocol &= ~SecurityProtocolType.Ssl3;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var configurationRoot = GetConfigurationRoot();
            var instrumentationKey = configurationRoot
                .GetSection(ConfigurationSectionName)
                .GetValue<string>(nameof(V3PerPackageConfiguration.InstrumentationKey));
            ApplicationInsights.Initialize(instrumentationKey);

            var loggerConfiguration = LoggingSetup.CreateDefaultLoggerConfiguration(withConsoleLogger: true);
            var loggerFactory = LoggingSetup.CreateLoggerFactory(loggerConfiguration, LogEventLevel.Information);

            var serviceCollection = new ServiceCollection();

            serviceCollection.Configure<V3PerPackageConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            serviceCollection.AddOptions();
            serviceCollection.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));

            serviceCollection.AddLogging();
            serviceCollection.AddSingleton(loggerFactory);

            serviceCollection.AddSingleton(new ControlledDisposeHttpClientHandler());
            serviceCollection.AddTransient<HttpMessageHandler>(x => x.GetRequiredService<ControlledDisposeHttpClientHandler>());
            serviceCollection.AddTransient<Func<HttpMessageHandler>>(x => () => x.GetRequiredService<HttpMessageHandler>());
            serviceCollection.AddTransient<PerBatchProcessor>();
            serviceCollection.AddTransient<ITelemetryService, TelemetryService>();
            serviceCollection.AddTransient<PerWorkerProcessor>();
            serviceCollection.AddTransient<PerProcessProcessor>();
            serviceCollection.AddSingleton<StringLocker>();
            serviceCollection.AddTransient<EnqueueCollector>();
            serviceCollection.AddTransient<EnqueueCommand>();
            serviceCollection.AddTransient<CleanUpCommand>();

            serviceCollection.AddSingleton(x =>
            {
                var globalContext = x.GetRequiredService<GlobalContext>();

                var perProcessContext = new PerProcessContext(
                    globalContext,
                    UniqueName.New("process"),
                    WorkerCount,
                    MessageCount,
                    BatchSize);

                var blobClient = BlobStorageUtilities.GetBlobClient(globalContext);
                var flatContainerUrl = $"{blobClient.BaseUri.AbsoluteUri}/{globalContext.FlatContainerContainerName}/{perProcessContext.Name}";
                RegistrationMakerCatalogItem.PackagePathProvider = new FlatContainerPackagePathProvider(flatContainerUrl);

                return perProcessContext;
            });

            serviceCollection.AddTransient(x =>
            {
                var settings = x.GetRequiredService<IOptionsSnapshot<V3PerPackageConfiguration>>();
                return new GlobalContext(
                    settings.Value.StorageBaseAddress,
                    settings.Value.StorageAccountName,
                    settings.Value.StorageKeyValue,
                    settings.Value.ContentBaseAddress);
            });

            serviceCollection.AddSingleton(new TelemetryClient());

            serviceCollection.AddTransient<IStorageQueue<PackageMessage>>(x =>
            {
                var globalContext = x.GetRequiredService<GlobalContext>();
                var storageCredentials = new StorageCredentials(globalContext.StorageAccountName, globalContext.StorageKeyValue);
                var storageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);

                return new StorageQueue<PackageMessage>(
                   new AzureStorageQueue(storageAccount, "v3perpackage"),
                   new JsonMessageSerializer<PackageMessage>(JsonSerializerUtility.SerializerSettings),
                   PackageMessage.Version);
            });

            return serviceCollection.BuildServiceProvider();
        }

        private static IConfigurationRoot GetConfigurationRoot()
        {
            var configurationFilename = "Settings.json";
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }
    }
}

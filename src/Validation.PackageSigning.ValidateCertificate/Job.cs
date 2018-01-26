// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Configuration;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.PackageSigning.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.ServiceBus;
using NuGet.Services.Storage;
using NuGet.Services.Validation;

namespace Validation.PackageSigning.ValidateCertificate
{
    internal class Job : JobBase
    {
        /// <summary>
        /// The argument this job uses to determine the configuration file's path.
        /// </summary>
        private const string ConfigurationArgument = "Configuration";
        
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string CertificateStoreConfigurationSectionName = "CertificateStore";

        /// <summary>
        /// The maximum time that a KeyVault secret will be cached for.
        /// </summary>
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        /// <summary>
        /// The maximum amount of time that graceful shutdown can take before the job will
        /// forcefully end itself.
        /// </summary>
        private static readonly TimeSpan MaxShutdownTime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The configured service provider, used to instiate the services this job depends on.
        /// </summary>
        private IServiceProvider _serviceProvider;

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);

            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));
        }

        public async override Task Run()
        {
            var processor = _serviceProvider.GetRequiredService<ISubscriptionProcessor<CertificateValidationMessage>>();

            processor.Start();

            // Wait a day, and then shutdown this process so that it is restarted.
            await Task.Delay(TimeSpan.FromDays(1));
            
            if (!await processor.ShutdownAsync(MaxShutdownTime))
            {
                Logger.LogWarning(
                    "Failed to gracefully shutdown Service Bus subscription processor. {MessagesInProgress} messages left",
                    processor.NumberOfMessagesInProgress);
            }
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);

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

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            var services = new ServiceCollection();

            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // Use the custom NonCachingOptionsSnapshot so that KeyVault secret injection works properly.
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<CertificateStoreConfiguration>(configurationRoot.GetSection(CertificateStoreConfigurationSectionName));

            services.AddScoped<IValidationEntitiesContext>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>().Value;

                return new ValidationEntitiesContext(config.ConnectionString);
            });

            services.AddTransient<ISubscriptionClient>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

                return new SubscriptionClientWrapper(config.ConnectionString, config.TopicPath, config.SubscriptionName);
            });

            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();
            services.AddTransient<IMessageHandler<CertificateValidationMessage>, CertificateValidationMessageHandler>();

            services.AddTransient<ICertificateStore>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<CertificateStoreConfiguration>>().Value;
                var targetStorageAccount = CloudStorageAccount.Parse(config.DataStorageAccount);

                var storageFactory = new AzureStorageFactory(targetStorageAccount, config.ContainerName, LoggerFactory.CreateLogger<AzureStorage>());
                var storage = storageFactory.Create();

                return new CertificateStore(storage, LoggerFactory.CreateLogger<CertificateStore>());
            });

            services.AddTransient<ICertificateValidationService, CertificateValidationService>();
            services.AddTransient<ITelemetryService, TelemetryService>();
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            const string validateCertificateBindingKey = "ValidateCertificateKey";
            var certificateValidationMessageHandlerType = typeof(IMessageHandler<CertificateValidationMessage>);

            var containerBuilder = new ContainerBuilder();

            containerBuilder.Populate(services);

            containerBuilder
                .RegisterType<ScopedMessageHandler<CertificateValidationMessage>>()
                .Keyed<IMessageHandler<CertificateValidationMessage>>(validateCertificateBindingKey);

            containerBuilder
                .RegisterType<SubscriptionProcessor<CertificateValidationMessage>>()
                .WithParameter(
                    (parameter, context) => parameter.ParameterType == certificateValidationMessageHandlerType,
                    (parameter, context) => context.ResolveKeyed(validateCertificateBindingKey, certificateValidationMessageHandlerType))
                .As<ISubscriptionProcessor<CertificateValidationMessage>>();

            return new AutofacServiceProvider(containerBuilder.Build());
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Leases;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Jobs.Validation.Symbols.Core;
using NuGet.Services.Configuration;
using NuGet.Services.Entities;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.Messaging;
using NuGet.Services.Messaging.Email;
using NuGet.Services.ServiceBus;
using NuGet.Services.Sql;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using NuGet.Services.Validation.PackageSigning.ValidateCertificate;
using NuGet.Services.Validation.Symbols;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        /// <summary>
        /// The maximum number of concurrent connections that can be established to a single server.
        /// </summary>
        private const int MaximumConnectionsPerServer = 64;

        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string PackageSigningSectionName = "PackageSigning";
        private const string PackageCertificatesSectionName = "PackageCertificates";
        private const string ScanAndSignSectionName = "ScanAndSign";
        private const string SymbolScanOnlySectionName = "SymbolScanOnly";
        private const string RunnerConfigurationSectionName = "RunnerConfiguration";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string EmailConfigurationSectionName = "Email";
        private const string PackageDownloadTimeoutName = "PackageDownloadTimeout";
        private const string FlatContainerConfigurationSectionName = "FlatContainer";
        private const string LeaseConfigurationSectionName = "Leases";

        private const string EmailBindingKey = EmailConfigurationSectionName;
        private const string PackageVerificationTopicClientBindingKey = "PackageVerificationTopicClient";
        private const string PackageSignatureBindingKey = PackageSigningSectionName;
        private const string PackageCertificatesBindingKey = PackageCertificatesSectionName;
        private const string ScanAndSignBindingKey = ScanAndSignSectionName;
        private const string SymbolsScanBindingKey = "SymbolsScan";
        private const string ScanBindingKey = "Scan";
        private const string ValidationStorageBindingKey = "ValidationStorage";
        private const string OrchestratorBindingKey = "Orchestrator";
        private const string CoreLicenseFileServiceBindingKey = "CoreLicenseFileService";

        private const string SymbolsValidatorSectionName = "SymbolsValidator";
        private const string SymbolsValidationBindingKey = SymbolsValidatorSectionName;

        private const string SymbolsIngesterSectionName = "SymbolsIngester";
        private const string SymbolsIngesterBindingKey = SymbolsIngesterSectionName;

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private bool _validateOnly;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Indicates whether we had successful configuration validation or not
        /// </summary>
        public bool ConfigurationValidated { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            ServicePointManager.DefaultConnectionLimit = MaximumConnectionsPerServer;

            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _validateOnly = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, ValidateArgument, defaultValue: false);

            var configurationRoot = GetConfigurationRoot(configurationFilename, _validateOnly, out var secretInjector);
            _serviceProvider = GetServiceProvider(configurationRoot, secretInjector);

            ConfigurationValidated = false;
        }

        public override async Task Run()
        {
            var validator = GetRequiredService<ConfigurationValidator>();
            validator.Validate();
            ConfigurationValidated = true;

            if (_validateOnly)
            {
                Logger.LogInformation("Configuration validation successful");
                return;
            }

            var featureFlagRefresher = _serviceProvider.GetRequiredService<IFeatureFlagRefresher>();
            await featureFlagRefresher.StartIfConfiguredAsync();

            var runner = GetRequiredService<OrchestrationRunner>();
            await runner.RunOrchestrationAsync();

            await featureFlagRefresher.StopAndWaitAsync();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly, out ISecretInjector secretInjector)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();

            secretInjector = null;
            if (validateOnly)
            {
                // don't try to access KeyVault if only validation is requested:
                // we might not be running on a machine with KeyVault access.
                // Validation settings should not contain KeyVault references anyway
                return uninjectedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot, ISecretInjector secretInjector)
        {
            var services = new ServiceCollection();
            if (!_validateOnly)
            {
                services.AddSingleton(secretInjector);
            }

            ConfigureLibraries(services);
            ConfigureJobServices(services, configurationRoot);

            return CreateProvider(services, configurationRoot);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private DbConnection CreateDbConnection<T>(IServiceProvider serviceProvider) where T : class, IDbConfiguration, new()
        {
            var connectionString = serviceProvider.GetRequiredService<IOptionsSnapshot<T>>().Value.ConnectionString;
            var connectionFactory = new AzureSqlConnectionFactory(connectionString,
                serviceProvider.GetRequiredService<ISecretInjector>());

            return Task.Run(() => connectionFactory.CreateAsync()).Result;
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<ProcessSignatureConfiguration>(configurationRoot.GetSection(PackageSigningSectionName));
            services.Configure<ValidateCertificateConfiguration>(configurationRoot.GetSection(PackageCertificatesSectionName));
            services.Configure<OrchestrationRunnerConfiguration>(configurationRoot.GetSection(RunnerConfigurationSectionName));
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<EmailConfiguration>(configurationRoot.GetSection(EmailConfigurationSectionName));
            services.Configure<ScanAndSignConfiguration>(configurationRoot.GetSection(ScanAndSignSectionName));
            services.Configure<SymbolScanOnlyConfiguration>(configurationRoot.GetSection(SymbolScanOnlySectionName));
            services.Configure<ScanAndSignEnqueuerConfiguration>(configurationRoot.GetSection(ScanAndSignSectionName));
            services.Configure<FlatContainerConfiguration>(configurationRoot.GetSection(FlatContainerConfigurationSectionName));
            services.Configure<LeaseConfiguration>(configurationRoot.GetSection(LeaseConfigurationSectionName));

            services.Configure<SymbolsValidationConfiguration>(configurationRoot.GetSection(SymbolsValidatorSectionName));
            services.Configure<SymbolsIngesterConfiguration>(configurationRoot.GetSection(SymbolsIngesterSectionName));

            services.AddTransient<ConfigurationValidator>();
            services.AddTransient<OrchestrationRunner>();

            services.AddScoped<IEntitiesContext>(serviceProvider =>
                new EntitiesContext(
                    CreateDbConnection<GalleryDbConfiguration>(serviceProvider),
                    readOnly: false)
                    );
            services.AddScoped(serviceProvider =>
                new ValidationEntitiesContext(
                    CreateDbConnection<ValidationDbConfiguration>(serviceProvider)));

            services.AddScoped<IValidationEntitiesContext>(serviceProvider =>
                serviceProvider.GetRequiredService<ValidationEntitiesContext>());
            services.AddScoped<IValidationStorageService, ValidationStorageService>();
            services.Add(ServiceDescriptor.Transient(typeof(IEntityRepository<>), typeof(EntityRepository<>)));
            services.AddTransient<ICorePackageService, CorePackageService>();
            services.AddTransient<IEntityService<Package>, PackageEntityService>();
            services.AddTransient<ISubscriptionClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new SubscriptionClientWrapper(
                    configuration.ConnectionString,
                    configuration.TopicPath,
                    configuration.SubscriptionName,
                    serviceProvider.GetRequiredService<ILogger<SubscriptionClientWrapper>>());
            });
            services.AddTransient<ITopicClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
            });
            services.AddTransient<IPackageValidationEnqueuer, PackageValidationEnqueuer>();
            services.AddTransient<IValidatorProvider, ValidatorProvider>();
            services.AddTransient<IValidationSetProvider<Package>, ValidationSetProvider<Package>>();
            // Only one Orchestrator Message Handler will be registered.
            ConfigureOrchestratorMessageHandler(services, configurationRoot);
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<PackageValidationMessageData>, PackageValidationMessageDataSerializationAdapter>();
            services.AddTransient<ICriteriaEvaluator<Package>, PackageCriteriaEvaluator>();
            services.AddTransient<IProcessSignatureEnqueuer, ProcessSignatureEnqueuer>();
            services.AddTransient<ICloudBlobClient>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<ValidationConfiguration>>();
                return new CloudBlobClientWrapper(
                    configurationAccessor.Value.ValidationStorageConnectionString,
                    readAccessGeoRedundant: false);
            });
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();
            services.AddTransient<IFileDownloader, PackageDownloader>();
            services.AddTransient<IStatusProcessor<Package>, PackageStatusProcessor>();
            services.AddTransient<IValidationSetProvider<Package>, ValidationSetProvider<Package>>();
            services.AddTransient<IValidationSetProcessor, ValidationSetProcessor>();
            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<ScanAndSignMessage>, ScanAndSignMessageSerializer>();
            services.AddTransient<IValidatorStateService, ValidatorStateService>();
            services.AddTransient<ISimpleCloudBlobProvider, SimpleCloudBlobProvider>();
            services.AddTransient<PackageSignatureProcessor>();
            services.AddTransient<PackageSignatureValidator>();
            services.AddTransient<Messaging.IServiceBusMessageSerializer, Messaging.ServiceBusMessageSerializer>();
            services.AddTransient<IMessageServiceConfiguration, CoreMessageServiceConfiguration>();
            services.AddTransient<IMessageService, AsynchronousEmailMessageService>();
            services.AddTransient<IMessageService<Package>, PackageMessageService>();
            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddSingleton(new TelemetryClient(ApplicationInsightsConfiguration.TelemetryConfiguration));
            services.AddTransient<IValidationOutcomeProcessor<Package>, ValidationOutcomeProcessor<Package>>();
            services.AddSingleton(p =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var assemblyName = assembly.GetName().Name;
                var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                var client = new HttpClient(new WebRequestHandler
                {
                    AllowPipelining = true,
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
                });

                client.Timeout = configurationRoot.GetValue<TimeSpan>(PackageDownloadTimeoutName);
                client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");

                return client;
            });

            /// See <see cref="SubscriptionProcessorJob{T}.ConfigureDefaultJobServices(IServiceCollection, IConfigurationRoot)"/>
            /// for reasoning on why this is registered here.
            services.AddSingleton<IFeatureFlagRefresher, FeatureFlagRefresher>();

            ConfigureFileServices(services, configurationRoot);
            ConfigureOrchestratorSymbolTypes(services);
            ValidationJobBase.ConfigureFeatureFlagServices(services, configurationRoot);
        }

        private static IServiceProvider CreateProvider(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            containerBuilder
                .Register(c =>
                {
                    var serviceBusConfiguration = c.Resolve<IOptionsSnapshot<ServiceBusConfiguration>>();
                    var topicClient = new TopicClientWrapper(serviceBusConfiguration.Value.ConnectionString, serviceBusConfiguration.Value.TopicPath);
                    return topicClient;
                })
                .Keyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey);
            
            containerBuilder
                .RegisterType<ProcessSignatureEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IBrokeredMessageSerializer<SignatureValidationMessage>),
                    (pi, ctx) => ctx.Resolve<SignatureValidationMessageSerializer>()))
                .As<IProcessSignatureEnqueuer>();

            containerBuilder
                .RegisterType<ScopedMessageHandler<PackageValidationMessageData>>()
                .Keyed<IMessageHandler<PackageValidationMessageData>>(OrchestratorBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    ISubscriptionProcessor<PackageValidationMessageData>, 
                    SubscriptionProcessor<PackageValidationMessageData>, 
                    IMessageHandler<PackageValidationMessageData>>(
                        OrchestratorBindingKey);

            // Configure the email enqueuer.
            containerBuilder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<EmailConfiguration>>().Value.ServiceBus;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(EmailBindingKey);

            containerBuilder
                .RegisterTypeWithKeyedParameter<
                    IEmailMessageEnqueuer, 
                    EmailMessageEnqueuer, 
                    ITopicClient>(
                        EmailBindingKey);

            // Configure Validators
            var validatingType = configurationRoot
                .GetSection(RunnerConfigurationSectionName)
                .GetValue(nameof(OrchestrationRunnerConfiguration.ValidatingType), ValidatingType.Package);
            switch (validatingType)
            {
                case ValidatingType.Package:
                    ConfigurePackageSigningValidators(containerBuilder);
                    ConfigurePackageCertificatesValidator(containerBuilder);
                    ConfigureScanAndSignProcessor(containerBuilder);
                    ConfigureFlatContainer(containerBuilder);
                    break;
                case ValidatingType.SymbolPackage:
                    ConfigureSymbolScanValidator(containerBuilder);
                    ConfigureSymbolsValidator(containerBuilder);
                    ConfigureSymbolsIngester(containerBuilder);
                    break;
                default:
                    throw new NotImplementedException($"Unknown type: {validatingType}");
            }

            ValidationJobBase.ConfigureFeatureFlagAutofacServices(containerBuilder);
            ConfigureLeaseService(containerBuilder);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private static void ConfigurePackageSigningValidators(ContainerBuilder builder)
        {
            // Configure the validator state service for the package certificates validator.
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.PackageSignatureProcessor)
                .Keyed<IValidatorStateService>(PackageSignatureBindingKey);

            // Configure the package signature verification enqueuer.
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<ProcessSignatureConfiguration>>().Value.ServiceBus;

                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(PackageSignatureBindingKey);

            builder
                .RegisterType<ProcessSignatureEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), PackageSignatureBindingKey)
                .As<IProcessSignatureEnqueuer>();

            // Configure the package signature validators. The processor runs before packages are
            // repository signed and can strip unacceptable repository signatures. The validator
            // runs after packages are repository signed.
            builder
                .RegisterType<PackageSignatureProcessor>()
                .WithKeyedParameter(typeof(IValidatorStateService), PackageSignatureBindingKey)
                .As<PackageSignatureProcessor>();

            builder
                .RegisterType<PackageSignatureValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), PackageSignatureBindingKey)
                .As<PackageSignatureValidator>();
        }

        private static void ConfigurePackageCertificatesValidator(ContainerBuilder builder)
        {
            // Configure the validator state service for the package certificates validator.
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.PackageCertificate)
                .Keyed<IValidatorStateService>(PackageCertificatesBindingKey);

            // Configure the certificate verification enqueuer.
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<ValidateCertificateConfiguration>>().Value.ServiceBus;

                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(PackageCertificatesBindingKey);

            builder
                .RegisterType<ValidateCertificateEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), PackageCertificatesBindingKey)
                .As<IValidateCertificateEnqueuer>();

            // Configure the certificates validator.
            builder
                .RegisterType<PackageCertificatesValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), PackageCertificatesBindingKey)
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(TimeSpan?),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<ValidateCertificateConfiguration>>().Value.CertificateRevalidationThreshold)
                .As<PackageCertificatesValidator>();
        }

        private static void ConfigureLeaseService(ContainerBuilder builder)
        {
            builder
                .Register(c =>
                {
                    var config = c.Resolve<IOptionsSnapshot<LeaseConfiguration>>().Value;
                    var storageAccount = CloudStorageAccount.Parse(config.ConnectionString);
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    return new CloudBlobLeaseService(blobClient, config.ContainerName, config.StoragePath);
                })
                .As<ILeaseService>();
        }

        private static void ConfigureScanAndSignProcessor(ContainerBuilder builder)
        {
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<ScanAndSignConfiguration>>().Value.ServiceBus;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(ScanAndSignBindingKey);

            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.ScanAndSign)
                .Keyed<IValidatorStateService>(ScanAndSignBindingKey);

            builder
                .RegisterType<ScanAndSignEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), ScanAndSignBindingKey)
                .As<IScanAndSignEnqueuer>();

            builder
                .RegisterType<ScanAndSignProcessor>()
                .WithKeyedParameter(typeof(IValidatorStateService), ScanAndSignBindingKey)
                .AsSelf();
        }

        private static void ConfigureSymbolScanValidator(ContainerBuilder builder)
        {
            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<SymbolScanOnlyConfiguration>>().Value.ServiceBus;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(SymbolsScanBindingKey);

            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.SymbolScan)
                .Keyed<IValidatorStateService>(SymbolsScanBindingKey);

            builder
                .RegisterType<ScanAndSignEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), SymbolsScanBindingKey)
                .As<IScanAndSignEnqueuer>();

            builder
                .RegisterType<SymbolScanValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), SymbolsScanBindingKey)
                .AsSelf();
        }

        private static void ConfigureFlatContainer(ContainerBuilder builder)
        {
            builder
                .Register<CloudBlobClientWrapper>(c =>
                {
                    var configurationAccessor = c.Resolve<IOptionsSnapshot<FlatContainerConfiguration>>();
                    return new CloudBlobClientWrapper(
                        configurationAccessor.Value.ConnectionString,
                        readAccessGeoRedundant: false);
                })
                .Keyed<ICloudBlobClient>(CoreLicenseFileServiceBindingKey);

            builder
                .RegisterType<CloudBlobCoreFileStorageService>()
                .WithKeyedParameter(typeof(ICloudBlobClient), CoreLicenseFileServiceBindingKey)
                .Keyed<ICoreFileStorageService>(CoreLicenseFileServiceBindingKey);

            builder
                .RegisterType<OrchestratorContentFileMetadataService>()
                .As<IContentFileMetadataService>();

            builder
                .RegisterType<CoreLicenseFileService>()
                .WithKeyedParameter(typeof(ICoreFileStorageService), CoreLicenseFileServiceBindingKey)
                .As<ICoreLicenseFileService>();
        }

        private static void ConfigureOrchestratorMessageHandler(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            var validatingType = configurationRoot
                .GetSection(RunnerConfigurationSectionName)
                .GetValue(nameof(OrchestrationRunnerConfiguration.ValidatingType), ValidatingType.Package);
            switch (validatingType)
            {
                case ValidatingType.Package:
                    services.AddTransient<IMessageHandler<PackageValidationMessageData>, PackageValidationMessageHandler>();
                    break;
                case ValidatingType.SymbolPackage:
                    services.AddTransient<IMessageHandler<PackageValidationMessageData>, SymbolValidationMessageHandler>();
                    break;
                default:
                    throw new NotImplementedException($"Unknown type: {validatingType}");
            }
        }

        /// <summary>
        /// Configure the initialization of the File Service.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configurationRoot"></param>
        private static void ConfigureFileServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();
            var validatingType = configurationRoot
                .GetSection(RunnerConfigurationSectionName)
                .GetValue(nameof(OrchestrationRunnerConfiguration.ValidatingType), ValidatingType.Package);
            switch (validatingType)
            {
                case ValidatingType.Package:
                    services.AddTransient<IFileMetadataService, PackageFileMetadataService>();
                    services.AddTransient<IValidationFileService, ValidationFileService>();
                    break;
                case ValidatingType.SymbolPackage:
                    services.AddTransient<IFileMetadataService, SymbolPackageFileMetadataService>();
                    services.AddTransient<IValidationFileService, ValidationFileService>();
                    break;
                default:
                    throw new NotImplementedException($"Unknown type: {validatingType}");
            }
        }

        private static void ConfigureOrchestratorSymbolTypes(IServiceCollection services)
        {
            services.AddTransient<IEntityService<SymbolPackage>, SymbolEntityService>();
            services.AddTransient<IValidationSetProvider<SymbolPackage>, ValidationSetProvider<SymbolPackage>>();
            services.AddTransient<ICoreSymbolPackageService, CoreSymbolPackageService>();
            services.AddTransient<ICriteriaEvaluator<SymbolPackage>, SymbolCriteriaEvaluator>();
            services.AddTransient<IValidationOutcomeProcessor<SymbolPackage>, ValidationOutcomeProcessor<SymbolPackage>>();
            services.AddTransient<IStatusProcessor<SymbolPackage>, SymbolsStatusProcessor>();
            services.AddTransient<IValidationSetProvider<SymbolPackage>, ValidationSetProvider<SymbolPackage>>();
            services.AddTransient<IMessageService<SymbolPackage>, SymbolsPackageMessageService>();
            services.AddTransient<IBrokeredMessageSerializer<SymbolsValidatorMessage>, SymbolsValidatorMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<SymbolsIngesterMessage>, SymbolsIngesterMessageSerializer>();
            services.AddTransient<ISymbolsValidationEntitiesService, SymbolsValidationEntitiesService>();
        }

        private static void ConfigureSymbolsValidator(ContainerBuilder builder)
        {
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.SymbolsValidator)
                .Keyed<IValidatorStateService>(SymbolsValidationBindingKey);

            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<SymbolsValidationConfiguration>>().Value.ServiceBus;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(SymbolsValidationBindingKey);

            builder
                .RegisterType<SymbolsMessageEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), SymbolsValidationBindingKey)
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(TimeSpan?),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<SymbolsValidationConfiguration>>().Value.MessageDelay)
                .Keyed<ISymbolsMessageEnqueuer>(SymbolsValidationBindingKey)
                .As<ISymbolsMessageEnqueuer>();

            builder
                .RegisterType<SymbolsValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), SymbolsValidationBindingKey)
                .WithKeyedParameter(typeof(ISymbolsMessageEnqueuer), SymbolsValidationBindingKey)
                .AsSelf();
        }

        private static void ConfigureSymbolsIngester(ContainerBuilder builder)
        {
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.SymbolsIngester)
                .Keyed<IValidatorStateService>(SymbolsIngesterBindingKey);

            builder
                .Register(c =>
                {
                    var configuration = c.Resolve<IOptionsSnapshot<SymbolsIngesterConfiguration>>().Value.ServiceBus;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(SymbolsIngesterBindingKey);

            builder
                .RegisterType<SymbolsIngesterMessageEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), SymbolsIngesterBindingKey)
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(TimeSpan?),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<SymbolsIngesterConfiguration>>().Value.MessageDelay)
                .Keyed<ISymbolsIngesterMessageEnqueuer>(SymbolsIngesterBindingKey)
                .As<ISymbolsIngesterMessageEnqueuer>();

            builder
                .RegisterType<SymbolsIngester>()
                .WithKeyedParameter(typeof(IValidatorStateService), SymbolsIngesterBindingKey)
                .WithKeyedParameter(typeof(ISymbolsIngesterMessageEnqueuer), SymbolsIngesterBindingKey)
                .AsSelf();
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}

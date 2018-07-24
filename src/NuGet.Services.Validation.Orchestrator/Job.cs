// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Jobs.Validation.Common;
using NuGet.Jobs.Validation.PackageSigning.Messages;
using NuGet.Jobs.Validation.ScanAndSign;
using NuGet.Jobs.Validation.Storage;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation.Orchestrator.PackageSigning.ScanAndSign;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGet.Services.Validation.PackageSigning.ProcessSignature;
using NuGet.Services.Validation.PackageSigning.ValidateCertificate;
using NuGet.Services.Validation.Vcs;
using NuGetGallery.Diagnostics;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class Job : JobBase
    {
        private const string ConfigurationArgument = "Configuration";
        private const string ValidateArgument = "Validate";

        private const string ConfigurationSectionName = "Configuration";
        private const string VcsSectionName = "Vcs";
        private const string PackageSigningSectionName = "PackageSigning";
        private const string PackageCertificatesSectionName = "PackageCertificates";
        private const string ScanAndSignSectionName = "ScanAndSign";
        private const string RunnerConfigurationSectionName = "RunnerConfiguration";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string SmtpConfigurationSectionName = "Smtp";
        private const string EmailConfigurationSectionName = "Email";
        private const string PackageDownloadTimeoutName = "PackageDownloadTimeout";

        private const string VcsBindingKey = VcsSectionName;
        private const string PackageVerificationTopicClientBindingKey = "PackageVerificationTopicClient";
        private const string PackageSignatureBindingKey = PackageSigningSectionName;
        private const string PackageCertificatesBindingKey = PackageCertificatesSectionName;
        private const string ScanAndSignBindingKey = ScanAndSignSectionName;
        private const string ScanBindingKey = "Scan";
        private const string ValidationStorageBindingKey = "ValidationStorage";
        private const string OrchestratorBindingKey = "Orchestrator";

        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        private bool _validateOnly;
        private IServiceProvider _serviceProvider;

        /// <summary>
        /// Indicates whether we had successful configuration validation or not
        /// </summary>
        public bool ConfigurationValidated { get; set; }

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            _validateOnly = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, ValidateArgument, defaultValue: false);

            var configurationRoot = GetConfigurationRoot(configurationFilename, _validateOnly, out var secretInjector);
            _serviceProvider = GetServiceProvider(configurationRoot, secretInjector);
            
            if (!_validateOnly)
            {
                RegisterDatabase<GalleryDbConfiguration>(_serviceProvider);
                RegisterDatabase<ValidationDbConfiguration>(_serviceProvider);
            }

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

            var runner = GetRequiredService<OrchestrationRunner>();
            await runner.RunOrchestrationAsync();
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, bool validateOnly, out ISecretInjector secretInjector)
        {
            Logger.LogInformation("Using the {ConfigurationFilename} configuration file", configurationFilename);
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

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

            return CreateProvider(services);
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // we do not call services.AddOptions here, because we want our own custom version of IOptionsSnapshot 
            // to be present in the service collection for KeyVault secret injection to work properly
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        private void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<ValidationConfiguration>(configurationRoot.GetSection(ConfigurationSectionName));
            services.Configure<VcsConfiguration>(configurationRoot.GetSection(VcsSectionName));
            services.Configure<ProcessSignatureConfiguration>(configurationRoot.GetSection(PackageSigningSectionName));
            services.Configure<ValidateCertificateConfiguration>(configurationRoot.GetSection(PackageCertificatesSectionName));
            services.Configure<OrchestrationRunnerConfiguration>(configurationRoot.GetSection(RunnerConfigurationSectionName));
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<SmtpConfiguration>(configurationRoot.GetSection(SmtpConfigurationSectionName));
            services.Configure<EmailConfiguration>(configurationRoot.GetSection(EmailConfigurationSectionName));
            services.Configure<ScanAndSignConfiguration>(configurationRoot.GetSection(ScanAndSignSectionName));
            services.Configure<ScanAndSignEnqueuerConfiguration>(configurationRoot.GetSection(ScanAndSignSectionName));

            services.AddTransient<ConfigurationValidator>();
            services.AddTransient<OrchestrationRunner>();
            
            services.AddScoped<NuGetGallery.IEntitiesContext>(serviceProvider =>
                new NuGetGallery.EntitiesContext(
                    CreateSqlConnection<GalleryDbConfiguration>(),
                    readOnly: false)
                    );
            services.AddScoped(serviceProvider =>
                new ValidationEntitiesContext(
                    CreateSqlConnection<ValidationDbConfiguration>()));

            services.AddScoped<IValidationEntitiesContext>(serviceProvider =>
                serviceProvider.GetRequiredService<ValidationEntitiesContext>());
            services.AddScoped<IValidationStorageService, ValidationStorageService>();
            services.Add(ServiceDescriptor.Transient(typeof(NuGetGallery.IEntityRepository<>), typeof(NuGetGallery.EntityRepository<>)));
            services.AddTransient<NuGetGallery.ICorePackageService, NuGetGallery.CorePackageService>();
            services.AddTransient<IEntityService<Package>, PackageEntityService>();
            services.AddTransient<ISubscriptionClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new SubscriptionClientWrapper(configuration.ConnectionString, configuration.TopicPath, configuration.SubscriptionName);
            });
            services.AddTransient<ITopicClient>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
            });
            services.AddTransient<IPackageValidationEnqueuer, PackageValidationEnqueuer>();
            services.AddTransient<IValidatorProvider, ValidatorProvider>();
            services.AddTransient<IValidationSetProvider<Package>, ValidationSetProvider<Package>>();
            services.AddTransient<IMessageHandler<PackageValidationMessageData>, PackageValidationMessageHandler>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<PackageValidationMessageData>, PackageValidationMessageDataSerializationAdapter>();
            services.AddTransient<ICriteriaEvaluator<Package>, PackageCriteriaEvaluator>();
            services.AddTransient<VcsValidator>();
            services.AddTransient<IProcessSignatureEnqueuer, ProcessSignatureEnqueuer>();
            services.AddTransient<NuGetGallery.ICloudBlobClient>(c =>
                {
                    var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<ValidationConfiguration>>();
                    return new NuGetGallery.CloudBlobClientWrapper(
                        configurationAccessor.Value.ValidationStorageConnectionString,
                        readAccessGeoRedundant: false);
                });
            services.AddTransient<NuGetGallery.ICoreFileStorageService, NuGetGallery.CloudBlobCoreFileStorageService>();
            services.AddTransient<IFileMetadataService, PackageFileMetadataService>();
            services.AddTransient<IValidationFileService, ValidationFileService>();
            services.AddTransient<IFileDownloader, PackageDownloader>();
            services.AddTransient<IStatusProcessor<Package>, EntityStatusProcessor<Package>>();
            services.AddTransient<IValidationSetProvider<Package>, ValidationSetProvider<Package>>();
            services.AddTransient<IValidationSetProcessor, ValidationSetProcessor>();
            services.AddTransient<IBrokeredMessageSerializer<SignatureValidationMessage>, SignatureValidationMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<CertificateValidationMessage>, CertificateValidationMessageSerializer>();
            services.AddTransient<IBrokeredMessageSerializer<ScanAndSignMessage>, ScanAndSignMessageSerializer>();
            services.AddTransient<IValidatorStateService, ValidatorStateService>();
            services.AddTransient<ISimpleCloudBlobProvider, SimpleCloudBlobProvider>();
            services.AddTransient<PackageSignatureProcessor>();
            services.AddTransient<PackageSignatureValidator>();
            services.AddTransient<MailSenderConfiguration>(serviceProvider =>
            {
                var smtpConfigurationAccessor = serviceProvider.GetRequiredService<IOptionsSnapshot<SmtpConfiguration>>();
                var smtpConfiguration = smtpConfigurationAccessor.Value;
                if (string.IsNullOrWhiteSpace(smtpConfiguration.SmtpUri))
                {
                    return new MailSenderConfiguration();
                }
                var smtpUri = new SmtpUri(new Uri(smtpConfiguration.SmtpUri));
                return new MailSenderConfiguration
                {
                    DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                    Host = smtpUri.Host,
                    Port = smtpUri.Port,
                    EnableSsl = smtpUri.Secure,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        smtpUri.UserName,
                        smtpUri.Password)
                };
            });
            services.AddTransient<IMailSender>(serviceProvider =>
            {
                var mailSenderConfiguration = serviceProvider.GetRequiredService<MailSenderConfiguration>();
                return string.IsNullOrWhiteSpace(mailSenderConfiguration.Host)
                    ? (IMailSender)new DiskMailSender()
                    : (IMailSender)new MailSender(mailSenderConfiguration);
            });
            services.AddTransient<ICoreMessageServiceConfiguration, CoreMessageServiceConfiguration>();
            services.AddTransient<ICoreMessageService, CoreMessageService>();
            services.AddTransient<IMessageService<Package>, PackageMessageService>();
            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, TelemetryService>();
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddSingleton(new TelemetryClient());
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
        }

        private static IServiceProvider CreateProvider(IServiceCollection services)
        {
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            /// Initialize dependencies for the <see cref="VcsValidator"/>. There is some additional complexity here
            /// because the implementations require ambiguous types (such as a <see cref="string"/> and a
            /// <see cref="CloudStorageAccount"/> which there may be more than one configuration of).
            containerBuilder
                .Register(c =>
                {
                    var vcsConfiguration = c.Resolve<IOptionsSnapshot<VcsConfiguration>>();
                    var cloudStorageAccount = CloudStorageAccount.Parse(vcsConfiguration.Value.DataStorageAccount);
                    return cloudStorageAccount;
                })
                .Keyed<CloudStorageAccount>(VcsBindingKey);
            containerBuilder
                .Register(c =>
                {
                    var serviceBusConfiguration = c.Resolve<IOptionsSnapshot<ServiceBusConfiguration>>();
                    var topicClient = new TopicClientWrapper(serviceBusConfiguration.Value.ConnectionString, serviceBusConfiguration.Value.TopicPath);
                    return topicClient;
                })
                .Keyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey);

            containerBuilder
                .RegisterType<PackageValidationService>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationService>();

            containerBuilder
                .RegisterType<PackageValidationAuditor>()
                .WithKeyedParameter(typeof(CloudStorageAccount), VcsBindingKey)
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ctx.Resolve<IOptionsSnapshot<VcsConfiguration>>().Value.ContainerName))
                .As<IPackageValidationAuditor>();

            containerBuilder
                .RegisterType<ProcessSignatureEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<TopicClientWrapper>(PackageVerificationTopicClientBindingKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(IBrokeredMessageSerializer<SignatureValidationMessage>),
                    (pi, ctx) => ctx.Resolve<SignatureValidationMessageSerializer>()
                    ))
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

            ConfigurePackageSigningValidators(containerBuilder);
            ConfigurePackageCertificatesValidator(containerBuilder);
            ConfigureScanAndSignProcessor(containerBuilder);
            ConfigureScanValidator(containerBuilder);

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

        private static void ConfigureScanValidator(ContainerBuilder builder)
        {
            builder
                .RegisterType<ValidatorStateService>()
                .WithParameter(
                    (pi, ctx) => pi.ParameterType == typeof(string),
                    (pi, ctx) => ValidatorName.ScanOnly)
                .Keyed<IValidatorStateService>(ScanBindingKey);

            builder
                .RegisterType<ScanValidator>()
                .WithKeyedParameter(typeof(IValidatorStateService), ScanBindingKey)
                .AsSelf();
        }

        private T GetRequiredService<T>()
        {
            return _serviceProvider.GetRequiredService<T>();
        }
    }
}
